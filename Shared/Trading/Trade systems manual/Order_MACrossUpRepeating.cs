using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using AlgoTrader.Shared.MovingAverages;
using AlgoTrader.Shared.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace AlgoTrader.Shared
{
    //unused
    public class Order_MACrossUpRepeating : Order_MACrossUp
    {

        #region Init
        public Order_MACrossUpRepeating(string symbolName, int timeInterval, TradeEngine tradeEngine, Chart chart) : base(symbolName, timeInterval, tradeEngine, chart)
        {

        }
        #endregion

        internal bool CanSetRepeatingOrder(Trade closedTrade)
        {
            var frame = chart.GetFrameForTrade(closedTrade);
            return MovingAverage.IsRising(TradeEngine.ema_200, frame, closedTrade.TimeFrame);
        }

        internal Trade SetOrder(int timeFrame, TradePosition tradePosition, TradeType tradeType, string symbolName, ExchangeId exchangeId, DataContext db, bool isBacktest, double price, DateTime timeStamp)
        {
                

            try
            {
                //don't allow more than one order, or closing one could cause another unseen order to be immediately executed
                //however, can set orders on multiple symbols, only one will execute if Constants.ALLOW_MULTIPLE_SYMBOL_TRADES == false
                if (ActiveOrder != null)
                {
                    AlertDialog.Show("Cannot set 2 active trades to run concurrently on same symbol", "Not possible");
                    return null;
                }


                //frameNum = isBacktest ? Chart.GetFrameNumFromLastMousePos() : Chart.GetLastFrameNum();
                var frameNum = Chart.GetFrameForTimestamp(timeStamp);

                double orderPrice = tradeEngine.ema_63.GetResultsFromDictionary(timeFrame)[frameNum];

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
                }

                var ret = new Trade
                {
                    System = isBacktest ? TradeSystem.BACKTEST : TradeSystem.LIVE,
                    GlobalStatus = isBacktest ? TradeGlobalStatus.STOPPED : TradeGlobalStatus.ACTIVE,
                    ItemStatus = isBacktest ? TradeItemStatus.CLOSED : TradeItemStatus.OPEN,
                    Timestamp = timeStamp,
                    TradeId = Trade.GetNewTradeId(),
                    Type = tradeType,
                    Position = tradePosition,
                    Action = TradeAction.ORDER,
                    Price = orderPrice,
                    ActionValue = 0,
                    TimeFrame = TimeFrame,
                    SymbolName = symbolName,
                    ExchangeId = exchangeId,
                    ExecutionSystem = TradeExecutionSystem.MANUAL,
                    ChartLineId = -1,
                    OrderType = OrderType.ENTERED_OFF_EXCHANGE

                };

                db.Trades.Add(ret);
                db.SaveChanges();
                return ret;
            }
#pragma warning disable CS0168
            catch (Exception e)
            {
                return null;
            }
            finally
            {
                MainObj.DrawCurrentDispatcher(db);
            }
        }
        #region Enter trade


        public override bool CanEnterTrade(SymbolDataItem item, Trade order, DataContext db)
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

            var chartLine = db.Lines.Where(x => x.Id == ActiveOrder.ChartLineId).FirstOrDefault();
            if (chartLine != null)
            {
                bool isWithinLine = false;
                (_, isWithinLine) = chartLine.GetPriceAtTime(item.Timestamp, true);
                if (!isWithinLine) //outside of line
                {
                    //LogTrades.WriteLine($"{item.SymbolName} could not enter as entry point outside line, at {DateTime.UtcNow:G}");
                    return false;
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

            if (!tradeEngine.Results[frame - 1].Interrupted)
            {

                if (MovingAverage.IsAbove(tradeEngine.ema_63, TradeEngine.ema_5, frame, order.TimeFrame))
                {
                    if (MovingAverage.IsAbove(tradeEngine.ema_5, tradeEngine.ema_10, frame, order.TimeFrame) && !MovingAverage.IsAbove(tradeEngine.ema_5, tradeEngine.ema_10, frame - 60, order.TimeFrame))
                    {
                        //if (MovingAverage.IsRising(tradeEngine.ema_200, frame, order.TimeFrame))
                        //{
                            return true;
                       // }
                        //else
                        //{
                        //    LogTrades.WriteLine("Entry blocked");
                        //}
                    }
                }
            }

            return false;

        }
        #endregion

    }
}
