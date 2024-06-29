using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using Newtonsoft.Json;
using Shared.Trading;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

namespace AlgoTrader.Shared.ExchangeConnection
{
    public partial class ExchangeDataProcessorBinance : ExchangeDataProcessor
    {
        /*
         * Stops and take profits:
         * To run on the exchange, the initial order is placed, then subsequent orders are first cancelled then added again on the exchange.
         * This requires a couple of flags (pendingLiveEntryStop, pendingLiveEntryTP) and the logic to respond to the close order acknowlegements.
         * This has been tested on the exchange simulation and it works. 
         * This also assumes that the exchange stop will execute before the system one, then the system will execute its stop and send a close order
         *  which will be rejected. The system will then synch the pending close against the exchange in the OnQueryOrderDataReceived() event,
         *  which has not been tested at this time.
         *  6/4/21.
         * */
        #region Binance data classes (JSON deserializes into these directly)

        #region Exchange info data
        //ExchangeInfo, global
        public class ExchangeInfoData
        {
            public class RateLimitData
            {
                public string RateLimitType { get; set; }
                public string Interval { get; set; }
                public long IntervalNum { get; set; }
                public long Limit { get; set; }
            }
            public class ExchangeFilterData
            {
                public string FilterType { get; set; }
                public long MaxNumOrders { get; set; }
                public long MaxNumAlgoOrders { get; set; }
                public double MinQty { get; set; }
                public double MaxQty { get; set; }
                public double StepSize { get; set; }
                public double MinNotional { get; set; }
                public bool ApplyToMarket { get; set; }
                public long AvgPriceMins { get; set; }
            }
            public class SymbolData
            {
                public string Symbol { get; set; }
                public string Status { get; set; }
                public string QuoteAsset { get; set; } //this would be BTC for example, in ETHBTC
                public List<string> OrderTypes { get; set; }
                public bool OcoAllowed { get; set; }
                public bool IsSpotTradingAllowed { get; set; }
                public bool IsMarginTradingAllowed { get; set; }
                public List<string> permissions { get; set; }
                public List<ExchangeFilterData> Filters { get; set; }
                #region Unused properties
                //public bool QuoteOrderQtyMarketAllowed { get; set; }
                //public bool IcebergAllowed { get; set; }

                public string BaseAsset { get; set; } //this would be ETH for example in ETHBTC
                public long BaseAssetPrecision { get; set; }
                public long QuotePrecision { get; set; } // will be removed in future api versions (v4+)
                public long QuoteAssetPrecision { get; set; }
                public long BaseCommissionPrecision { get; set; }
                public long QuoteCommissionPrecision { get; set; }
                #endregion
            }

            public string TimeZone { get; set; }
            public long ServerTime { get; set; }
            public List<RateLimitData> RateLimits { get; set; }
            public List<ExchangeFilterData> ExchangeFilters { get; set; }
            public List<SymbolData> Symbols { get; set; }
        }
        //the exchange data, filtered out before passing back
        public class ExchangeInfoDataFiltered
        {
            public long RequestWeightPerMin { get; set; } //the total request weights for all orders must not excede this in a minute
            public long MaxOrdersPer10Secs { get; set; }
            public long MaxOrdersPerDay { get; set; }
            public long MaxOpenAlgoOrders { get; set; } //these are STOP_LOSS, STOP_LOSS_LIMIT, TAKE_PROFIT, and TAKE_PROFIT_LIMIT orders, not market orders

            //all the trading pairs. The booleans are: spot trading allowed, margin trading allowed, oco allowed. There is also iceberg allowed, and some quote precision data available
            //the tuples items are named (string SymbolName, bool SpotAllowed, bool MarginAllowed, bool OcoAllowed)
            public List<(string, bool, bool, bool)> TradingPairs { get; set; }
        }
        #endregion

        #region Quote price data
        public class QuoteData
        {
            public string Symbol { get; set; }
            public double Price { get; set; }
        }

        #endregion

        #region Account data
        //AccountData, used to get balance prior to submitting order
        //this is not symbol-sprcific
        public class AccountData
        {
            public class BalanceData
            {
                public string Asset { get; set; }
                public double Free { get; set; }
                public double Locked { get; set; }
            }
            public bool CanTrade { get; set; }
            public bool MakerCommission { get; set; }
            public List<BalanceData> Balances { get; set; }

            //unused here so not filled
            public bool TakerCommission { get; set; }
            public bool CanWidthdraw { get; set; }
            public bool CanDeposit { get; set; }
            public long UpdateTime { get; set; } //won't convert to DateTime from javascript format

            public string AccountType { get; set; }
            public List<string> Permissions { get; set; }

