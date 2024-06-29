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
    public class Order_MarketBreak : Order
    {
        readonly SolidColorBrush colorUp, colorDown;
        private readonly int lineThickness;

        #region Init
        public Order_MarketBreak(string symbolName, int timeInterval, TradeEngine tradeEngine, Chart chart, CandlePosition candlePosition, int lineThickness, SolidColorBrush colorDown, SolidColorBrush colorUp) : base(symbolName, timeInterval, tradeEngine, chart)
        {
            this.lineThickness = lineThickness;
            this.colorUp = colorUp;
            this.colorDown = colorDown;
        }
        #endregion

        public override bool CanEnterTrade(SymbolDataItem item, DataContext db)
        {
            return HasCrossedEntryPoint(item, db);
        }
        protected virtual bool HasCrossedEntryPoint(SymbolDataItem item, DataContext db)
        {
            if (!MainObj.BACKTEST)
            {
                if (tradeEngine.dataReady == false)
                {
                    return false;
                }
            }

            if (ActiveOrder == null)
            {
                LogTrades.WriteLine($"{item.SymbolName} could not enter as active order was null, at {DateTime.UtcNow:G}");
                return false;
            }

            var orderPrice = ActiveOrder.Price; //for a market break order

            if (ActiveOrder.Type == TradeType.LINE_BREAK_ORDER)
            {
                //get price from line at current timestamp
                var chartLine = db.Lines.Where(x => x.Id == ActiveOrder.ChartLineId).FirstOrDefault();
                if (chartLine != null)
                {
                    bool isWithinLine = false;
                    (orderPrice, isWithinLine) = chartLine.GetPriceAtTime(item.Timestamp, true);
                    if (!isWithinLine) //outside of line
                    {
                        //LogTrades.WriteLine($"{item.SymbolName} could not enter as entry point outside line, at {DateTime.UtcNow:G}");
                        return false;
                    }
                }
            }

            var frame = chart.GetFrameForTimestamp(item.Timestamp); //because a backtest trade can be at any point

            if (frame >= tradeEngine.results.Count)
            {
                //this would happen if a trade was set after startup but before interpolation of missed frames. However, there is also a flag in tradeEngine to block this
                frame = tradeEngine.Results.Count - 1;
            }
            if (frame < 1)
            {
                LogTrades.WriteLine($"{item.SymbolName} could not enter as frame < 1, at {DateTime.UtcNow:G}");
                return false;
            }

            //NOTE - THIS WILL ONLY ENTER ON 1-MIN CLOSE, AS EMA'S ARE NOT DYNAMICALLY UPDATED IN TRADE ENGINE
            var emaSmallPrice = emaUpDown.GetResultSmall(frame); //item.Close;
            var emaLargePrice = emaUpDown.GetResultLarge(frame);

            bool canEnter = false;

            //no entry unless emas are crossed up


            // var prevItem = emaUpDown.GetResultSmall(frame - 1);// tradeEngine.Results[frame - 1];
            if (ActiveOrder.Position == TradePosition.LONG
                           ? emaSmallPrice > orderPrice
                           : emaSmallPrice < orderPrice)
            {
                LogTrades.WriteLine($"{item.SymbolName} entry reached as emaSmall price > orderPrice, at {DateTime.UtcNow:G}", ActiveOrder);
                if (ActiveOrder.Position == TradePosition.LONG
                          ? emaSmallPrice > emaLargePrice
                          : emaSmallPrice < emaLargePrice)
                {
                    canEnter = true;
                }
                else
                {
                    LogTrades.WriteLine($"{item.SymbolName} could not enter as emaSmall price <= emaLarge price, at {DateTime.UtcNow:G}", ActiveOrder);
                }
            }



            if (canEnter)
            {
                var maxOrderPrice = ActiveOrder.Position == TradePosition.LONG
                                                ? orderPrice * (1 + TradeEngine.MAX_ENTRY_THRESHOLD_OVER_BREAK / 100)
                                                : orderPrice / (1 + TradeEngine.MAX_ENTRY_THRESHOLD_OVER_BREAK / 100);

                //don't allow entry if a sudden spike take price too far above break price
                //however, this will often happen with backtests as only 1-min close is available so ignore that
          //      if (!MainObj.IsBacktesting)
                {
                    if (ActiveOrder.Position == TradePosition.LONG
                                  ? item.Close > maxOrderPrice
                                  : item.Close < maxOrderPrice)
                    {
                        var pcOver = 100 * (emaSmallPrice - orderPrice) / emaSmallPrice;
                        LogTrades.WriteLine(string.Format("Trade {0}, Id {1} was {2}% above ema break at entry point, and was not entered at {3}", ActiveOrder.SymbolName, ActiveOrder.Id, pcOver.ToString("0.0"), item.Timestamp), ActiveOrder);
                        return false;
                    }
                }
                var prevCandle = tradeEngine.Results[frame - 1];
                var elapsedTime = item.Timestamp.Subtract(prevCandle.Timestamp).TotalMinutes;

                //When storing the CURRENT binance frame in the Results lists, at the end, the seconds value is always 59, but the fractional value is just shy of one minute
                // - so technically, the current frame is one minute advanced
                // - so the previous frame will be 119.999 seconds behind, which will round to 2 minutes in elapsedTime
                // - so any time of 3 mins or greater will be a gap in the data
                if (prevCandle.Interrupted || elapsedTime > 4) //allow 2  minutes, will be 2 mins for consecutive frames
                {
                    //close the order at this point, the entry position has moved on past the immediate break point
                    LogTrades.WriteLine(string.Format("Trade {0}, Id {1} had {2} during entry, and was not entered at {3}", ActiveOrder.SymbolName, ActiveOrder.Id, prevCandle.Interrupted ? "data interpolation" : "data dropout", item.Timestamp), ActiveOrder);
                    //tradeEngine.ClosePendingTrade(db);
                    //MainObj.Draw(db);
                    return false;
                }
                LogTrades.WriteLine($"Trade {ActiveOrder.SymbolName} entering at frame {frame} on {DateTime.UtcNow:G}", ActiveOrder);
                return true;
            }

            return false;
        }

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


        #region Set order

        internal virtual Trade SetOrder(TradePosition tradePosition, TradeType tradeType, string symbolName, ExchangeId exchangeId, DataContext db, bool isBacktest, double price, int frameNum = -1, int lineId = -1)
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

            if (tradeType== TradeType.MA_BREAK_ORDER)
            {
                price = emaUpDown.GetResultLarge(frameNum);
            }
            double orderPrice = price == 0 ? Chart.GetTradePriceFromLastMouseY() : price;

            if (!isBacktest)
            {
                if (tradeType == TradeType.PRICE_BREAK_ORDER)
                {
                    if (orderPrice <= tradeEngine.currentDataItem.Close)
                    {
                        AlertDialog.Show("Cannot set break order below current price", "Not possible");
                        return null;
                    }
                }
                if (tradeType == TradeType.LIMIT_ORDER)
                {
                    if (orderPrice >= tradeEngine.currentDataItem.Close)
                    {
                        AlertDialog.Show("Cannot set limit order above current price", "Not possible");
                        return null;
                    }
                }
            }

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
                    ActionValue = 0, 
                    TimeFrame = TimeFrame,
                    SymbolName = symbolName,
                    ExchangeId = exchangeId,
                    ExecutionSystem = TradeExecutionSystem.MANUAL,
                    ChartLineId = lineId,
                    OrderType = OrderType.TEST

                };

                db.Trades.Add(ret);
                db.SaveChanges();
            }
            catch (Exception e)
            {
                return null;
            }

            MainObj.DrawCurrentDispatcher(db);
            return ret;
        }

        #endregion

        public override void Draw(DataContext db)
        {
            if (MainObj.IsBacktesting)
            {
                //      return;
            }
            //can only draw one line here, as the base class will re-init the line list count and then reuse line segments which will already be added to canvas 
            //DrawLineOverlayChart(ema_60.ma, 1, Brushes.Gray);
            //DrawLineOverlayChart(ema_60.ma, 2, Brushes.Blue, true); - set param to true, to draw more than one line

            int startFrame = Chart.ScrollBars;
            int numFrames = Chart.ScaleBars;

            InitDraw();

            //todo - rework to draw on the chart canvas, not using the ancilliary canvases now
            for (int i = 0; i < indicators.Count; i++)
            {
                canvas = Chart.canvas;

                List<double> itemsLarge = emaUpDown.GetResultsLarge(startFrame, numFrames);
                List<double> itemsSmall = emaUpDown.GetResultsSmall(startFrame, numFrames);

                #region set the scaling to exactly fill the panel

                Low = Chart.Low;
                High = Chart.High;

                SetScaling();

                #endregion


                Point start = new Point();
                Point end = new Point();

                void DrawEMAColored(EMA emaLarge, EMA emaSmall, List<double> items, int lineWidth)
                {
                    int count = 0;

                    //need the actual start frame in the visible timeframe, which is available from GetFrameCount()
                    int frame = startFrame * MainObj.TimeFrame;
                    int _numFrames = numFrames;
                    foreach (double val in items)
                    {
                        if (frame < 2)
                        {
                            continue;
                        }
                        if (count == 0)
                        {
                            start.X = count;
                            start.Y = val;
                        }
                        else
                        {
                            start.X = end.X;
                            start.Y = end.Y;
                        }
                        end.X = count;
                        end.Y = val;

                        //get a suitable color
                        //bool _isRising60 = MovingAverage.IsRising(emaUpDown.emaLarge, frame, emaUpDown.timeFrame);
                        bool _isAbove60 = MovingAverage.IsAbove(emaUpDown.emaSmall, emaUpDown.emaLarge, frame, emaUpDown.timeFrame);

                        //SolidColorBrush color = _isAbove60 && _isRising60 ? colorUp : colorDown;
                        SolidColorBrush color = _isAbove60 ? colorUp : colorDown;

                        //this method basically replicates PanelBase.DrawLineOverlayChart, which is in visible time scale.
                        frame += TimeFrame;

                        DrawLineSegment(start, end, lineWidth, color);
                        count++;
                    }

                }
                void DrawEMA(EMA ema, List<double> items, int lineWidth, SolidColorBrush color)
                {
                    int count = 0;

                    //need the actual start frame in the visible timeframe, which is available from GetFrameCount()
                    int frame = startFrame * TimeFrame;
                    int _numFrames = numFrames;
                    foreach (double val in items) // emaLarge.GetResult(startFrame, numFrames))
                    {
                        if (frame < 2)
                        {
                            continue;
                        }
                        if (count == 0)
                        {
                            start.X = count;
                            start.Y = val;
                        }
                        else
                        {
                            start.X = end.X;
                            start.Y = end.Y;
                        }
                        end.X = count;
                        end.Y = val;

                        frame += TimeFrame;

                        DrawLineSegment(start, end, lineWidth, color);
                        count++;
                    }
                }

                DrawEMAColored(emaUpDown.emaLarge, emaUpDown.emaLarge, itemsLarge, 2);
                DrawEMA(emaUpDown.emaSmall, itemsSmall, 1, Brushes.Black);
                //DrawEMA(emaUpDown.emaLarge, items1, 1, Brushes.Blue);
            }
        }

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
