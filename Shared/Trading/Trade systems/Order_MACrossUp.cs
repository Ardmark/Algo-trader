using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using AlgoTrader.Shared.MovingAverages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace AlgoTrader.Shared
{
    public class Order_MACrossUp : Order_PriceBreak
    {
        #region Fields
        private readonly int lineThickness;
        readonly SolidColorBrush colorUp, colorDown;
        #endregion

        #region Init
        public Order_MACrossUp(string symbolName, int timeInterval, TradeEngine tradeEngine, Chart chart) : base(symbolName, timeInterval, tradeEngine, chart)
        {

        }
        #endregion

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
                if (frame % 60 == 0)
                {
                    if (MovingAverage.IsAbove(tradeEngine.ema_5, tradeEngine.ema_10, frame, order.TimeFrame) && !MovingAverage.IsAbove(tradeEngine.ema_5, tradeEngine.ema_10, frame - 60, order.TimeFrame))
                    {
                        //to test if the 200 ema can be used to block entry if it is falling, does not help at all
                        //if (MovingAverage.IsRising(ema_200.ma, frame, 60))
                        //{
                            return true;
                        //}
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