            public DateTime UpdateTimeAsDateTime
            {
                get
                {
                    return TimeStampFromNodeMS(UpdateTime);
                }
            }
        }
        public class AccountDataFiltered
        {
            public List<AccountData.BalanceData> Balances { get; set; }
        }
        #endregion

        #region Order data
        //the ExchangeData-derived objects are all symbol-specific
        public class ExchangeData
        {
            public string Symbol { get; set; }
            public string ClientOrderId { get; set; }

        }

        public class OrderData : ExchangeData
        {
            public long OrderId { get; set; }
            public long OrderListId { get; set; }

            public decimal Price { get; set; }
            public double OrigQty { get; set; }
            public double ExecutedQty { get; set; }
            public double CummulativeQuoteQty { get; set; }

            //status:
            //Order: NEW, PARTIALLY_FILLED, FILLED, CANCELED, PENDING_CANCEL, REJECTED, EXPIRED (PENDING_CANCEL is currenty Unused)
            //there are some additional enum strings for OCO's
            public string Status { get; set; }

            public string TimeInForce { get; set; } //e.g. GTC
            public string Type { get; set; } //LIMIT, MARKET, STOP_LOSS, STOP_LOSS_LIMIT, TAKE_PROFIT, TAKE_PROFIT_LIMIT, LIMIT_MAKER
            public string Side { get; set; } //BUY or SELL
            public decimal StopPrice { get; set; }
            public double IcebergQty { get; set; }
            public long Time { get; set; }
            public long UpdateTime { get; set; }
            public long TransactTime { get; set; }
            public bool IsWorking { get; set; }
            public double OrigQuoteOrderQty { get; set; }
            public long OrigClientOrderId { get; set; } //this is for a cancel order, not sure if the OrderId prop. gets populated or this one
            public string ListClientOrderId { get; set; } //for stops
            public string AboveClientOrderId { get; set; } //for stops
            public string BelowClientOrderId { get; set; } //for stops

            public DateTime TimeAsDateTime
            {
                get
                {
                    return TimeStampFromNodeMS(Time);
                }
            }
            public DateTime UpdateTimeAsDateTime
            {
                get
                {
                    return TimeStampFromNodeMS(UpdateTime);
                }
            }
            public DateTime TransactTimeAsDateTime
            {
                get
                {
                    return TimeStampFromNodeMS(TransactTime);
                }
            }
        }

        public class NewOrderData : OrderData
        {
            //note, not all OrderData properties are filled for NewOrderData
            // - properties returned are: OrderId, OrderListId, TransactTime, Price, OrigQty, ExecutedQty, CummulativeQuoteQty, Status, TimeInForce, Type, Side

            public class FillsData
            {
                public decimal Price { get; set; }
                public double Qty { get; set; }
                public decimal Commission { get; set; }
                public string CommissionAsset { get; set; }
            }
            public List<FillsData> Fills { get; set; }

            //new order returns a single fill for a market order, but will do average price anyway
            //however, the executed quantity is available in "executedQty" so no need to set that
            internal void CalculateAverageFillPrice()
            {
                var totalQty = Fills.Select(x => x.Qty).Sum();
                Price = (decimal)Fills.Select(x => (double)x.Price * x.Qty / totalQty).Sum();
            }
        }

        public class NewOCOData : OrderData
        {
            //the OrderListID is in the parent object
            //the data contains an orders list as well, just the symbol, orderId and clientOrderId of each of the OrderReports, so not bothering with those
            public class OrderReportsData
            {
                public string Symbol { get; set; }
                public long OrderId { get; set; }
                public string ClientOrderId { get; set; }
                public string Type { get; set; } //STOP_LOSS_LIMIT and LIMIT_MAKER
            }

            public List<OrderReportsData> OrderReports { get; set; }
        }

        public class AllOrderData : ExchangeData
        {
            //presumably this is OK, the order data is the same anyway
            public List<OrderData> OrderDataItems { get; set; }
        }

        public class OpenOrdersData : ExchangeData
        {
            //unimplemented
        } 
        #endregion

        public class MyTradesData : ExchangeData
        {
            //unimplemented
        }

        /*can just use dynamic object and access these properties by name, or a class with specific property definitions */
        internal class BinanceCaptureData
        {
            //there are 3 properties: eventTime; symbol; eventType (of which, 'kline' is only of interest - anything else, this will not be set but only kline is being sent anyway from the connector)
            //here we are adding an EventTimestamp to automatically convert the node format to a DateTime object
            public string Symbol { get; set; }
            public DateTime EventTimestamp { get; set; }
            public Kline Kline { get; set; } //contains startTime,     endTime,     symbol,     interval,     firstTradeId,     lastTradeId,   open,     close,     high,     low,     volume,     trades,     final,     quoteVolume,     volumeActive,     quoteVolumeActive,     ignored,
            public string Message { get; set; }
            public string Error { get; set; }
            public string ExchangeInfo { get; set; }
            public string Account { get; set; }
            public string QuotePrice { get; set; }
            public string QueryOrder { get; set; }
            public string AllOrders { get; set; }
            public string NewOrder{ get; set; }
            public string CancelOrder { get; set; }
            public string OpenOrders { get; set; }
            public string MyTrades { get; set; }
            public string NewOCO { get; set; }

