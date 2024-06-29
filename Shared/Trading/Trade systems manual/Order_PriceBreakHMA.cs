using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using AlgoTrader.Shared.TradeIndicators;
using System;
using System.Linq;

namespace AlgoTrader.Shared
{
    public class Order_PriceBreakHMA : Order_PriceBreak
    {
        HMA_Indicator hmaIndicator;

        #region Init
        public Order_PriceBreakHMA(string symbolName, int timeInterval, TradeEngine tradeEngine, Chart chart) : base(symbolName, timeInterval, tradeEngine, chart)
        {
            hmaIndicator = MainObj.GetPanelForSymbol(symbolName, typeof(HMA_Indicator)) as HMA_Indicator;
        }
        #endregion

        public override bool CanEnterTrade_synchronised(_Trade order)
        {
            if (!MainObj.BACKTEST)
            {
                if (tradeEngine.dataReady == false)
                {
                    return false;
                }
            }

            //move order to current trade position
            order.Timestamp = tradeEngine.currentDataItem.Timestamp;

            var results = hmaIndicator.indicator.GetResultsFromDictionary(order.TimeFrame);
            if (results != null)
            {
                order.Price = results[tradeEngine.currentFrame];
            }
            tradeEngine.SetUpdateRequest(order);//, TradeUpdateRequest.UPDATE);

            //check for order entry
            (var prevPrice, var currPrice) = GetPrevCurrPrices(order);
  
            return hmaIndicator.CanEnterTrade(order.TimeFrame, currPrice, prevPrice, tradeEngine.currentFrame);
        }
    }
}
