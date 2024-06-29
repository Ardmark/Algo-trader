using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using AlgoTrader.Shared.MovingAverages;
using AlgoTrader.Shared.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace AlgoTrader.Shared
{
    public class Order_EMABounce : Order
    {

        #region Init
        public Order_EMABounce(string symbolName, int timeInterval, TradeEngine tradeEngine, Chart chart) : base(symbolName, timeInterval, tradeEngine, chart)
        {
        }
        #endregion

        EMA GetEMA(double emaLength)
        {
            try
            {
                foreach (var ma in tradeEngine.movingAverages)
                {
                    if (ma.movingAverageLength == emaLength - TradeEngine.emaOffset)
                    {
                        return ma as EMA;
                    }
                }
            }
            catch 
            { 
            }
            return tradeEngine.ema_200;
        }
        #region Enter trade
        public override bool CanEnterTrade(SymbolDataItem item, Trade order, DataContext db)
        {
            #region Check frame count OK
            if (!MainObj.BACKTEST)
            {
                if (tradeEngine.dataReady == false)
                {
                    return false;
                }
            }

            if (order == null)
            {
                LogTrades.WriteLine($"{item.SymbolName} could not enter as active order was null, at {DateTime.UtcNow:G}");
                return false;
            }

            var frame = chart.GetFrameForTimestamp(item.Timestamp); //because a backtest trade can be at any point

            if (frame >= tradeEngine.results.Count)
            {
                //this would happen if a trade was set after startup but before interpolation of missed frames. However, there is also a flag in tradeEngine to block this
                frame = tradeEngine.Results.Count - 1;
            }
            if (frame < TimeFrame)
            {
                LogTrades.WriteLine($"{item.SymbolName} could not enter as frame < timeframe, at {DateTime.UtcNow:G}");
                return false;
            }
            #endregion

            //only dealing with long positions here, to simplify code

            double emaPrice = GetEMA(order.ActionValue).GetResult(frame, order.TimeFrame);
            double orderPrice = emaPrice * (1 + tradeEngine.EMA_BREAK_PRICE_THRESHOLD_PERCENT / 100);
            
            order.Price = emaPrice;
            order.Timestamp = item.Timestamp;
            db.SaveChanges();

            //note - this may fail on 1-minute, since it does not use the open value, hence the extra minute added
            var timeInterval = TimeFrame + 1;

            //has price crossed order price?
            bool hasCrossedEMA = false;           
            var prevPrice = tradeEngine.Results[frame - timeInterval].Close;

            //check price has crossed the level above the ema, based on tradeEngine.EMA_BREAK_PRICE_THRESHOLD_PERCENT
            if (prevPrice < orderPrice && item.Close >= orderPrice)
            {

                var currFrame = frame;

                //get first preceding frame where emas crossed down (most likely this one)
                while (currFrame-- > timeInterval) //TODO - RETAIN STATE
                {
                    if (!MovingAverage.IsAbove(tradeEngine.ema_5, tradeEngine.ema_10, currFrame, TimeFrame))
                    {
                        break;
                    }
                }

                //now get first preceding frame where emas are crossed up, this is the cross down point
                while (currFrame-- > timeInterval)
                {
                    if (MovingAverage.IsAbove(tradeEngine.ema_5, tradeEngine.ema_10, currFrame, TimeFrame))
                    {
                        //order.ActionValue = currFrame;
                        //db.SaveChanges();
                        break;
                    }
                }

                //work backward and check that the price has crossed the 200 ema
                //this way, we know the price has risen from the 200 ema
                //for (int i = frame; i > (int)order.ActionValue; i--)
                for (int i = frame; i > currFrame; i--)
                {
                    prevPrice = tradeEngine.Results[i - timeInterval].Close;
                    var currPrice = tradeEngine.Results[i].Close;
                    var _emaPrice = tradeEngine.ema_200.GetResult(frame, order.TimeFrame);
                    if (prevPrice < _emaPrice && currPrice >= _emaPrice)
                    {
                        hasCrossedEMA = true;
                        break;
                    }
                }
            }

            if (hasCrossedEMA)
            {
                #region Test for max entry over break
                if (TradeEngine.MAX_ENTRY_THRESHOLD_OVER_BREAK > 0)
                {
                    var maxOrderPrice = orderPrice * (1 + TradeEngine.MAX_ENTRY_THRESHOLD_OVER_BREAK / 100);

                    //don't allow entry if a sudden spike take price too far above break price
                    //however, this will often happen with backtests as only 1-min close is available so ignore that
                    if (item.Close > maxOrderPrice)
                    {
                        var pcOver = (item.Close / orderPrice - 1) * 100;
                        LogTrades.WriteLine(string.Format("Trade {0}, Id {1} was {2}% above ema break at entry point, and was not entered at {3}", order.SymbolName, order.Id, pcOver.ToString("0.0"), item.Timestamp), order);
                        return false;
                    }
                }
                #endregion

                #region Test for interruption

                var prevCandle = tradeEngine.Results[frame - 1];
                var elapsedTime = item.Timestamp.Subtract(prevCandle.Timestamp).TotalMinutes;

                //When storing the CURRENT binance frame in the Results lists, at the end, the seconds value is always 59, but the fractional value is just shy of one minute
                // - so technically, the current frame is one minute advanced
                // - so the previous frame will be 119.999 seconds behind, which will round to 2 minutes in elapsedTime
                // - so any time of 3 mins or greater will be a gap in the data
                if (prevCandle.Interrupted || elapsedTime > 4) //allow 2  minutes, will be 2 mins for consecutive frames
                {
                    LogTrades.WriteLine(string.Format("Trade {0}, Id {1} had {2} during entry, and was not entered at {3}", order.SymbolName, order.Id, prevCandle.Interrupted ? "data interpolation" : "data dropout", item.Timestamp), order);
                    return false;
                }
                #endregion

                #region Test for 'red' 200EMA (200 is above 20)
                if (MovingAverage.IsAbove(tradeEngine.ema_200, tradeEngine.ema_20, frame, TimeFrame))
                {
                    //since this is an entry point, will terminate the trade entirely here
                    order.ItemStatus = TradeItemStatus.CLOSED;
                    order.GlobalStatus = TradeGlobalStatus.STOPPED;
                    MainObj.RemoveActiveOrder(order);
                    tradeEngine.activeOrder = null;

                    db.SaveChanges();
                    return false;
                }
                #endregion

                return true;

            }
            return false;

        }

        #endregion

        internal override bool ExecuteStop(Trade order, Trade stop, Trade entry, Trade floor, SymbolDataItem currentDataItem, int currentFrame, bool itemAdded, DataContext db)
        {
            bool hasStopped = base.ExecuteStop(order, stop, entry, floor, currentDataItem, currentFrame, itemAdded, db);

            if (hasStopped)
            {
                //only exit if 5EMA is above 20EMA
                //todo - this should really be a test for cross above within the trading period
                if (!MovingAverage.IsAbove(tradeEngine.ema_5, tradeEngine.ema_20, currentFrame, TimeFrame))
                {
                    hasStopped = false;
                }
            }
            return hasStopped;
        }
        #region Set order

        internal override Trade SetOrder(int timeFrame, TradePosition tradePosition, TradeType tradeType, string symbolName, ExchangeId exchangeId, DataContext db, bool isBacktest, double price, int frameNum = -1, int emaLength = -1)
        {
            //don't allow more than one order, or closing one could cause another unseen order to be immediately executed
            //however, can set orders on multiple symbols, only one will execute if Constants.ALLOW_MULTIPLE_SYMBOL_TRADES == false
            if (ActiveOrder != null)
            {
                AlertDialog.Show("Cannot set 2 active trades to run concurrently on same symbol", "Not possible");
                return null;
            }
            Trade ret = null;

            //get the frame to place the market order. If this position is outside of the chart bounds, set it on the last candle
            //only get frame if not already supplied, as for line break order. Otherwise, will be regular break order so use last frame position (unless backtest)
            if (frameNum == -1)
            {
                frameNum = isBacktest ? Chart.GetFrameNumFromLastMousePos() : Chart.GetLastFrameNum();
            }

            MarketEntry marketOrderPosition = new MarketEntry
            {
                frame = frameNum,
                position = tradePosition
            };

            double orderPrice = GetEMA(emaLength).GetResult(frameNum, timeFrame); //will set price correctly next ItemUpdated(), but not if backtesting with no updates

            try
            {
                DateTime timeStamp = isBacktest || frameNum != -1 ? Chart.GetTimestampFromFrameNum(frameNum) : DateTime.UtcNow;
                ret = new Trade
                {
                    System = isBacktest ? TradeSystem.BACKTEST : TradeSystem.LIVE,
                    GlobalStatus = isBacktest ? TradeGlobalStatus.STOPPED : TradeGlobalStatus.ACTIVE,
                    ItemStatus = isBacktest ? TradeItemStatus.CLOSED : TradeItemStatus.OPEN,
                    Timestamp = timeStamp,
                    TradeId = Trade.GetNewTradeId(),
                    Type = tradeType,
                    Position = marketOrderPosition.position,
                    Action = TradeAction.ORDER,
                    Price = orderPrice,
                    ActionValue = emaLength,
                    TimeFrame = timeFrame,
                    SymbolName = symbolName,
                    ExchangeId = exchangeId,
                    ExecutionSystem = TradeExecutionSystem.MANUAL,
                    ChartLineId = -1,
                    
                    OrderType = OrderType.ENTERED_OFF_EXCHANGE

                };

                db.Trades.Add(ret);
                db.SaveChanges();
            }
#pragma warning disable CS0168
            catch (Exception e)
            {
                return null;
            }

            MainObj.DrawCurrentDispatcher(db);
            return ret;
        }

        #endregion

        #region Terminate trade
        public override bool TerminateTrade(Trade trade, SymbolDataItem item, DataContext db)
        {
            if (trade.Type == TradeType.LINE_BREAK_ORDER)
            {
                var line = db.Lines.Where(x => x.Id == trade.ChartLineId).FirstOrDefault();
                if (line != null)
                {
                    return !line.TimeWithinLinePeriod(item.Timestamp);
                }
            }

            return false;
        }
        #endregion

        #region Unused
        /*    
         *    
                //return the next EMA crossover for UI scrolling
        public MarketEntry GetNextCrossoverPosition(bool forward)
        {
            bool firstCrossover = true;
            bool previousCrossUp = false;
            int totFrames = mainViewModel.TotalFrames;
            MovingIndicatorBase indicator1 = indicators.First();
            MovingIndicatorBase indicator2 = indicators.Last();

            int startPos = (mainViewModel.ScrollBars + mainViewModel.ScaleBars) * mainViewModel.TimeFrame;// tradeEntryStartPos;

            //need to skip all the initialisation frames
            if (startPos < indicator2.values.Count * mainViewModel.TimeFrame)
            {
                startPos = indicator2.values.Count * mainViewModel.TimeFrame;
            }
            if (startPos > indicator1.results.Count - 1)
            {
                startPos = indicator1.results.Count - 1;
            }
            int increment = mainViewModel.TimeFrame;
            int numResults = indicator2.results.Count;

            //local method
            MarketEntry? GetPosition(int i)
            {
                bool next = MovingIndicatorBase.AboveOrBelow(indicator1, indicator2, i);
                if (firstCrossover)
                {
                    previousCrossUp = next;
                    firstCrossover = false;
                }
                else
                {
                    if (!previousCrossUp && next)
                    {

                        DateTime dt = mainViewModel.Chart.results[i].Timestamp;
                        MovingIndicatorBase.AboveOrBelow(indicator1, indicator2, i);
                        if (!mainViewModel.IsInterrupted(i))
                        {
                            i++; //just to show the next frame
                            previousCrossUp = true;
                            return new MarketEntry
                            {
                                position = TradePosition.LONG,
                                frame = i
                            };
                        }
                    }
                    if (previousCrossUp && !next)
                    {
                        MovingIndicatorBase.AboveOrBelow(indicator1, indicator2, i);
                        DateTime dt = mainViewModel.Chart.results[i].Timestamp;
                        if (!mainViewModel.IsInterrupted(i))
                        {
                            i++;
                            previousCrossUp = false;
                            return new MarketEntry
                            {
                                position = TradePosition.SHORT,
                                frame = i
                            };
                        }
                    }
                }
                return null;
            }

            if (forward)
            {
                for (int i = startPos; i < totFrames && i < numResults; i += increment)
                {
                    MarketEntry? ret = GetPosition(i);
                    if (ret != null)
                    {
                        return (MarketEntry)ret;
                    }
                }
            }
            else
            {
                for (int i = startPos - 1; i > -1; i--) //must start one frame less because one gets added to the forward loop
                {
                    MarketEntry? ret = GetPosition(i);
                    if (ret != null)
                    {
                        return (MarketEntry)ret;
                    }
                }
            }

            return MarketEntry.None;
        }
        
        public bool HasCrossed(int firstFrame, int lastFrame, TradePosition position)
        {
            Debug.Assert(position != TradePosition.NONE);

            MovingIndicatorBase m1 = indicators.First();
            MovingIndicatorBase m2 = indicators.Last();

            if (m1.results.Count > firstFrame && m2.results.Count > lastFrame)
            {
                //m1 assumed to be the shorter ema
                //m2 assumed to be the longer ema
                var diff1 = m1.results[firstFrame] - m2.results[firstFrame];
                for (int i = firstFrame + 1; i <= lastFrame; i++)
                {
                    var diff2 = m1.results[i] - m2.results[i];
                    if (diff2 * diff1 < 0)
                    {
                        //signs are different, so the lines have now crossed
                        //if short, the shorter ema would be below the longer at first frame, and this would reverse on crossover
                        //if long, the opposite would apply
                        //BUT if short and the shorter ema is above the longer to start with, then the crossover would be a downtrend so wouldn't be counted here
                        return position == TradePosition.SHORT
                            ? diff1 < 0 && diff2 > 0
                            : diff1 > 0 && diff2 < 0;
                    }
                }
            }
            return false;
        }

        public override IEnumerable<TradeEntryPosition> GetTradeEntry()
        {
            int totFrames = mainViewModel.TotalFrames;
            MovingIndicatorBase indicator1 = indicators.First();
            MovingIndicatorBase indicator2 = indicators.Last();

            for (int i=0; i<totFrames; i++) 
            {
                bool next = MovingIndicatorBase.AboveOrBelow(indicator1, indicator2, i);
                if (!previousCrossUp && next)
                {
                    previousCrossUp = true;
                    yield return new TradeEntryPosition
                    {
                        tradeEntryType = TradeEntryPosition.TradeEntryType.LONG,
                        frame = i
                    };
                }
                if (previousCrossUp && !next)
                {
                    previousCrossUp = false;
                    yield return new TradeEntryPosition
                    {
                        tradeEntryType = TradeEntryPosition.TradeEntryType.SHORT,
                        frame = i
                    };
                }
                if (firstCrossover)
                {
                    previousCrossUp = next;
                    firstCrossover = false;
                }
            }
        }
        */
        #endregion
    }


}