            private long eventTime;
            public long EventTime
            {
                get
                {
                    return eventTime;
                }
                set
                {
                    eventTime = value;
                    EventTimestamp = TimeStampFromNodeMS(value);
                }
            }
        }
        
        internal class Kline
        {
            public DateTime EndTimestamp { get; set; }
            public double Close { get; set; }
            public double Open { get; set; }
            public double High { get; set; }
            public double Low { get; set; }
            public bool Final { get; set; }

            //only interested in the time, close value and whether this is the end of the minute (Final)
            private long endTime;
            public long EndTime
            {
                get
                {
                    return endTime;
                }
                set
                {
                    endTime = value;
                    //note, value here is always at current minute and 59 seconds, so the seconds cannot be used
                    EndTimestamp = TimeStampFromNodeMS(value);
                }
            }
        }

        const long TICKS_TO_1970 = 621355968000000000;
        public static DateTime TimeStampFromNodeMS(long ms)
        {
            //var dt = new DateTime(1970, 1, 1);
            //var ticks = dt.Ticks;
            //node returns dates in milliseconds since jan 1st 1970
            //DateTime uses ticks since an earlier date, and there are 10000 ticks per ms
            return new DateTime(ms * 10000 + TICKS_TO_1970);
        }

        public static long TimeStampToNodeMS(DateTime timeStamp)
        {
            return (timeStamp.Ticks - TICKS_TO_1970) / 10000;

        }
        #endregion

        #region Countdown timer
        DispatcherTimer countdownTimer;
        DateTime lastCountdownTime;
        bool countDownStarted = false;
        #endregion

        #region Fields

        [JsonExtensionData] //or this won't be serialized correctly
        public List<string> AdditionalData { get; set; }

        readonly List<KeyValuePair<ExchangeRequest, EventHandler<ExchangeEventArgs>>> callBacks = new List<KeyValuePair<ExchangeRequest, EventHandler<ExchangeEventArgs>>>();

        #endregion

        #region Init
        public ExchangeDataProcessorBinance() : base(ExchangeId.BINANCE)
        {
            connectorPath = string.Format(@"""{0}""", Path.Combine(projectDirectory, "Binance connectors", "csharpconnector", "connector.js"));    //note the escaped " to allow spaces

            countdownTimer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Normal, (Object sender, EventArgs e) =>
            {
                var countdownSeconds = DateTime.UtcNow.Subtract(lastCountdownTime).TotalSeconds;
                lastCountdownTime = DateTime.UtcNow;
                MainObj.SetCurrentSeconds(countdownSeconds); //not causing exception
                }, Dispatcher.CurrentDispatcher);
            countdownTimer.Stop();
            lastCountdownTime = DateTime.UtcNow;

            symbolNamesToUpdate = SymbolNames.GetSymbolNames().ToList();
        }

        protected override bool OpenConnection()
        {
            var ret = base.OpenConnection();
            if (ret == true)
            {
                //this just pumps a message out to the connector 
                if (process.StandardInput != null)
                {
                    //set up node connector with all the symbol names to retrieve data for
                    AdditionalData = new List<string>();
                    foreach (var symbolName in SymbolNames.GetSymbolNames())
                    {
                        //symbols which don't have a data feed are bracketed, but brackets in the symbol name break the connection process with Binance,
                        // so must exclude those from the request
                        if (!symbolName.Contains("("))
                        {
                            //need a name-value pair, so can just use the symbol name for both
                            AdditionalData.Add(symbolName);
                        }
                    }

                  SendCommandToExchange("SetSymbols", AdditionalData);

                  
                    //var s1 = JsonConvert.SerializeObject(new
                    //{
                    //    Operation = "SetSymbols",
                    //    Data = AdditionalData
                    //});
                    //
                    //process.StandardInput.WriteLineAsync(s1);

                    //var s2 = JsonConvert.SerializeObject(new
                    //{
                    //    Operation = "ExchangeInfo"
                    //});
                    //process.StandardInput.WriteLineAsync(s2);

                    //cannot use Console.WriteLine, as that just writes to the debug window
                    //Console.SetOut(myStreamWriter); - could now use Console.WriteLine instead
                }
            }

            return ret;
        }

