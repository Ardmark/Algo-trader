using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using AlgoTrader.Shared.TradeIndicators;
using System;
using System.Linq;

namespace AlgoTrader.Shared
{
    public class Order_MACrossUp : Order_PriceBreak
    {
        MACD_Indicator macdIndicator;

        #region Init
        public Order_MACrossUp(string symbolName, int timeInterval, TradeEngine tradeEngine, Chart chart) : base(symbolName, timeInterval, tradeEngine, chart)
        {
            //this assumes a fixed timeframe in the MACD indicator
            macdIndicator = MainObj.GetPanelForSymbol(symbolName, typeof(MACD_Indicator)) as MACD_Indicator;
            TimeFrame = macdIndicator.TimeFrame;
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
            tradeEngine.SetUpdateRequest(order);//, TradeUpdateRequest.UPDATE);

            //check for order entry
            //return true;
            if (macdIndicator.CanEnterTrade(0,0,0,tradeEngine.currentFrame))
            {
                return true;
            }
            return base.CanEnterTrade_synchronised(order);
        }
    }


}
