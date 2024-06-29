using AlgoTrader.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoTrader.Shared
{
    public class PendingTradeRequest
    {
        ///*
        // ExchangeRequest.NEW_ORDER,
        //        (ApiParamNames.SYMBOL, symbolName),
        //        (ApiParamNames.SIDE, ApiParamNames.SELL),
        //        (ApiParamNames.ORDER_TYPE, orderType),
        //        (ApiParamNames.QUANTITY, trade.Quantity),
        //        (ApiParamNames.PRICE, trade.Price),
        //        (ApiParamNames.STOP_PRICE, trade.Price),
        //        (ApiParamNames.TIME_IN_FORCE, "GTC"),
        //        (ApiParamNames.TRADE_ID_NEW, trade.ItemId),
        //        (ApiParamNames.RESPONSE_TYPE, ApiParamNames.FULL));
        // * */
        //public bool inProgress;
        //public int tradeId;
        //public ExchangeRequest exchangeRequestType;
        //public string symbol;
        //public ApiParamNames side;
        //public ApiParamNames orderType;
        //public double quantity;
        //public double price;
        //public double stopPrice;
        //public string timeInForce = "GTC";
        //public string tradeIdNew;
        //public ApiParamNames responseType = ApiParamNames.FULL;

        public TradeEngine tradeEngine;
        public bool inProgress;
        public DateTime timeStamp;
        public TradeAction action;
    }
}
