using AlgoTrader.Shared.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace AlgoTrader.Shared.ExchangeConnection
{
    public partial class ExchangeDataProcessorBinance : ExchangeDataProcessor
    {

        internal void AddRequest(ExchangeRequest exchangeCommand, EventHandler<ExchangeEventArgs> eventHandler, params (dynamic, dynamic)[] _data)
        {
            Dictionary<string, string> data = null; 
            if (_data.Count() > 0)
            {
                data = new Dictionary<string,string>();
                foreach (var v in _data)
                {
                    //map the api request string name to the actual string required by the Binance API
                    string p;
                    ApiParamNames name = (ApiParamNames)v.Item1;
                    switch (name)
                    {
                        case ApiParamNames.SYMBOL:
                            p = "symbol";
                            break;                       
                        case ApiParamNames.TRADE_ID_ORIGINAL:
                            p = "origClientOrderId";
                            break;
                        case ApiParamNames.TRADE_ID_NEW:
                            p = "newClientOrderId";
                            break;
                        case ApiParamNames.EXCHANGE_ORDER_ID:
                            p = "orderId";
                            break;
                        case ApiParamNames.SIDE:
                            p = "side";
                            break;
                        case ApiParamNames.ORDER_TYPE:
                            p = "type";
                            break;
                        case ApiParamNames.QUOTE_ORDER_QTY:
                            p = "quoteOrderQty";
                            break;
                        case ApiParamNames.QUANTITY:
                            p = "quantity";
                            break;
                        case ApiParamNames.ICEBERG_QUANTITY:
                            p = "icebergQty";
                            break;
                        case ApiParamNames.PRICE:
                            p = "price";
                            break;
                        case ApiParamNames.STOP_PRICE:
                            p = "stopPrice";
                            break;
                        case ApiParamNames.RESPONSE_TYPE:
                            p = "newOrderRespType";
                            break;
                        case ApiParamNames.TIME_IN_FORCE:
                            p = "timeInForce";
                            break;
                        case ApiParamNames.TIMEOUT_MS:
                            p = "recvWindow";
                            break;

                        case ApiParamNames.BUY:
                            p = "BUY";
                            break;
                        case ApiParamNames.SELL:
                            p = "SELL";
                            break;
                        case ApiParamNames.LIMIT:
                            p = "LIMIT";
                            break;
                        case ApiParamNames.MARKET:
                            p = "MARKET";
                            break;
                        case ApiParamNames.STOP_LOSS_LIMIT:
                            p = "STOP_LOSS_LIMIT";
                            break;
                        case ApiParamNames.TAKE_PROFIT_LIMIT:
                            p = "TAKE_PROFIT_LIMIT";
                            break;
                        case ApiParamNames.ACK:
                            p = "ACK";
                            break;
                        case ApiParamNames.RESULT:
                            p = "RESULT";
                            break;
                        case ApiParamNames.FULL:
                            p = "FULL";
                            break;

                        default:
                            Logging.LogTrades.WriteLine($"could not map {name} to api parameter string");
                            return;


                    }
                    //data.Add(new KeyValuePair<string, string>(v.Item1 as string, v.Item2 as string));
                    data.Add(p, v.Item2.ToString());
                }
            }


            switch (exchangeCommand)
            {
                case ExchangeRequest.EXCHANGE_INFO:
                    SendCommandSimulated("ExchangeInfo");
                    break;

                case ExchangeRequest.ACCOUNT:
                    //this is symbol-agnostic so the data must be sent back to the TradeEngine which requested it via an event handler
                    //there would in principal be only one, although 2 could try to enter more or less simultaneously in principal
                    if (!callBacks.Where(x => x.Key == exchangeCommand).Any())
                    {
                        callBacks.Add(new KeyValuePair<ExchangeRequest, EventHandler<ExchangeEventArgs>>(exchangeCommand, eventHandler));
                        SendCommandSimulated("Account");
                    }
                    break;

                case ExchangeRequest.ALL_ORDERS:
                    //uses symbol name
                    SendCommandSimulated("AllOrders", data);
                     
                     //Required parameters:
                     //symbol
                     
                    break;

                case ExchangeRequest.QUERY_ORDER:
                   //
                   // Required parameters:
                   // symbol
                   // 
                   // Optional parameters:
                   // orderId (long)
                   // origClientOrderId (string)
                   // 
                    SendCommandSimulated("QueryOrder", data);
                    break;

                case ExchangeRequest.NEW_ORDER__ENTRY:
                case ExchangeRequest.NEW_ORDER__STOP:
                case ExchangeRequest.NEW_ORDER__TAKEPROFIT:

                    //these are all new orders, a cancel is a new sell market order
                    //
                    // Required parameters:
                    // symbol
                    // side
                    // type
                    // timestamp
                    // quoteOrderQty (for market orders, can send quantity instead)
                    // quantity  (for stop loss)
                    // stopPrice (for stop loss)
                    // 
                    // Optional parameters:
                    // newClientOrderId (string) (must be unique amongst open orders. If already in use by an open order, new order will be rejected. If not supplied, will be generated)
                    // 
                    SendCommandSimulated("NewOrder", data);
                    break;

                case ExchangeRequest.OPEN_ORDERS:
                    //this should use a symbol name or is weight 40, so will not by symbol-agnostic
                    SendCommandSimulated("OpenOrders", data);
                    break;


                case ExchangeRequest.MY_TRADES:
                    //uses symbol name
                    SendCommandSimulated("MyTrades", data);
                    break;

                case ExchangeRequest.CANCEL_ORDER:
                case ExchangeRequest.NEW_ORDER_ON_CLOSE__STOP:
                case ExchangeRequest.NEW_ORDER_ON_CLOSE__TAKEPROFIT:
                    SendCommandSimulated("CancelOrder", data);
                    break;
            }


        }

        //send command to the connector via StandardInput
        public void SendCommandSimulated(string command, dynamic data = null)
        {
           switch (command)
            {
                case "Account":
                    var timer1 = new System.Timers.Timer(100)  {  AutoReset = false,  Enabled = true  };

                    // Hook up the Elapsed event for the timer. 
                    timer1.Elapsed += (object source, ElapsedEventArgs e) =>
                    {
                        InvokeAccountDataRequestCallbacks(
                                 ExchangeRequest.ACCOUNT,
                                 new ExchangeEventArgs
                                 {
                                     Result = new AccountDataFiltered
                                     {
                                         BalanceBTC = 1
                                     }
                                 }
                             );
                    };
                    break;

                case "NewOrder":

                    string clientId = string.Empty;
                    bool isStop = false;
                    foreach (var v in data)
                    {
                        var p = v.Key;
                        if (p == "newClientOrderId")
                        {
                            clientId = v.Value;
                        }
                        if (p == "type")
                        {
                            isStop = v.Value == "STOP_LOSS_LIMIT";
                        }
                    }
                    var timer2 = new System.Timers.Timer(100) { AutoReset = false, Enabled = true };
                    timer2.Elapsed += (object source, ElapsedEventArgs e) =>
                    {
                        var newOrderData = new NewOrderData
                        {
                            Symbol = "BNBBTC",
                            Price = 0.004907M,
                            CummulativeQuoteQty = 1,
                            ClientOrderId = clientId,
                            Status = isStop ? "NEW" : "FILLED"
                        };

                        MainObj.GetTradeEngineForSymbol(newOrderData.Symbol)?.OnExchangeDataReceived(newOrderData);

                    };
                    break;

                case "CancelOrder":
                    var timer3 = new System.Timers.Timer(100) { AutoReset = false, Enabled = true };
                    timer3.Elapsed += (object source, ElapsedEventArgs e) =>
                    {

                    };
                    break;



                case "ExchangeInfo":
                    break;
                case "AllOrders":
                    break;
                case "QueryOrder":
                    break;
                case "MyTrades":
                    break;
           
            }
        }


    }
}
