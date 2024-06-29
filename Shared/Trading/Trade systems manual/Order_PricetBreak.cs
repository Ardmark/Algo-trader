using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;

namespace AlgoTrader.Shared
{
    public class Order_PriceBreak : Order
    {
        #region Init
        public Order_PriceBreak(string symbolName, int timeInterval, TradeEngine tradeEngine, Chart chart) : base(symbolName, timeInterval, tradeEngine, chart)  {  }
        #endregion

        static bool isTiming = false;
        public override bool CanEnterTrade_synchronised(_Trade order)
        {
            if (!MainObj.BACKTEST)
            {
                if (tradeEngine.dataReady == false)
                {
                    return false;
                }
            }

            var lines = tradeEngine.AllLinesForSymbol;

            //get price from line at current timestamp
            var chartLine = lines.GetLineForTrade(order);
            if (chartLine != null)
            {
                //have had one occasion where the line had identical start and end times. for some reason.
                //If this occurs, set minimum line length to 1 hour
                var d = chartLine.EndTimeStamp.Subtract(chartLine.StartTimeStamp).TotalMinutes;
                if (d < TradeEngine.MINIMUM_TRADE_TIME)
                {
                    chartLine.EndTimeStamp = chartLine.StartTimeStamp.AddMinutes(TradeEngine.MINIMUM_TRADE_TIME);
                    tradeEngine.SetUpdateRequest(chartLine);//, TradeUpdateRequest.UPDATE);
                    return false;
                }

                var item = tradeEngine.currentDataItem;

                var currLinePrice = order.Price; //for a market break order

                bool isWithinLine = false;
                (currLinePrice, isWithinLine) = chartLine.GetPriceAtTime(item.Timestamp, true);

                if (!isWithinLine && TradeEngine.AUTO_EXTEND_LINES) //outside of line
                {
                    //if (MainObj.BACKTEST)
                    //{
                    //    return false; //to allow for backtest order lines to be placed, without extension to current time
                    //}
                    //LogTrades.WriteLine($"{item.SymbolName} could not enter as entry point outside line, at {DateTime.UtcNow:G}");
                    //return false;

                    //project line forward
                    // - this will not immediately redraw, as this method only returns entry indicated or not, does not return a redraw flag
                   chartLine.Extend(item.Timestamp, TimeFrame);
                    tradeEngine.SetUpdateRequest(chartLine);//, TradeUpdateRequest.UPDATE);
                }


                (var prevItemPrice, var currItemPrice) = GetPrevCurrPrices(order);
                //Debug.WriteLine($"{currLinePrice}, {currItemPrice}, {prevItemPrice}");  

                if (currItemPrice == -1)
                {
                    return false;
                }

                if (order.Position == TradePosition.LONG
                    ? prevItemPrice <= currLinePrice && currItemPrice > currLinePrice
                    : prevItemPrice >= currLinePrice && currItemPrice < currLinePrice)
                {

                    if (TradeEngine.MAX_FLIP_DIFF_PERCENT > 0)
                    {
                        var maxOrderPrice = order.Position == TradePosition.LONG
                                                        ? currLinePrice * (1 + TradeEngine.MAX_FLIP_DIFF_PERCENT / 100)
                                                        : currLinePrice / (1 + TradeEngine.MAX_FLIP_DIFF_PERCENT / 100);

                        //don't allow entry if a sudden spike take price too far above break price
                        //however, this will often happen with backtests as only 1-min close is available so ignore that
                        if (order.Position == TradePosition.LONG
                                        ? item.Close > maxOrderPrice
                                        : item.Close < maxOrderPrice)
                        {
                            //will continue to test against previous price for the duration of the current timeframe, which would be up to 4 hours
                            // so don't want to continually show price messages, just once for reference in case it does not enter at all
                            if (!isTiming)
                            {
                                var pcOver = (item.Close / currLinePrice - 1) * 100;
                                LogTrades.WriteLine(string.Format("Trade {0}, Id {1} was {2}% above break at entry point, and was not entered at {3}. This message will not show again for {4} minutes", order.SymbolName, order.Id, pcOver.ToString("0.0"), item.Timestamp, order.TimeFrame));
                                isTiming = true;
                                var timer = new System.Timers.Timer(order.TimeFrame * 60000)
                                {
                                    AutoReset = false
                                };
                                timer.Elapsed += (o, e) =>
                                {
                                    isTiming = false;
                                };
                            }
                            return false;
                        }
                    }

                    return true;
                }
            }
            return false;
        }


    }
}