        #endregion

        #region Send data

        internal override void AddRequest(ExchangeRequest exchangeCommand, EventHandler<ExchangeEventArgs> eventHandler, params (dynamic, dynamic)[] _data)
        {
            Dictionary<string, string> data = null;
            if (_data != null && _data.Count() > 0)
            {
                data = new Dictionary<string, string>();
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
                        case ApiParamNames.STOP_LOSS:
                            p = "STOP_LOSS";
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

                        case ApiParamNames.ABOVE_TYPE:
                            p = "aboveType";
                            break;
                        case ApiParamNames.ABOVE_PRICE:
                            p = "abovePrice";
                            break;
                        case ApiParamNames.ABOVE_STOP_PRICE:
                            p = "aboveStopPrice";
                            break;
                        case ApiParamNames.ABOVE_TIMEINFORCE:
                            p = "aboveTimeInForce";
                            break;
                        case ApiParamNames.BELOW_TYPE:
                            p = "belowType";
                            break;
                        case ApiParamNames.BELOW_PRICE:
                            p = "belowPrice";
                            break;
                        case ApiParamNames.BELOW_STOP_PRICE:
                            p = "belowStopPrice";
                            break;
                        case ApiParamNames.BELOW_TIMEINFORCE:
                            p = "belowTimeInForce";
                            break;
                        case ApiParamNames.ORIG_CLIENT_ORDER_ID:
                            p = "origClientOrderId";
                            break;


                        default:
                            Logging.LogTrades.WriteLine($"could not map {name} to api parameter string");
                            return;


                    }
                    //data.Add(new KeyValuePair<string, string>(v.Item1 as string, v.Item2 as string));
                    if (v.Item2 != null)
                    {
                        data.Add(p, v.Item2.ToString());
                    }
                    else
                    {
                        Logging.LogTrades.WriteLine($"missing data for ExchangeCommand {exchangeCommand}: {p} was not found");
                    }
                }
            }

            switch (exchangeCommand)
            {
                case ExchangeRequest.EXCHANGE_INFO:
                    //this is symbol-agnostic so the data must be sent back to the TradeEngine which requested it via an event handler
                    //there would in principal be only one, although 2 could try to enter more or less simultaneously in principal
                    if (!callBacks.Where(x => x.Key == exchangeCommand).Any())
                    {
                        callBacks.Add(new KeyValuePair<ExchangeRequest, EventHandler<ExchangeEventArgs>>(exchangeCommand, eventHandler));
                    }
                    SendCommand("ExchangeInfo");
                    break;

                case ExchangeRequest.ACCOUNT:
                    //ditto
                    if (!callBacks.Where(x => x.Key == exchangeCommand).Any())
                    {
                        callBacks.Add(new KeyValuePair<ExchangeRequest, EventHandler<ExchangeEventArgs>>(exchangeCommand, eventHandler));
                    }
                    SendCommand("Account");
                    break;

                case ExchangeRequest.QUOTE_PRICE:   
                    if (!callBacks.Where(x => x.Key == exchangeCommand).Any())
                    {
                        callBacks.Add(new KeyValuePair<ExchangeRequest, EventHandler<ExchangeEventArgs>>(exchangeCommand, eventHandler));
                    }
                    SendCommand("QuotePrice", data);                    
                    break;

                case ExchangeRequest.ALL_ORDERS:
                    //uses symbol name
                    SendCommand("AllOrders", data);

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
                    SendCommand("QueryOrder", data);
                    break;

                case ExchangeRequest.NEW_ORDER:

                    //close - this is a new order
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
                    SendCommand("NewOrder", data);
                    break;

                case ExchangeRequest.NEW_OCO:
                    SendCommand("NewOCO", data);
                    break;


                case ExchangeRequest.OPEN_ORDERS:
                    //this should use a symbol name or is weight 40, so will not by symbol-agnostic
                    SendCommand("OpenOrders", data);
                    break;

                case ExchangeRequest.MY_TRADES:
                    //uses symbol name
                    SendCommand("MyTrades", data);
                    break;

                case ExchangeRequest.CANCEL_ORDER:
                    SendCommand("CancelOrder", data);
                    break;
            }
        }

        void SendCommand(string command, dynamic data = null)
        {
            if (!TradeEngine.USE_SIMULATED_EXCHANGE)
            {
                 SendCommandToExchange(command, data);
            }
            else
            {
                //bypass the exchange for the actual order, no need to place it for live testing, just to get the price, quantity etc.
                SendCommandSimulated(command, data);
            }
        }

        //send command to the connector via StandardInput
        void SendCommandSimulated(string command, dynamic data = null)
        {
            string clientId = string.Empty;
            bool isStopTakeProfit = false;
            decimal price = 0;
            string symbol = string.Empty;
            if (data != null)
            {
                foreach (var v in data)
                {
                    var p = v.Key;
                    switch (p)
                    {
                        case "newClientOrderId":
                        case "origClientOrderId":
                            clientId = v.Value;
                            break;
                        case "type":
                            isStopTakeProfit = v.Value == "STOP_LOSS_LIMIT" || v.Value == "TAKE_PROFIT_LIMIT";
                            break;
                        case "price":
                            double.TryParse(v.Value, out double dprice);
                            price = (decimal)dprice; //decimal won't parse exponents
                            break;
                        case "symbol":
                            symbol = v.Value;
                            break;
                    }
                }
            }
            switch (command)
            {
                case "Account":
                    var timer1 = new Timer(200) { AutoReset = false, Enabled = true };

                    // Hook up the Elapsed event for the timer. 
                    timer1.Elapsed += (object source, ElapsedEventArgs e) =>
                    {

                        var balances = new List<AccountData.BalanceData>();
                       
                            //just return a balance of 1 for each asset
                        foreach (var v in SymbolNames.GetSymbolNames())
                        {
                            var assets = SymbolNames.GetAssetsForSymbol(v);

                            balances.Add(new AccountData.BalanceData
                            {
                                Asset = assets.Item1,
                                Free = 1
                            });
          
                        };
                        if (!balances.Any(x => x.Asset == "BTC"))
                        {
                            //and add a BTC balance for new orders
                            balances.Add(new AccountData.BalanceData
                            {
                                Asset = "BTC",
                                Free = 1
                            });
                        }
                        if (!balances.Any(x => x.Asset == "USDT"))
                        {
                            balances.Add(new AccountData.BalanceData
                            {
                                Asset = "USDT",
                                Free = 1000
                            });
                        }
                        InvokeAccountDataRequestCallbacks(
                                 ExchangeRequest.ACCOUNT,
                                 new ExchangeEventArgs
                                 {
                                     Result = new AccountDataFiltered
                                     {
                                         Balances = balances
                                     }
                                 }
                             );
                    };
                    break;

                case "QuotePrice":
                    var timer2 = new Timer(200) { AutoReset = false, Enabled = true };

                    // Hook up the Elapsed event for the timer. 
                    timer2.Elapsed += (object source, ElapsedEventArgs e) =>
                    {
                        InvokeAccountDataRequestCallbacks(
                                 ExchangeRequest.QUOTE_PRICE,
                                 new ExchangeEventArgs
                                 {
                                     Result = new QuoteData
                                     {
                                         Price = 1, //this is not used anyway
                                         Symbol = symbol
                                     }
                                 }
                             ); 
                    };
                    break;
                case "NewOrder":

                    var timer3 = new Timer(150) { AutoReset = false, Enabled = true };
                    timer3.Elapsed += (object source, ElapsedEventArgs e) =>
                    {
                        var newOrderData = new NewOrderData
                        {
                            Symbol = symbol,
                            Price = price,
                            CummulativeQuoteQty = 1,
                            ExecutedQty = 1,
                            ClientOrderId = clientId,
                            Status = isStopTakeProfit ? "NEW" : "FILLED"
                        };
                        (MainObj.GetTradeEngineForSymbol(symbol) as TradeEngineBinance)?.OnDataReceivedForTrade_synchronised(newOrderData);

                    };
                    break;

                case "CancelOrder":

                    var timer4 = new Timer(100) { AutoReset = false, Enabled = true };
                    timer4.Elapsed += (object source, ElapsedEventArgs e) =>
                    {
                        var OrderData = new OrderData
                        {
                            Symbol = symbol,
                            Status = "CANCELED",
                            ClientOrderId = clientId

                        };
                        (MainObj.GetTradeEngineForSymbol(symbol) as TradeEngineBinance)?.OnDataReceivedForTrade_synchronised(OrderData);
                    };
                    break;

                case "ExchangeInfo":
                    var timer5 = new System.Timers.Timer(100) { AutoReset = false, Enabled = true };

                    // Hook up the Elapsed event for the timer. 
                    timer5.Elapsed += (object source, ElapsedEventArgs e) =>
                    {
                        ((MainObj.GetTradeEngineForSymbol(symbol) as TradeEngineBinance) as TradeEngineBinance)?.OnDataReceivedForTrade_synchronised(new ExchangeInfoData());

                        InvokeAccountDataRequestCallbacks(
                                 ExchangeRequest.EXCHANGE_INFO,
                                 new ExchangeEventArgs
                                 {
                                     Result = new ExchangeInfoData()
                                 }
                             ); 
                    };
                    break;
                case "AllOrders":
                    break;
                case "QueryOrder":
                    break;
                case "MyTrades":
                    break;

            }
        }

        //send command to the connector via StandardInput
        public override void SendCommandToExchange(string command, dynamic data = null)
        {
            string s;
            if (data != null)
            {
                s = JsonConvert.SerializeObject(new
                {
                    Operation = command,
                    Data = data //will be serialized correctly but needs to be passed in as a public property with the [JsonExtensionData] attribute
                });
            }
            else
            {
                s = JsonConvert.SerializeObject(new
                {
                    Operation = command
                });
            }
            try
            {
                process.StandardInput.WriteLine(s);
            }
#pragma warning disable CS0168
            catch (Exception e)
            {

            }
        }
        #endregion
       
        #region Receive data
        List<DateTime> errors = new List<DateTime>();
        protected override void CaptureOutput(object sender, DataReceivedEventArgs e)
        {
            void HandleError()
            {
                const int MAX_ERRORS_PER_MINUTE = 3;
                if (errors.Count >= MAX_ERRORS_PER_MINUTE - 1) //2 errors so this one will make 3 in the list
                {
                    LogConnection.WriteLine($"{errors.Count + 1} errors in one minute, restarting connection...");
                    errors.Clear();
                    OpenConnection();
                }
                else
                {
                    errors.Add(DateTime.UtcNow);
                }
            }
           
            if (!MainObj.initComplete)
            {
                lastProcessTimestamp = DateTime.UtcNow; //so no reconnection
                return;
            }

            if (e == null || e.Data == null) //seems to happen after breaking for some time
            {
                return;
            }       

            try
            {          
                //Send data using SendCommand, process in commands.js (REST methods for orders, etc.)
                
                var captureData = JsonConvert.DeserializeObject<BinanceCaptureData>(e.Data);
                //if (captureData.Message != null) Debug.WriteLine(captureData.Message);

                #region Error


                if (errors.Any())
                {
                    var prevMinute = DateTime.UtcNow.AddMinutes(-1);
                    errors.RemoveAll((DateTime dt) => { return dt < prevMinute; });
                }
                if (captureData.Error != null)
                {
                    LogConnection.WriteLine($"Binance capture data error at {DateTime.UtcNow:G}: {captureData.Message}");

                    HandleError();
   
                    return;
                }
                #endregion

                //from REST

                #region Message
                if (captureData.Message != null)
                {
                    LogConnection.WriteLine($"Message: {captureData.Message}");
                    
                    return;
                }
                #endregion

                //for all trade data, data is projected onto a new object (defined in Trade Engine), to reduce complexity of data presented to that class,
                // and to make all Trade Engine data exchange-agnostic

                if (captureData.Kline == null)
                {
                    #region Exchange info
                    if (captureData.ExchangeInfo != null)
                    {
                        try
                        {
                            var exchangeInfoData = JsonConvert.DeserializeObject<ExchangeInfoData>(captureData.ExchangeInfo);

                            //note, no trading pairs have STOP_LOSS or TAKE_PROFIT order types available, only the _LIMIT types
                            //var tradingPairs = exchangeInfoData.Symbols.Where(x =>
                            //    x.Status == "TRADING" &&
                            //    x.OrderTypes.Contains("MARKET") &&
                            //    x.OrderTypes.Contains("STOP_LOSS_LIMIT") &&
                            //    x.OrderTypes.Contains("TAKE_PROFIT_LIMIT")
                            //    )
                            //    .Select(x => (SymbolName: x.Symbol, SpotTradingAllowed: x.IsSpotTradingAllowed, MarginTradingAllowed: x.IsMarginTradingAllowed, OcoAllowed: x.OcoAllowed)).ToList();
                            //
                            //TradeEngine.ExchangeInfoDataReceived(new ExchangeInfoDataFiltered
                            //{
                            //    //there are only 3 items sent anyway, but will do full query in case any others get added
                            //    RequestWeightPerMin = exchangeInfoData.RateLimits.Where(x => x.RateLimitType == "REQUEST_WEIGHT" && x.Interval == "MINUTE" && x.IntervalNum == 1).Select(x => x.Limit).FirstOrDefault(),
                            //    MaxOrdersPerDay = exchangeInfoData.RateLimits.Where(x => x.RateLimitType == "ORDERS" && x.Interval == "DAY" && x.IntervalNum == 1).Select(x => x.Limit).FirstOrDefault(),
                            //    MaxOrdersPer10Secs = exchangeInfoData.RateLimits.Where(x => x.RateLimitType == "ORDERS" && x.Interval == "SECOND" && x.IntervalNum == 10).Select(x => x.Limit).FirstOrDefault(),
                            //
                            //    //also there is a max. orders, but only interested in algo orders here
                            //    MaxOpenAlgoOrders = exchangeInfoData.ExchangeFilters.Where(x => x.FilterType == "EXCHANGE_MAX_ALGO_ORDERS").Select(x => x.MaxNumAlgoOrders).FirstOrDefault(),
                            //    TradingPairs = tradingPairs
                            //
                            //});

                            InvokeAccountDataRequestCallbacks(
                                ExchangeRequest.EXCHANGE_INFO,
                                new ExchangeEventArgs
                                {
                                    Result = exchangeInfoData
                                }
                            );
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                    #endregion

                    #region Account info
                    if (captureData.Account != null)
                    {
                        try
                        {
                            var accountData = JsonConvert.DeserializeObject<AccountData>(captureData.Account);

                            //symbol agnostic, so get callback for the correct trade
                            InvokeAccountDataRequestCallbacks(
                                ExchangeRequest.ACCOUNT,
                                new ExchangeEventArgs
                                {
                                    Result = new AccountDataFiltered
                                    {
                                        Balances = accountData.Balances
                                    }
                                }
                            );
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                    #endregion

                    #region Quote price info
                    if (captureData.QuotePrice != null)
                    {
                        try
                        {
                            var quotePriceData = JsonConvert.DeserializeObject<QuoteData>(captureData.QuotePrice);

                            //symbol agnostic, so get callback for the correct trade
                            InvokeAccountDataRequestCallbacks(
                                ExchangeRequest.QUOTE_PRICE,
                                new ExchangeEventArgs
                                {
                                    Result = quotePriceData
                                }
                            );
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                    #endregion

                    #region New order info
                    if (captureData.NewOrder != null)
                    {
                        try
                        {
                            var newOrderData = JsonConvert.DeserializeObject<NewOrderData>(captureData.NewOrder);
                            newOrderData.CalculateAverageFillPrice();
                            (MainObj.GetTradeEngineForSymbol(newOrderData.Symbol) as TradeEngineBinance).OnDataReceivedForTrade_synchronised(newOrderData);
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                    #endregion

                    #region New OCO info
                    if (captureData.NewOCO != null)
                    {
                        try
                        {
                            var newOCOData = JsonConvert.DeserializeObject<NewOCOData>(captureData.NewOCO);
                            (MainObj.GetTradeEngineForSymbol(newOCOData.Symbol) as TradeEngineBinance).OnDataReceivedForTrade_synchronised(newOCOData);
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                    #endregion

                    #region Cancel OCO info (only an OCO will be cancelled, as we are not placing limit orders on the exchange
                    if (captureData.CancelOrder != null)
                    {
                        try
                        {
                            var cancelOrderData = JsonConvert.DeserializeObject<NewOCOData>(captureData.CancelOrder);
                            var symbol = cancelOrderData.OrderReports[0].Symbol; //since both orders will have the same symbol
                            (MainObj.GetTradeEngineForSymbol(symbol) as TradeEngineBinance).OnDataReceivedForTrade_synchronised(cancelOrderData);
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                    #endregion

                    #region Query order info
                    if (captureData.QueryOrder != null)
                    {
                        try
                        {
                            var orderData = JsonConvert.DeserializeObject<OrderData>(captureData.NewOrder);
                            (MainObj.GetTradeEngineForSymbol(captureData.Symbol) as TradeEngineBinance).OnDataReceivedForTrade_synchronised(orderData);
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                    #endregion

                }

                //From websocket (user data stream)
                #region Candle data
                if (captureData.Kline != null)
                {
                    //Process data here 

                    lastProcessTimestamp = DateTime.UtcNow;

                    lock (thisLock) //could use SemaphoreSlim class instead
                    {
                        //this is way more simple than Bitmex, where the data is streamed in blocks with repeated entries each time, so that needs a lot of sorting to get the latest data
                        //here, we just need to save data when Final == true, otherwise just pass the close value back to the app
                        try
                        {
                            var currentSymbolDataItem = GetCurrentSymbolDataItem(captureData.Symbol);

                            //need a new object or the db will hold a reference and that data will be changed next update
                            var item = new _SymbolDataItem()
                            {
                                Close = captureData.Kline.Close,
                                Open = captureData.Kline.Open,
                                High = captureData.Kline.High,
                                Low = captureData.Kline.Low,
                                Timestamp = captureData.Kline.EndTimestamp,
                                //SymbolName = captureData.Symbol,
                                //ExchangeId = exchangeId
                            };

                            if (!captureData.Kline.Final) //maybe 10 per minute of these
                            {
                                currentSymbolDataItem.Update(captureData.Symbol, item, false);
                                MainObj.ItemUpdated(currentSymbolDataItem, captureData.Symbol);
                                if (!countDownStarted)
                                {
                                    //countdownSeconds = captureData.Kline.EndTimestamp.Second;
                                    countDownStarted = true;
                                    countdownTimer.Start();
                                }
                            }
                            else
                            {
                                //countdownSeconds = 60;

                                currentSymbolDataItem.Update(captureData.Symbol, item, true);

                                var itemsToSave = GetItemsToSave(item, captureData.Symbol);
                                if (itemsToSave.Count > 0)
                                {
                                    if (MainObj.WRITE_TO_DB)
                                    {
                                        bool block = itemsToSave.Count > 1;
                                        if (block)
                                        {
                                            LogConnection.WriteLine($"Adding block of {itemsToSave.Count} data items for {captureData.Symbol}...");
                                        }

                                        //db.Configuration.AutoDetectChangesEnabled = false; //speeds up entity framework but not used here

                                        //INSANELY SLOW USING EF


                                        //cannot mix EF with SQL, won't write out sometimes
                                        //also, no need to set identity to ON that just throws an error with SQL query

                                        //using (DataContext db = new DataContext())
                                        //{
                                        //db.Database.ExecuteSqlCommand("SET IDENTITY_INSERT[dbo].[SymbolDataItems] ON");

                                        //foreach (var p in itemsToSave)
                                        //{
                                        //    lastProcessTimestamp = DateTime.UtcNow; //stops reconnection during long saves, probably not an issue with this but EF methods just took hours
                                        //    p.ExecuteInsertCommand(db);
                                        //}

                                        //db.Database.ExecuteSqlCommand("SET IDENTITY_INSERT[dbo].[SymbolDataItems] OFF");
                                        var value = System.Configuration.ConfigurationManager.ConnectionStrings["DataContext"];
                                        using (SqlConnection conn = new SqlConnection(value.ToString()))
                                        {
                                            conn.Open();

                                            foreach (var p in itemsToSave)
                                            {
                                                lastProcessTimestamp = DateTime.UtcNow; //stops reconnection during long saves, probably not an issue with this but EF methods just took hours
                                                p.ExecuteInsertCommand(captureData.Symbol, conn);

                                            }
                                        }
                                    }
                                    //var s = string.Empty;
                                    //if interruption is v. short, may not do a block interpolate, so need to remove symbol from update list anyway
                                    if (symbolNamesToUpdate.Count > 0 && symbolNamesToUpdate.Contains(captureData.Symbol))
                                    {
                                        symbolNamesToUpdate.Remove(captureData.Symbol);

                                        if (symbolNamesToUpdate.Count > 0)
                                        {
                                            string s = string.Join(", ", symbolNamesToUpdate);
                                            LogConnection.WriteLine($"Symbols remaining to connect: {s}");
                                        }
                                        else
                                        {
                                            LogConnection.WriteLine("All symbols connected.");
                                        }
                                    }
                                }
                            }

                            lastCaptureTimestamp = captureData.Kline.EndTimestamp;
                        }
                        catch (Exception ex)
                        {
                            //can get a 400 error if timestamps get out of synch, so easiest thing is just to reconnect
                            LogConnection.WriteLine($"CaptureData.Kline exception at {DateTime.UtcNow:G}: {ex}");
                            HandleError();
                        }
                    }
                } 
#endregion
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogConnection.WriteLineTimestamp($"Binance capture output exception at {DateTime.UtcNow:G}: " + ex.ToString());
            }
        }
        #endregion

        #region Invoked callbacks

        //invoke all callbacks which match the key for the exchange request, and remove those from the callbacks list
        //this is for symbol-agnostic data (account info) which must go just to the trade engine which is waiting for it
        //so there will only be one key used anyway
        private void InvokeAccountDataRequestCallbacks(ExchangeRequest exchangeRequest, ExchangeEventArgs eventArgs)
        {
            try
            {
                var _callBacks = (new KeyValuePair<ExchangeRequest, EventHandler<ExchangeEventArgs>>[callBacks.Count]);
                callBacks.CopyTo(_callBacks); //in simulation, this will be called again immediately thus modifying the collection and throwing exception
                callBacks.RemoveAll((KeyValuePair<ExchangeRequest, EventHandler<ExchangeEventArgs>> kvp) =>
                {
                    return kvp.Key == exchangeRequest;
                });                
                
                var items = _callBacks.Where(x => x.Key == exchangeRequest);
                foreach (var item in items)
                {
                    item.Value?.Invoke(this, eventArgs);
                }

            }
            catch { }

        }

        #endregion


        
    }
}
