using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using AlgoTrader.Shared.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Timers;
using System.Threading.Tasks;
using static AlgoTrader.Shared.ExchangeConnection.ExchangeDataProcessorBinance;
using AlgoTrader.Shared;
using System.Diagnostics.Tracing;

namespace Shared.Trading
{
    internal class TradeEngineBinance : TradeEngine
    {
        class ExchangeRequestData
        {
            public int tradeId;
            public ExchangeRequest exchangeRequestType;
            public string symbol;
            public ApiParamNames side;
            public ApiParamNames orderType;
            public double quantity;
            public double price;
            public double stopPrice;
            public string tradeIdNew;
        }

        #region Script names
        //script names - rather than enums - because these are stored in the db as strings so are easier to use when debugging
        const string QUERY_ORDER_SCRIPT = "Query Order"; //to update a trade with live server data
        const string NEW_ORDER_SCRIPT = "New Order";
        const string CLOSE_ORDER_SCRIPT = "Close Order";
        const string CANCEL_ORDER_SCRIPT = "Cancel Order"; //to cancel a stop, tp or floor (or OCO)

        #region Unimplemented
        const string NEW_ORDER_ON_CLOSE_STOP_SCRIPT = ""; //see unused method
        const string NEW_ORDER_ON_CLOSE_TAKEPROFIT_SCRIPT = "";
        const string NEW_ORDER_TAKEPROFIT_SCRIPT = "";
        const string NEW_ORDER_STOP_SCRIPT = "";
        #endregion

        #endregion

        public event EventHandler<ExchangeEventArgs> ExchangeDataReceivedEventHandler;
        public static event EventHandler<ExchangeEventArgs> GetAccountBalancesEventHandler; //non-trade event handler (e.g. to display balances)
        public event EventHandler<ExchangeEventArgs> GetExchangeInfoEventHandler; //unimplemented


        DateTime lastExchangeInfoRequest = new DateTime();
        public ExchangeInfoData exchangeInfo = null;
        AccountDataFiltered accountData;


       
        static TradeEngineBinance()
        {
            GetAccountBalancesEventHandler += OnAccountBalancesReceived;
        }

        public TradeEngineBinance(string symbolName, int timeInterval) : base(symbolName, timeInterval)
        {

        }

        protected override void Binance_InitEvents()
        {
            ExchangeDataReceivedEventHandler += (object sender, ExchangeEventArgs eventArgs) => { OnDataReceivedForTrade_synchronised(eventArgs?.Result); };
            // ExchangeDataReceivedEventHandler += OnDataReceived;


            // if (exchangeInfo == null)
            // {
            //     StartExchangeRequest( ExchangeRequest.EXCHANGE_INFO, ExchangeInfoEventHandler, (ApiParamNames.SYMBOL, symbolName));
            // }

            /* 
            CheckExchangeOrdersStatus();

            //every 1 minute, check server to synch orders in case anything has been done on the exchange 
            //dispatcher timer is on UI thread, but is safe to use for data-bound variables
            DispatcherTimer allOrdersTimer = new DispatcherTimer();

            allOrdersTimer.Tick += new EventHandler((source, eventArgs) =>
            {
                CheckExchangeOrdersStatus();
            });

            allOrdersTimer.Interval = new TimeSpan(0, 1, 0); //just check every 10 mins, this is not critical
            allOrdersTimer.Start();
            */
        }
  
        #region Process account data
        // ---------------------------------------------------------------------------------------------------------------- //
        //this is the event handler call and delegate for display of balances, nothing to do with trades
        // ---------------------------------------------------------------------------------------------------------------- //

        internal override void GetAccountBalances()
        {
            MainObj.exchangeConnector.AddRequest(ExchangeRequest.ACCOUNT, GetAccountBalancesEventHandler, null);
        }

        internal static void OnAccountBalancesReceived(object sender, ExchangeEventArgs eventArgs)
        {
            OnDataReceivedForAccountBalances_synchronised(eventArgs?.Result);
        }

        internal static void OnDataReceivedForAccountBalances_synchronised(object result)
        {
            (double value, string msg) GetValueForSymbol(string baseAsset, string quoteAsset)
            {
                var symbol = baseAsset + quoteAsset;

                var tradeEngine = MainObj.GetTradeEngineForSymbol(symbol);
                if (tradeEngine == null)
                {
                    return (0, $"{symbol} not trading on system");
                }
                else
                {
                    if (tradeEngine.currentDataItem == null)
                    {
                        return (0, $"Waiting for connection on {symbol}");
                    }
                }
                return (tradeEngine.currentDataItem.Close, null);
            }

            if (result is AccountDataFiltered) //which it should be
            {
                var accountData = result as AccountDataFiltered;

                double totalEquity = 0;
                string errorMsg = string.Empty;
                string resultMsg = string.Empty;

                //e.g. BTCUSDT - BTC is the base asset, USDT is the quote asset
                //so for these calculations, BTC will always be the quote asset, the base asset will be ADA, IOTA etc
                // and there is a separate fiat asset
                //const string QUOTE_ASSET = "BTC";

                //can ignore locked amount, this is just for trades placed on the exchange which have not yet entered

                foreach (var v in accountData.Balances.Where(x => x.Free > 0 || x.Locked > 0))
                {
                    if (SymbolNames.SYMBOLS_TO_EXCLUDE.Contains(v.Asset))
                    {
                        continue;
                    }

                    //get the quote asset: probably USDT, may be BTC
                    var quoteAsset = SymbolNames.GetQuoteAssetForBaseAsset(v.Asset);


                    if (v.Asset == SymbolNames.FIAT_ASSET || v.Asset == "BUSD" || v.Asset == "USDC")
                    {
                        //e.g. USDT
                        totalEquity += v.Free + v.Locked;
                    }
                    else if (quoteAsset == SymbolNames.FIAT_ASSET || quoteAsset == "BUSD" || quoteAsset == "USDC")
                    {
                        //e.g ADA, where ADAUSDT is available
                        (var lastSymbolValue, var s1) = GetValueForSymbol(v.Asset, quoteAsset);
                        totalEquity += (v.Free + v.Locked) * lastSymbolValue;
                    }
                    else if (quoteAsset == null)
                    {
                        errorMsg += $"\n{v.Asset} not on system, not included in total equity";
                    }
                    else
                    {
                        //e.g LOOM, where only LOOMBTC is available
                        //so get BTCUSDT
                        (var lastQuoteValue, var s1) = GetValueForSymbol(quoteAsset, SymbolNames.FIAT_ASSET);
                        if (s1 != null)
                        {
                            errorMsg += $"\n{s1}";
                        }
                        else
                        {
                            (var lastSymbolValue, _) = GetValueForSymbol(v.Asset, quoteAsset);
                            totalEquity += (v.Free + v.Locked) * lastSymbolValue * lastQuoteValue;
                        }
                    }
                }



                foreach (var v in accountData.Balances
                            .Where(x => x.Free > 0 || x.Locked > 0)
                            .OrderBy(x => x.Free)
                            .ThenBy(x => x.Locked))
                {
                    if (SymbolNames.SYMBOLS_TO_EXCLUDE.Contains(v.Asset))
                    {
                        continue;
                    }
                    double value = 0;
                    bool noSymbol = false;
                    //get the quote asset: probably USDT, may be BTC
                    var quoteAsset = SymbolNames.GetQuoteAssetForBaseAsset(v.Asset);

                    if (v.Asset == SymbolNames.FIAT_ASSET || v.Asset == "BUSD" || v.Asset == "USDC")
                    {
                        //e.g. USDT
                        value = v.Free + v.Locked;
                    }
                    else if (quoteAsset == SymbolNames.FIAT_ASSET || quoteAsset == "BUSD" || quoteAsset == "USDC")
                    {
                        //e.g ADA, where ADAUSDT is available
                        (var lastSymbolValue, var s1) = GetValueForSymbol(v.Asset, quoteAsset);
                        if (s1 != null)
                        {
                            noSymbol = true;
                        }
                        else
                        {
                            value = (v.Free + v.Locked) * lastSymbolValue;
                        }
                    }
                    else
                    {
                        //e.g LOOM, where only LOOMBTC is available
                        //so get BTCUSDT
                        (var lastQuoteValue, var s1) = GetValueForSymbol(quoteAsset, SymbolNames.FIAT_ASSET);
                        if (s1 != null)
                        {
                            noSymbol = true;
                        }
                        else
                        {
                            (var lastSymbolValue, _) = GetValueForSymbol(v.Asset, quoteAsset);
                            value = (v.Free + v.Locked) * lastSymbolValue * lastQuoteValue;
                        }
                    }

                    var pc = value / totalEquity * 100;

                    var free = string.Empty;
                    if (v.Free > 0 && v.Locked == 0)
                    {
                        free = "100";
                    }
                    else if (v.Free == 0 && v.Locked > 0)
                    {
                        free = "0";
                    }
                    else
                    {
                        free = ((v.Free - v.Locked) / v.Free * 100).ToString("0.00");
                    }
                    free += "% free";
                    if (noSymbol)
                    {
                        resultMsg += $"{v.Asset}:\tno symbol\t\t{free}";
                    }
                    else
                    {
                        resultMsg += $"{v.Asset}:\t{pc.ToString("0.0")}%\t${value.ToString("0.00")}\t\t{free}";
                    }
                    resultMsg += "\n";
                }

                resultMsg += $"\n------------------------------------\nTotal equity:\t${totalEquity.ToString("0.00")}\n------------------------------------\n";

                var btcEngine = MainObj.GetTradeEngineForSymbol("BTCUSDT");
                if (btcEngine != null && btcEngine.currentDataItem != null)
                {
                    resultMsg += $"\nLast BTC price:\t${btcEngine.currentDataItem.Close}";
                }

                //foreach (var quoteAsset in SymbolNamesBinance.QUOTE_ASSETS)
                //{
                //    var tradeEngine = MainObj.GetTradeEngineForSymbol(quoteAsset);
                //    if (tradeEngine != null && tradeEngine.currentDataItem != null)
                //    {
                //        resultMsg += $"Last traded price:\n{quoteAsset}:\t${tradeEngine.currentDataItem.Close}";
                //    }
                //}


                resultMsg += $"{errorMsg}";

                AlertDialog.Show(resultMsg, "Balances");
            }
        }
        #endregion

        #region Get entry count  

        internal static int NumLiveEntriesForQuoteAsset(DataContext db, _Trade tradeToExclude)
        {
            return db.Trades.Count(x =>
                                        x.Id != tradeToExclude.Id && //not really necessary, since the trade to exclude would have an exchange method script anyway
                                        x.Action == TradeAction.ENTRY &&

                                        x.Type != TradeType.STUB_ORDER && //this is only on Fiat, so would not be an issue in this context anyway but doesn't hurt to leave it in for future reference
                                        (
                                            (x.ItemStatus == TradeItemStatus.OPEN && x.GlobalStatus == TradeGlobalStatus.ACTIVE) &&
                                            x.ExchangeMethodScript == null
                                        )
                                    );

        }
        #endregion

        protected override void Binance_CancelStopTakeProfit(_Trade trade)
        {
            trade.ExchangeMethodScript = CANCEL_ORDER_SCRIPT;
        }

        protected override void Binance_NewOrder(_Trade trade)
        {
            trade.ExchangeMethodScript = NEW_ORDER_SCRIPT;
        }

        protected override void StartEntryOrClose()
        {
            void _StartExchangeRequest(ExchangeRequest request)
            {
                try
                {
                    if (DataContext != null)
                    {
                        DataContext.SaveChanges(); //because system is still executing trade so won't save for a while if something hangs the db
                    }
                }
                catch (Exception e)
                {
                }

                switch (request)
                {
                    case ExchangeRequest.EXCHANGE_INFO:
                        if (exchangeInfo == null) //get the data, but only do this once, thereafter will assume the data is always OK
                        {
                            MainObj.exchangeConnector.AddRequest(request, ExchangeDataReceivedEventHandler, null);
                        }
                        break;
                    case ExchangeRequest.ACCOUNT:
                        MainObj.exchangeConnector.AddRequest(request, ExchangeDataReceivedEventHandler, null);
                        break;
                }
            }
            accountData = null;

            if (DateTime.UtcNow.Subtract(lastExchangeInfoRequest).TotalDays > 1)
            {
                exchangeInfo = null; //fetch a new data set, once a day should be fine to keep up to date with any changes
                lastExchangeInfoRequest = DateTime.UtcNow;
            }

            _StartExchangeRequest(ExchangeRequest.ACCOUNT);
            if (exchangeInfo == null)
            {
                //can have this as a static field, and only request it once. Otherwise, refresh every time a trade executes
                _StartExchangeRequest(ExchangeRequest.EXCHANGE_INFO);// sending symbol name just validates it's presence, it still returns the lot, (ApiParamNames.SYMBOL, symbolName));
            }
        }

        internal void OnDataReceivedForTrade_synchronised(object result)
        {
            //bool _IsQuoteAssetTrading(string quoteAssetSymbol)
            //{
            //    //get any active trades, open or closed
            //    var trades = DataContext.Trades.Where(x =>
            //                                                x.SymbolName == quoteAssetSymbol &&
            //                                                x.ExchangeMethodScript == null
            //                                                ).ToList();
            //    bool ret = trades.Any(x => x.GlobalStatus == TradeGlobalStatus.ACTIVE && x.Action == TradeAction.ENTRY);
            //    return ret;
            //}

            bool _HasExcededMaxConcurrentTrades(int numTrades)
            {
                if (numTrades >= TradeEngine.MAX_CONCURRENT_TRADES)
                {
                    //this should not happen, but just in case...
                    KillEntryOrCloseOrder();

                    MainObj.DrawCurrentDispatcher();
                    LogTrades.WriteLine($"Could not enter trade as there are already {numTrades} entries running");
                    return true;
                }
                return false;
            }



            //this method is called during trade execution, the first steps are always to obtain exchange and account data
            // - exchange data may be persistant from a previous call

            void _RunExchangeTradeRequest(ExchangeRequestData pendingRequest)
            {
                if (!USE_SIMULATED_EXCHANGE)
                {
                    MainObj.exchangeConnector.AddRequest(
                        pendingRequest.exchangeRequestType,
                        ExchangeDataReceivedEventHandler,
                        (ApiParamNames.SYMBOL, pendingRequest.symbol),
                        (ApiParamNames.SIDE, pendingRequest.side),
                        (ApiParamNames.ORDER_TYPE, pendingRequest.orderType),
                        (ApiParamNames.QUANTITY, pendingRequest.quantity),
                        (ApiParamNames.TRADE_ID_NEW, pendingRequest.tradeIdNew),
                        (ApiParamNames.RESPONSE_TYPE, ApiParamNames.FULL)
                    //(ApiParamNames.TIME_IN_FORCE,   "GTC") - default anyway
                        );

                }
                else
                {
                    MainObj.exchangeConnector.AddRequest(
                        pendingRequest.exchangeRequestType,
                        ExchangeDataReceivedEventHandler,
                        (ApiParamNames.SYMBOL, pendingRequest.symbol),
                        (ApiParamNames.SIDE, pendingRequest.side),
                        (ApiParamNames.ORDER_TYPE, pendingRequest.orderType),
                        (ApiParamNames.QUANTITY, pendingRequest.quantity),
                        (ApiParamNames.TRADE_ID_NEW, pendingRequest.tradeIdNew),
                        (ApiParamNames.RESPONSE_TYPE, ApiParamNames.FULL),
                        //(ApiParamNames.TIME_IN_FORCE, "GTC"),
                        (ApiParamNames.PRICE, currentDataItem.Close) //add in price so this can be returned in the trade data
                        );
                }
            }

            InitForDataContext(() =>
            {

                if (result is ExchangeInfoData)
                {
                    exchangeInfo = result as ExchangeInfoData;

                    ////this is just to output the entire list of tradeable symbols
                    //foreach (var symbol in exchangeInfo.Symbols.OrderBy(x => x.QuoteAsset))
                    //{
                    //    Debug.WriteLine(symbol.Symbol);
                    //}
                }
                else if (result is AccountDataFiltered)
                {
                    accountData = result as AccountDataFiltered;
                }

                //result may be OrderData, OpenOrdersData, QuoteData, NewOrderData, MyTradesData, AllOrderData
                // - these will be processed by the appropriate script section
                //note, NewOrderData is sent as OrderData, because only the Price is needed here, not the fills list etc.

                if (exchangeInfo == null || accountData == null)
                {
                    return; //still waiting for these to be returned
                }

                #region Stub trade (now unused)
                _Trade AddStubTrade(string _quoteAssetSymbol)
                {
                    _Trade entry = null;
                    var tradeEngine = MainObj.GetTradeEngineForSymbol(_quoteAssetSymbol) as TradeEngineBinance;

                    //just in case, remove any pending stub trades
                    var stubTrades = DataContext.Trades.Where(x => x.SymbolName == _quoteAssetSymbol && x.Type == TradeType.STUB_ORDER).ToList();
                    foreach (var tr in stubTrades)
                    {
                        SetUpdateRequest(tr, TradeUpdateRequest.FORCE_DELETE);
                    }

                    if (tradeEngine != null)
                    {
                        var order = new _Trade
                        {
                            Timestamp = DateTime.UtcNow,
                            TradeReference = _Trade.GetNewTradeId(),
                            Type = TradeType.STUB_ORDER,
                            Position = Entry.Position,
                            Action = TradeAction.ORDER,
                            SymbolName = _quoteAssetSymbol,
                            ExchangeId = exchangeId,
                            ItemStatus = TradeItemStatus.CLOSED,
                            StubCallbackTradeReference = Entry.TradeReference,
                        };

                        SetUpdateRequest(order, TradeUpdateRequest.ADD);

                        entry = order.Clone();
                        entry.Action = TradeAction.ENTRY;
                        entry.ExchangeMethodScript = Entry.ExchangeMethodScript;
                        entry.ExchangeMethodIndex = 1; //jump to generating the order
                        entry.ItemStatus = TradeItemStatus.OPEN;
                        entry.ItemId = Guid.NewGuid().ToString();

                        SetUpdateRequest(entry, TradeUpdateRequest.ADD);

                        //save the trade reference of the stub order, for timeout removal, since it is the base trade (this) which will timeout
                        Entry.StubCallbackTradeReference = entry.TradeReference;

                        //these need to be set for the script to execute in the target trade engine, and they are of course still valid
                        tradeEngine.exchangeInfo = exchangeInfo;
                        tradeEngine.accountData = accountData;

                    }
                    LogTrades.WriteLine("Setting stub trade on system");
                    return entry;
                }
                #endregion

                //swap out for stub trades if any, this is important if there is an interrupted trade on quote symbol (like BTCUSDT) - don't want to pick that one up in a QTB trade
                Order = OrderOrStub;
                Entry = EntryOrStub;

                if (Entry != null)
                {
                    //var isQuoteAsset = SymbolNamesBinance.QUOTE_ASSETS.Contains(Entry.SymbolName);

                    switch (Entry.ExchangeMethodScript)
                    {
                        #region New order
                        case NEW_ORDER_SCRIPT:
                            /*
                            * process is 
                            * 1) get the account data, for the amount of BTC available
                            * 2) get the price of all assets, and use account data to calculate a purchase amount
                            * */
                            switch (Entry.ExchangeMethodIndex)
                            {
                                case 0:
                                    #region Get quote price

                                    //either buying a percentage of the quote asset, or all of it if this is a quote-to-base asset purchase
                                    //so if this is a regular buy, it will be the percentage based on the number of concurrent trades
                                    //but if it is a QTB purchase, then it differs:
                                    // - the quote asset will be the percentage purchase, the base asset will then be 100%


                                    //determine if this is a QTB purchase:
                                    // - a running trade on fiat indicates a buy, so quote asset (ie. BTC) is available
                                    // - otherwise, all trades are closed, in which case base has sold out to fiat, so only fiat available
                                    // - if no trades present at all, this indicates only fiat available

                                    var isFiat = SymbolNames.GetQuoteAssetForSymbol(Entry.SymbolName) == SymbolNames.FIAT_ASSET;// string.Concat(SymbolNamesBinance.GetQuoteAssetForSymbol(Entry.SymbolName), SymbolNamesBinance.FIAT_ASSET); //e.g. USDT
                                    if (!isFiat && !ALLOW_ALT_TRADING)
                                    {
                                        LogTrades.WriteLine($"ALT trading is disabled, {symbolName} not entered, exiting OnExchangeDataReceived");
                                        KillEntryOrCloseOrder();
                                        return;
                                    }
                                    bool isQuoteToBaseTrade = !isFiat;

                                    if (isQuoteToBaseTrade)
                                    {
                                        //e.g. 'ADABTC'
                                        //quote asset is not trading, so it is assumed that there is no BTC purchased (for example, could be other pairs)

                                        //need to purchase the correct amount of BTC first
                                        //this will require setting a new order here, for the quote asset, which will then be picked up
                                        // when the trade data is received
                                        //Once this happens, the next section will fire with the completion data, at which point the
                                        // buy order for the base asset can be generated
                                        //The stub trade for the quote asset purchase will then be deleted
                                        //Once the base asset buy order completes, the trade will then be finished

                                        //create a stub order for the quote asset symbol, this will then be picked up when the exchange returns the price
                                        var quoteAssetSymbol = string.Concat(SymbolNames.GetQuoteAssetForSymbol(Entry.SymbolName), SymbolNames.FIAT_ASSET); //e.g. USDT

                                        AddStubTrade(quoteAssetSymbol);
                                        //MainObj.GetTradeEngineForSymbol(quoteAssetSymbol)?._StartExchangeRequestDirect(ExchangeRequest.QUOTE_PRICE, (ApiParamNames.SYMBOL, quoteAssetSymbol));
                                        MainObj.exchangeConnector.AddRequest(ExchangeRequest.QUOTE_PRICE, ExchangeDataReceivedEventHandler, (ApiParamNames.SYMBOL, quoteAssetSymbol));

                                        #region unused
                                        //var order = new Trade
                                        //{
                                        //    Timestamp = DateTime.UtcNow,
                                        //    TradeReference = Trade.GetNewTradeId(),
                                        //    Type = TradeType.STUB_ORDER,
                                        //    Position = Entry.Position,
                                        //    Action = TradeAction.ORDER,
                                        //    SymbolName = quoteAssetSymbol,
                                        //    ExchangeId = exchangeId,
                                        //    ItemStatus = TradeItemStatus.CLOSED,
                                        //    StubCallbackTradeReference = Entry.TradeReference,
                                        //};

                                        //SetUpdateRequest(order, TradeUpdateRequest.ADD);

                                        //var entry = order.Clone();
                                        //entry.Action = TradeAction.ENTRY;
                                        //entry.ExchangeMethodScript = Entry.ExchangeMethodScript;
                                        //entry.ExchangeMethodIndex = 1; //jump to generating the order
                                        //entry.ItemStatus = TradeItemStatus.OPEN;
                                        //entry.ItemId = Guid.NewGuid().ToString();

                                        //SetUpdateRequest(entry, TradeUpdateRequest.ADD);

                                        ////DataContext.SaveChanges();
                                        ////get the id
                                        ////entry = DataContext.Trades.FirstOrDefault(x => x.Action == TradeAction.ENTRY && x.TradeReference == entry.TradeReference);

                                        ////save the trade reference of the stub order, for timeout removal, since it is the base trade (this) which will timeout
                                        //Entry.StubCallbackTradeReference = entry.TradeReference;

                                        //var tradeEngine = MainObj.GetTradeEngineForSymbol(quoteAssetSymbol);
                                        //if (tradeEngine != null)
                                        //{                   
                                        //    //these need to be set for the script to execute in the target trade engine, and they are of course still valid
                                        //    tradeEngine.exchangeInfo = exchangeInfo;
                                        //    tradeEngine.accountData = accountData;
                                        //    tradeEngine.StartExchangeRequestDirect(ExchangeRequest.QUOTE_PRICE, (ApiParamNames.SYMBOL, quoteAssetSymbol));
                                        //} 
                                        #endregion
                                    }
                                    else
                                    {
                                        //quote asset is trading, or this is the quote asset, so this is a regular buy order
                                        //_StartExchangeRequestDirect(ExchangeRequest.QUOTE_PRICE, (ApiParamNames.SYMBOL, symbolName));
                                        MainObj.exchangeConnector.AddRequest(ExchangeRequest.QUOTE_PRICE, ExchangeDataReceivedEventHandler, (ApiParamNames.SYMBOL, symbolName));

                                    }

                                    Entry.IsQuoteToBasePurchase = isQuoteToBaseTrade;
                                    SetUpdateRequest(Entry, TradeUpdateRequest.UPDATE);

                                    #region Place order directly using QUOTE_ORDER_QTY (unused)
                                    //---------------------------------------------------------------------------------------------------------------------------
                                    //Since the step size limits the amount which can be sold using QUANTITY, i.e. since the amount to be sold must be specified,
                                    // rather than the amount to be received, we cannot have fractional purchases greater than the step size or the exchange will
                                    // reject the trade with a 400 error code. Purchasing with QUOTE_ORDER_QTY, i.e. specifying the amount to spend, results in these
                                    // fractional amounts. 
                                    //E.g. for bnb: if the purchase amount ended up as 0.0195, the sale would only be 0.01 so almost half would be locked up and presumably
                                    // could only be converted to bnb as a small balance (obviously for other symbols) until bnb was > 0.01 then that could be sold on the
                                    // exchange web site.
                                    //Because of this, purchases are only made using QUANTITY, not QUOTE_ORDER_QTY.
                                    //
                                    //---------------------------------------------------------------------------------------------------------------------------
                                    //
                                    //Note on precision for QUOTE_ORDER_QTY (this is a little undefined):
                                    //It seems that there is a max. precision of 8 digits for placing orders using BTC.
                                    //While this is the quote asset precision value for ALL symbols (from exchange info), there is no reference to step size for placing quote orders
                                    // (i.e. QUOTE_ORDER_QTY) anywhere,
                                    // and the base asset precision value (also from exchange info) means nothing for QUANTITY orders which must have dec. places set according to step size.
                                    //So rather than assume this to be correct, and truncate to 8 digits before placing a QUOTE_ORDER_QTY order,
                                    // it may be better to use a QUANTITY order because the correct precision here is obtained from the step size, in the exchange info data
                                    //Therefore there is an extra step in placing an order, which is to get a quote price, so any issues and this can be used instead.
                                    //
                                    //---------------------------------------------------------------------------------------------------------------------------


                                    //(var baseAsset1, var quoteAsset1) = SymbolNamesBinance.GetAssetsForSymbol(symbolName);
                                    //var assetBalance = accountData.Balances.Where(x => x.Asset == quoteAsset1).Select(x => x.Free).FirstOrDefault();
                                    //var amount = assetBalance;// * 0.1;
                                    //long quoteAssetPrecision = 8;
                                    //try
                                    //{
                                    //    quoteAssetPrecision = exchangeInfo.Symbols.Where(x => x.Symbol == symbolName).Select(x => x.QuoteAssetPrecision).First();
                                    //}
                                    //catch { }

                                    //double precision = Math.Pow(10, -quoteAssetPrecision);
                                    //amount = (int)(amount / precision) * precision; //may result in very small remainder
                                    //amount = Math.Round(amount, (int)quoteAssetPrecision); //with very small remainder, this won't round up so OK to do it like this

                                    //if (amount > 0)
                                    //{
                                    //    LogTrades.WriteLine($"Buying {amount} of {baseAsset1}");
                                    //    LogTrades.WriteLine("Sending order to exchange");
                                    //    if (!USE_SIMULATED_EXCHANGE)
                                    //    {
                                    //        StartExchangeRequest(
                                    //            ExchangeRequest.NEW_ORDER__ENTRY,
                                    //            (ApiParamNames.SYMBOL, symbolName),
                                    //            (ApiParamNames.SIDE, ApiParamNames.BUY),
                                    //            (ApiParamNames.ORDER_TYPE, ApiParamNames.MARKET),
                                    //            (ApiParamNames.QUOTE_ORDER_QTY, amount), //can also specify the amount to be purchased in QUANTITY instead
                                    //            (ApiParamNames.TRADE_ID_NEW, trade.ItemId),
                                    //            (ApiParamNames.RESPONSE_TYPE, ApiParamNames.FULL) //this is the default for market orders, but must be specified for stops and tp orders
                                    //    );
                                    //    }
                                    //    else
                                    //    {
                                    //        StartExchangeRequest(
                                    //            ExchangeRequest.NEW_ORDER__ENTRY,
                                    //            (ApiParamNames.SYMBOL, symbolName),
                                    //            (ApiParamNames.SIDE, ApiParamNames.BUY),
                                    //            (ApiParamNames.ORDER_TYPE, ApiParamNames.MARKET),
                                    //            (ApiParamNames.QUOTE_ORDER_QTY, amount), //can also specify the amount to be purchased in QUANTITY instead
                                    //            (ApiParamNames.TRADE_ID_NEW, trade.ItemId),
                                    //            (ApiParamNames.RESPONSE_TYPE, ApiParamNames.FULL), //this is the default for market orders, but must be specified for stops and tp orders
                                    //            (ApiParamNames.PRICE, currentDataItem != null ? currentDataItem.Close : trade.Price)
                                    //        );
                                    //    }
                                    //}
                                    //break;
                                    // 
                                    #endregion

                                    #endregion
                                    break;

                                case 1:
                                    #region Generate Entry order
                                    var quoteData = result as QuoteData;
                                    if (quoteData == null)
                                    {
                                        LogTrades.WriteLine($"Quote data was null in NEW_ORDER_SCRIPT for {symbolName}, exiting OnExchangeDataReceived");
                                        KillEntryOrCloseOrder();
                                        return;
                                    }

                                    /*
                                     *  From the API docs:
                                        base asset refers to the asset that is the quantity of a symbol. For the symbol BTCUSDT, BTC would be the base asset.
                                        quote asset refers to the asset that is the price of a symbol. For the symbol BTCUSDT, USDT would be the quote asset.
                                    */

                                    //if this is a QTB purchase, the symbol from the quote data will not match the entry symbol,
                                    //and this is the symbol to purchase
                                    (var baseAsset, var quoteAsset) = SymbolNames.GetAssetsForSymbol(quoteData.Symbol);

                                    if (quoteAsset == null)
                                    {
                                        LogTrades.WriteLine($"Quote asset was NULL for {quoteData.Symbol}, exiting OnExchangeDataReceived");
                                        KillEntryOrCloseOrder();
                                        return;
                                    }
                                    var assetBalance = accountData.Balances.Where(x => x.Asset == quoteAsset).Select(x => x.Free).FirstOrDefault();

                                    //the symbol price is the ratio between the base and quote assets
                                    var symbolPrice = quoteData.Price;

                                    if (symbolPrice > 0 && TradeEngine.MAX_CONCURRENT_TRADES > 0)
                                    {
                                        var numLiveQuoteAssetEntries = 0;
                                        var isStubTrade = Entry.StubCallbackTradeReference != null;

                                        var displaySymbolName = Entry.SymbolName;
                                        if (isStubTrade)
                                        {
                                            //both the base and quote asset trades will have a stub callback reference. However, when the Quote asset trade has finished,
                                            // the stub trade will be deleted so the stub symbol name will be null, thus there will be a count of zero trades in the second phase, resulting in a 100% purchase
                                            var stubSymbolName = DataContext.Trades.Where(x => x.TradeReference == Entry.StubCallbackTradeReference).Select(x => x.SymbolName).FirstOrDefault();
                                            displaySymbolName = stubSymbolName;
                                            numLiveQuoteAssetEntries = stubSymbolName != null
                                                                        ? NumLiveEntriesForQuoteAsset(DataContext, Entry)//, SymbolNamesBinance.GetQuoteAssetForSymbol(stubSymbolName))
                                                                        : 0;
                                        }
                                        else
                                        {
                                            numLiveQuoteAssetEntries = NumLiveEntriesForQuoteAsset(DataContext, Entry);//, quoteAsset);
                                        }

                                        if (_HasExcededMaxConcurrentTrades(numLiveQuoteAssetEntries))
                                        {
                                            LogTrades.WriteLine($"Maximum number of concurrent trades exceded for {displaySymbolName}, not completing trade (Get Quote Price).");
                                            return;
                                        }

                                        double tradeDivisor = 0;
                                        double tradeDivisorToDisplay = 0;

                                        //the fractional trade amount is the amount of e.g. ADABTC which must be purchased relative to the available equity
                                        //this will be 0.25, 0.33, 0.5, 1 for a max. of 4 trades
                                        double baseAssetTradeAmount = 1 / (double)(TradeEngine.MAX_CONCURRENT_TRADES - numLiveQuoteAssetEntries);

                                        if (!Entry.IsQuoteToBasePurchase)
                                        {
                                            //stub trade or fiat trade will still trade the same amount of fiat
                                            tradeDivisor = baseAssetTradeAmount;
                                            tradeDivisorToDisplay = tradeDivisor;
                                        }
                                        else
                                        {
                                            //second phase of QTB purchase, buy 100% (fractional amount of quote asset already purchased)
                                            tradeDivisor = 1;
                                            tradeDivisorToDisplay = tradeDivisor;
                                        }


                                        LogTrades.WriteLine($"Amount of quote asset to be used: {(int)(tradeDivisorToDisplay * 100)}% of {quoteAsset} (adjusted for price variations)");

                                        var coinBalance = assetBalance / symbolPrice;

                                        var balanceToTrade = coinBalance * tradeDivisor;

                                        //it is possible that the asset price will rise slightly during the order execution
                                        //this will only affect the final trade, as there will not be enough quote asset to fill the order.
                                        //Easiest way to avoid this is to leave 0.5% quote asset, this will aggregate up to 2% for the final trade.
                                        //So this would always leave a small floating balance of the quote asset (i.e. BTC) but would sell out 98%
                                        balanceToTrade *= 0.995;

                                        (var amount, var stepSize) = GetCorrectPrecisionForTrade(symbolName, balanceToTrade);

                                        if (amount > 0)
                                        {
                                            LogTrades.WriteLine($"Buying {amount} of {baseAsset}");

                                            _RunExchangeTradeRequest(new ExchangeRequestData
                                            {
                                                tradeId = Entry.Id,
                                                exchangeRequestType = ExchangeRequest.NEW_ORDER,
                                                symbol = symbolName,
                                                side = ApiParamNames.BUY,
                                                orderType = ApiParamNames.MARKET,
                                                quantity = amount,
                                                tradeIdNew = Entry.ItemId
                                            });
                                        }
                                        else
                                        {
                                            AlertDialog.Show($"Quote asset balance was zero so could not enter trade for {symbolName}");
                                        }
                                    }
                                    #endregion
                                    break;

                                case 2:
                                    #region Set order on system
                                    //this will be hit on confirmation of order

                                    var dataEntry = result as OrderData;
                                    if (dataEntry == null)
                                    {
                                        LogTrades.WriteLine($"Order data was null for {symbolName}, exiting OnExchangeDataReceived");
                                        KillEntryOrCloseOrder();
                                        return;
                                    }

                                    if (Entry.ItemId != dataEntry.ClientOrderId)
                                    {
                                        AlertDialog.Show($"Mismatched ItemId and ClientOrderId for {symbolName} in NEW_ORDER_SCRIPT. ItemId has been changed to {dataEntry.ClientOrderId}.");
                                        Entry.ItemId = dataEntry.ClientOrderId;
                                        SetUpdateRequest(Entry, TradeUpdateRequest.UPDATE);
                                    }

                                    switch (dataEntry.Status)
                                    {
                                        case "FILLED":

                                            LogTrades.WriteLine($"Order FILLED for {Entry.SymbolName}");
                                            if (dataEntry.Price == 0 && !USE_SIMULATED_EXCHANGE)
                                            {
                                                LogTrades.WriteLine($"--Price received was 0 for {Entry.SymbolName}--");
                                            }

                                            if (Entry.Type == TradeType.STUB_ORDER)
                                            {
                                                //the executed quantity now is saved out to the account balance, so this is used in the next QUOTE_PRICE
                                                // section, to calculate the buy amount. So there is no need to run another balance request

                                                LogTrades.WriteLine($"Executed quantity was {dataEntry.ExecutedQty} for {Entry.SymbolName}");

                                                //here, the base asset is e.g. BTC (from BTCUSDT)
                                                var baseAssetTraded = SymbolNames.GetBaseAssetForSymbol(Entry.SymbolName);
                                                var balance = accountData.Balances.FirstOrDefault(x => x.Asset == baseAssetTraded);
                                                balance.Free = dataEntry.ExecutedQty;

                                                //remove the stub orders
                                                LogTrades.WriteLine("Removing stub trade from system");
                                                SetUpdateRequest(Order, TradeUpdateRequest.FORCE_DELETE); //bypass the delete checks
                                                SetUpdateRequest(Entry, TradeUpdateRequest.FORCE_DELETE);

                                                var stubSymbolName = DataContext.Trades.Where(x => x.TradeReference == Entry.StubCallbackTradeReference).Select(x => x.SymbolName).FirstOrDefault();
                                                var _tradeEngine = MainObj.GetTradeEngineForSymbol(stubSymbolName);
                                                LogTrades.WriteLine($"Starting buy order for {stubSymbolName}");
                                                //_tradeEngine._StartExchangeRequestDirect(ExchangeRequest.QUOTE_PRICE, (ApiParamNames.SYMBOL, stubSymbolName));
                                                MainObj.exchangeConnector.AddRequest(ExchangeRequest.QUOTE_PRICE, ExchangeDataReceivedEventHandler, (ApiParamNames.SYMBOL, stubSymbolName));

                                            }
                                            else
                                            {
                                                //this will be either the second phase from a QTB buy, or the first in a regular buy
                                                // - enter the trade on the system
                                                EnterTradeOnSystem_synchronised(dataEntry.Price, dataEntry.ExecutedQty, TradeExchangeStatus.ENTERED_ON_EXCHANGE);
                                                EndEntryOrCloseScript();
                                            }
                                            break;
                                        case "CANCELED":
                                        case "REJECTED":
                                        case "EXPIRED":
                                            LogTrades.WriteLine($"Order was rejected for {Entry.SymbolName} as {dataEntry.Status} at {DateTime.UtcNow}");
                                            KillEntryOrCloseOrder();
                                            break;
                                    }

                                    #endregion

                                    tradeStatusChanged = true;
                                    break;
                            }
                            break;
                        #endregion

                        #region Close order
                        case CLOSE_ORDER_SCRIPT:

                            if (Close == null)
                            {
                                LogTrades.WriteLine($"No Close set in CLOSE_ORDER_SCRIPT for {symbolName}, cannot close trade, exiting OnExchangeDataReceived");
                                KillEntryOrCloseOrder(true);
                                return;
                            }

                            void CloseOrder(string closeSymbolName, _Trade close, bool executeImmediate = false)
                            {
                                LogTrades.WriteLine($"Closing order for {closeSymbolName}");
                                var baseAsset = SymbolNames.GetBaseAssetForSymbol(closeSymbolName);
                                if (baseAsset != null)
                                {
                                    var coinBalance = accountData.Balances.Where(x => x.Asset == baseAsset).Select(x => x.Free).FirstOrDefault();
                                    if (executeImmediate)
                                    {
                                        LogTrades.WriteLine($"Using balance of {coinBalance} from quote asset sell {baseAsset}");
                                    }
                                    else
                                    {
                                        LogTrades.WriteLine($"Using received balance of {coinBalance} {baseAsset}");
                                    }

                                    //retain some BNB to reduce trading fees
                                    if (baseAsset == "BNB" && RETAINED_BNB_PERCENTAGE > 0)
                                    {
                                        coinBalance *= (1 - RETAINED_BNB_PERCENTAGE / 100);
                                        LogTrades.WriteLine($"Retaining {RETAINED_BNB_PERCENTAGE}% of BNB");
                                    }

                                    //LogTrades.WriteLine("+++Start dump of account balances+++");
                                    //foreach (var balance in accountData.Balances)
                                    //{
                                    //    LogTrades.WriteLine($"Symbol: {balance.Asset}, Free: {balance.Free}, Locked: {balance.Locked}");
                                    //}
                                    //LogTrades.WriteLine("+++End dump of account balances+++");

                                    (var amount1, var stepSize) = GetCorrectPrecisionForTrade(closeSymbolName, coinBalance);

                                    if (amount1 > 0)
                                    {
                                        close.Quantity = amount1;

                                        LogTrades.WriteLine($"Placing sell order of {coinBalance}, adjusted for step size, at {DateTime.Now}");
                                        SetUpdateRequest(close, TradeUpdateRequest.UPDATE);

                                        //this is a QTB close, so don't want to run any other pending order but the close to fiat one
                                        _RunExchangeTradeRequest(new ExchangeRequestData
                                        {
                                            tradeId = close.Id,
                                            exchangeRequestType = ExchangeRequest.NEW_ORDER,
                                            symbol = closeSymbolName,
                                            side = ApiParamNames.SELL,
                                            orderType = ApiParamNames.MARKET,
                                            quantity = amount1,
                                            tradeIdNew = close.ItemId,
                                        });
                                    }
                                    else
                                    {
                                        AlertDialog.Show($"Quote asset balance was zero so could not close trade for {symbolName}");
                                    }
                                }
                            }

                            switch (Entry.ExchangeMethodIndex)
                            {
                                case 0:
                                    #region Generate Close order

                                    //close order, so just sell out 100%
                                    //a QUANTITY order specifies the amount of base asset to buy or sell, so can go directly from here

                                    CloseOrder(symbolName, Close);
                                    #endregion
                                    break;

                                case 1:
                                    #region Set order on system

                                    LogTrades.WriteLine($"Received close balance at {DateTime.Now}");
                                    //if status is 'NEW' then order is accepted but not yet filled
                                    //status could be 'PARTIALLY_FILLED' (limit orders only??)
                                    var dataClose = result as NewOrderData;
                                    if (dataClose == null)
                                    {
                                        LogTrades.WriteLine($"Order data was null in CLOSE_ORDER_SCRIPT for {symbolName}, exiting OnExchangeDataReceived");
                                        KillEntryOrCloseOrder(true);
                                        return;
                                    }
                                    if (Close.ItemId != dataClose.ClientOrderId)
                                    {
                                        AlertDialog.Show($"Mismatched ItemId and ClientOrderId for {symbolName} in CLOSE_ORDER_SCRIPT. ItemId has been changed to {dataClose.ClientOrderId}");
                                        Close.ItemId = dataClose.ClientOrderId;
                                    }
                                    switch (dataClose.Status)
                                    {
                                        case "FILLED":

                                            //if this is a normal sell, i.e. a sell to quote asset, regardless of whether there is a following sell to
                                            // fiat, then procede. If this is the second phase of a QTB trade, then this is the filled fiat order, so
                                            // nothing else to be done except clean up

                                            var isFiat = SymbolNames.GetQuoteAssetForSymbol(Entry.SymbolName) == SymbolNames.FIAT_ASSET;// string.Concat(SymbolNamesBinance.GetQuoteAssetForSymbol(Entry.SymbolName), SymbolNamesBinance.FIAT_ASSET); //e.g. USDT

                                            var quoteAssetSymbol = string.Concat(SymbolNames.GetQuoteAssetForSymbol(Entry.SymbolName), SymbolNames.FIAT_ASSET); //e.g. USDT
                                            bool isBaseToQuoteTrade = !isFiat;

                                            if (Close.Type != TradeType.STUB_ORDER)
                                            {
                                                //if this should also sell the quote asset, do it here

                                                LogTrades.WriteLine($"Close order filled, closing trade on system");


                                                if (isBaseToQuoteTrade)
                                                {
                                                    LogTrades.WriteLine($"Requesting balances...");
                                                    //Get the balance after the sell to quote asset
                                                    Close.Price = (double)dataClose.Price; //need to set this now as it is the actual value from balances, not the system close value

                                                    StartEntryOrClose();

                                                    #region Sell to fiat by calculating the sell amount from last received price (unused)
                                                    /*
                                                    CloseTradeOnSystem_synchronised(dataClose.Price);
                                                    //so need to now sell the quote asset to fiat
                                                    //here, the quote asset is e.g. BTC (from ADABTC)
                                                    LogTrades.WriteLine($"Selling quote asset...");
                                                    var quoteAssetTraded = SymbolNamesBinance.GetQuoteAssetForSymbol(Entry.SymbolName);
                                                    var balance = accountData.Balances.FirstOrDefault(x => x.Asset == quoteAssetTraded);

                                                    //this is wrong, the executed quantity will be for base asset, the balance will be for quote asset

                                                    //now need to set a close order on the quote asset
                                                    //balance.Free = dataClose.ExecutedQty;

                                                    //get last traded price for quote asset
                                                    var lastBasePrice = MainObj.GetTradeEngineForSymbol(Entry.SymbolName).currentDataItem.Close;

                                                    //get the amount of quote asset to sell.
                                                    //e.g. if KMD was trading at 0.000012 BTC, and the executed quantity was 210,
                                                    // the amount of BTC to now sell would be 0.000252
                                                    var quoteAssetAmt = dataClose.ExecutedQty * lastBasePrice;

                                                    //since we have not requested the new balances from the exchange, need to update the old one
                                                    // - the actual traded amount may not be quite the same as this, though
                                                    // - but there is still a risk the trade could fail, even using the acual traded balance, if price drops sharply
                                                    balance.Free = quoteAssetAmt;

                                                    var entry = AddStubTrade(quoteAssetSymbol);
                                                    var close = entry.Clone();
                                                    close.Action = TradeAction.CLOSE;
                                                    SetUpdateRequest(close, TradeUpdateRequest.ADD);
                                                    CloseOrder(quoteAssetSymbol, close, true); 
                                                    */
                                                    #endregion
                                                }
                                                else
                                                {
                                                    //not a base to quote trade, so end here
                                                    CloseTradeOnSystem_synchronised(dataClose.Price);
                                                    EndEntryOrCloseScript();
                                                    LogTrades.WriteLine($"**************************************************************************************\n");
                                                }
                                            }
                                            else
                                            {
                                                //second phase of base to quote trade
                                                //so this will be the Fiat trade, which needs removing, and the base trade needs closing here
                                                //However, cannot close the base trade prior to this because there could be another queued order which would
                                                // attempt a fiat buy before this trade had completed.
                                                //So it is a bit hacky, but this will close the base trade from here
                                                var baseTrades = DataContext.Trades.Where(x => x.TradeReference == Entry.StubCallbackTradeReference).ToList();
                                                if (baseTrades != null)
                                                {
                                                    var symbolName = baseTrades.Select(x => x.SymbolName).FirstOrDefault();
                                                    var tradeEngine = MainObj.GetTradeEngineForSymbol(symbolName) as TradeEngineBinance;
                                                    tradeEngine?.EndEntryOrCloseScript(baseTrades);
                                                    DataContext.SaveChanges(); //need to do this to get the UI lists for redrawing, or the base trade will appear to be interrupted until next 1-min update
                                                    tradeEngine?.GetUILists(DataContext);
                                                }
                                                accountData = null;
                                                Entry.ExchangeMethodScript = null; //just so it won't increment the ExchangeMethodIndex after removal

                                                //remove the stub orders
                                                foreach (var trade in ActiveTrades)
                                                {
                                                    SetUpdateRequest(trade, TradeUpdateRequest.FORCE_DELETE);//bypass the delete checks
                                                }
                                                LogTrades.WriteLine($"Stub order filled, removing stub trade from system.\nTrade complete.");
                                                LogTrades.WriteLine($"**************************************************************************************\n");
                                            }

                                            break;

                                        case "CANCELED":
                                        case "REJECTED":
                                        case "EXPIRED":
                                            //can try again
                                            LogTrades.WriteLine($"Trade was {dataClose.Status} in CLOSE_ORDER_SCRIPT for {Close.SymbolName} at {DateTime.UtcNow}");

                                            KillEntryOrCloseOrder(true);

                                            break;
                                    }

                                    tradeStatusChanged = true;

                                    #endregion
                                    break;

                                case 2:
                                    //so need to now sell the quote asset to fiat, having received the balances from the exchange
                                    LogTrades.WriteLine($"Balances received, selling to fiat...");
                                    CloseTradeOnSystem_synchronised((decimal)Close.Price); //price has already been set so reuse this
                                    var quoteAssetSymbol1 = string.Concat(SymbolNames.GetQuoteAssetForSymbol(Entry.SymbolName), SymbolNames.FIAT_ASSET); //e.g. USDT
                                    var entry1 = AddStubTrade(quoteAssetSymbol1);
                                    var close1 = entry1.Clone();
                                    close1.Action = TradeAction.CLOSE;
                                    CloseOrder(quoteAssetSymbol1, close1, true);
                                    SetUpdateRequest(close1, TradeUpdateRequest.ADD);
                                    break;
                            }
                            break;
                        #endregion

                        #region Cancel order (unimplemented)
                        case CANCEL_ORDER_SCRIPT:
                            //this would be to cancel a stop, tp, floor (or OCO)
                            break;
                        #endregion

                        #region Query order (unimplemented)
                        case QUERY_ORDER_SCRIPT:

                            #region Unimplemented
                            /*
                                            switch (trade.Action)
                                       {
                                           case TradeAction.ORDER:
                                               if (orderData.Status == "FILLED")
                                               {
                                                   EnterTradeOnSystem_synchronised(orderData.Price, orderData.CummulativeQuoteQty, TradeExchangeStatus.ENTERED_ON_EXCHANGE);
                                               }
                                               break;
                                           case TradeAction.STOP:
                                           case TradeAction.CEILING:
                                               if (orderData.Status == "CANCELED")
                                               {
                                                   CancelOrderConfirmed_synchronised(trade);
                                               }
                                               if (orderData.Status == "FILLED")
                                               {
                                                   CloseEntryConfirmed_synchronised(orderData, trade);
                                               }
                                               break;
                                           case TradeAction.CLOSE:
                                               if (orderData.Status == "FILLED")
                                               {
                                                   CloseEntryConfirmed_synchronised(orderData, trade);
                                               }
                                               break;
                                       }
                                            * */
                            #endregion

                            break;
                            #endregion

                            #region Stop and take profit, unimplemented
                            /*
                            case ExchangeRequest.NEW_ORDER_ON_CLOSE__STOP:
                                //existing stop has been cancelled
                                var dataStop = exchangeData as OrderData;
                                if (dataStop.Status == "CANCELED" || dataStop.Status == "REJECTED" || dataStop.Status == "EXPIRED")
                                {
                                    LogTrades.WriteLine($"OnExchangeDataReceived NEW_ORDER_ON_CLOSE__STOP...");
                                    trade.ExchangeRequestIntent = ExchangeRequest.NEW_ORDER__STOP;
                                    RequestStopTakeProfitAddition(trade, ApiParamNames.STOP_LOSS_LIMIT);
                                    DataContext.SaveChanges();
                                }
                                break;

                            case ExchangeRequest.NEW_ORDER_ON_CLOSE__TAKEPROFIT:
                                //existing stop has been cancelled
                                var dataTP = exchangeData as OrderData;
                                if (dataTP.Status == "CANCELED" || dataTP.Status == "REJECTED" || dataTP.Status == "EXPIRED")
                                {
                                    LogTrades.WriteLine($"OnExchangeDataReceived NEW_ORDER_ON_CLOSE__TAKEPROFIT...");
                                    trade.ExchangeRequestIntent = ExchangeRequest.NEW_ORDER__TAKEPROFIT;
                                    RequestStopTakeProfitAddition(trade, ApiParamNames.TAKE_PROFIT_LIMIT);
                                    SetUpdateRequest(trade, TradeUpdateRequest.UPDATE);
                                }
                                break;

                            case ExchangeRequest.NEW_ORDER__STOP:
                            case ExchangeRequest.NEW_ORDER__TAKEPROFIT:
                                var dataNewStop = exchangeData as NewOrderData;
                                if (dataNewStop.Status == "NEW")
                                {
                                    LogTrades.WriteLine($"OnExchangeDataReceived {request}...");
                                    trade.ExchangeRequestIntent = ExchangeRequest.COMPLETED;
                                    trade.Price = (double)dataNewStop.Price;
                                    SetUpdateRequest(trade, TradeUpdateRequest.UPDATE);
                                }
                                if (request == ExchangeRequest.NEW_ORDER__STOP)
                                {
                                    pendingLiveEntryStop = null;
                                }
                                else
                                {
                                    pendingLiveEntryTP = null;
                                }
                                break;

                            case ExchangeRequest.CLOSE_ORDER__ENTRY:
                                //if status is 'NEW' then order is accepted but not yet filled
                                //status could be 'PARTIALLY_FILLED' (limit orders only??)
                                var dataClose = exchangeData as NewOrderData;
                                switch (dataClose.Status)
                                {
                                    case "FILLED":
                                        LogTrades.WriteLine($"OnExchangeDataReceived CLOSE_ORDER__ENTRY filled...");
                                        CloseEntryConfirmed_synchronised(exchangeData as NewOrderData, trade);
                                        break;
                                    case "CANCELED":
                                    case "REJECTED":
                                    case "EXPIRED":

                                        LogTrades.WriteLine($"OnExchangeDataReceived CLOSE_ORDER__ENTRY cancelled...");
                                        LogTrades.WriteLine($"Trade {trade.ExchangeRequestIntent} was rejected for {trade.SymbolName} as {dataClose.Status} at {DateTime.UtcNow}");

                                        //entry and close will be set with pending exchange update requests
                                        //need to remove the close, so it can be regenerated on next update loop when stop or tp triggers
                                        if (trade.Action == TradeAction.CLOSE)
                                        {
                                            Entry.ExchangeRequestIntent = ExchangeRequest.COMPLETED;

                                            SetUpdateRequest(trade, TradeUpdateRequest.DELETE);
                                            SetUpdateRequest(Entry, TradeUpdateRequest.UPDATE);
                                        }

                                        break;
                                }

                                break;

                            case ExchangeRequest.CANCEL_ORDER:
                                LogTrades.WriteLine($"OnExchangeDataReceived CANCEL_ORDER...");

                                //this should only be hit when removing a stop or take profit.
                                //taking a running trade off is a close order, and taking an unentered trade off is nothing to do with the exchange
                                CancelOrderConfirmed_synchronised(trade);
                                break;

                                */
                            #endregion
                    }

                    if (Entry.ExchangeMethodScript != null) //if script ended, this will be null
                    {
                        Entry.ExchangeMethodIndex++;
                        SetUpdateRequest(Entry, TradeUpdateRequest.UPDATE);
                    }
                }
            });

            MainObj.DrawCurrentDispatcher();
        }

        protected override void KillEntryOrCloseOrder(bool interrupted = false)
        {
            //kill order only applies to new entry being abandoned prior to submitting order request to exchange, or if it is rejected by exchange
            // - so only the Entry will actually have the script name, etc., but this saves a test for NULL on that if calling EndScript()

            foreach (var trade in ActiveTrades)
            {
                trade.ItemStatus = TradeItemStatus.CLOSED;
                trade.GlobalStatus = TradeGlobalStatus.STOPPED;
                if (!interrupted)
                {
                    trade.ExchangeMethodScript = null;
                    trade.ExchangeMethodIndex = 0;
                }
                SetUpdateRequest(trade, TradeUpdateRequest.UPDATE);
                tradeEnded = true;
            }
            accountData = null;
            TradeEngine.DequeueOrder_synchronised(this);
        }

        void EndEntryOrCloseScript()
        {
            EndEntryOrCloseScript(ActiveTrades);
        }
        void EndEntryOrCloseScript(List<_Trade> activeTrades)
        {
            foreach (var trade in activeTrades)
            {
                trade.ExchangeMethodScript = null;
                trade.ExchangeMethodIndex = 0;
                SetUpdateRequest(trade, TradeUpdateRequest.UPDATE);
            }

            accountData = null;
            TradeEngine.DequeueOrder_synchronised(this);
        }
        #region Utility methods  



        private (double, double) GetCorrectPrecisionForTrade(string symbolName, double balanceToTrade)
        {
            if (balanceToTrade > 0) //just in case there are too many active trades for some reason, which would present a negative value
            {
                if (USE_SIMULATED_EXCHANGE)
                {
                    return (balanceToTrade, 1);
                }
                if (exchangeInfo != null)
                {
                    var symbol = exchangeInfo.Symbols.Where(x => x.Symbol == symbolName).FirstOrDefault();
                    if (symbol == null)
                    {
                        LogTrades.WriteLine($"Could not find symbol in ExchangeInfo for {symbolName}");
                        return (-1, 0);
                    }

                    var lotSize = symbol.Filters.Where(x => x.FilterType == "LOT_SIZE").FirstOrDefault();
                    if (lotSize == null)
                    {
                        LogTrades.WriteLine($"Could not find lot size in ExchangeInfo for {symbolName}");
                        return (-1, 0);
                    }
                    var stepSize = lotSize.StepSize;
                    var ret = (int)(balanceToTrade / stepSize) * stepSize;
                    var p = (int)Math.Log10(stepSize);
                    ret = Math.Round(ret, -p);
                    return (ret, stepSize);
                }
            }
            return (-1, 0);
        }

        protected override void Binance_StartSimulation()
        {
            exchangeInfo = new ExchangeInfoData();
            if (!connectionInitialised)
            {
                connectionInitialised = true;
                Binance_InitEvents();
            }
            dataReady = true;
        }
        #endregion

        #region Unused

        //private void RequestStopTakeProfitAddition(_Trade trade, ApiParamNames orderType)
        //{
        //    //this is probably correct not to use the pending trade list, that's for entry and close orders
        //    StartExchangeRequestDirect(
        //        ExchangeRequest.NEW_ORDER,
        //        (ApiParamNames.SYMBOL, symbolName),
        //        (ApiParamNames.SIDE, ApiParamNames.SELL),
        //        (ApiParamNames.ORDER_TYPE, orderType),
        //        (ApiParamNames.QUANTITY, trade.Quantity),
        //        (ApiParamNames.PRICE, trade.Price),
        //        (ApiParamNames.STOP_PRICE, trade.Price),
        //        (ApiParamNames.TIME_IN_FORCE, "GTC"),
        //        (ApiParamNames.TRADE_ID_NEW, trade.ItemId),
        //        (ApiParamNames.RESPONSE_TYPE, ApiParamNames.FULL)); //this is the default for market orders, but must be specified for stops and tp orders
        //}
        //public void StopTakeProfitConfirmed(Trade trade, OrderData newOrderData, DataContext db)
        //{
        //    try
        //    {
        //        if (newOrderData.Symbol == symbolName)
        //        {
        //            //just a single intent for stop confirm, so no need to query for intent
        //            // - market, stop and tp orders generate an order type which is queried to invoke the correct callback handler
        //            if (newOrderData.Status == "NEW")
        //            {
        //                //tp will be EF tracked
        //                trade.Timestamp = newOrderData.TimeAsDateTime;
        //                trade.Price = (double)newOrderData.Price;
        //                trade.ExchangeRequestIntent = ExchangeRequest.COMPLETED;

        //                EnterStopTakeProfitOnSystem(trade, newOrderData.Price);
        //            }
        //            else if (newOrderData.Status == "FILLED")
        //            {
        //                //close trade on system
        //                CloseTrade(trade);
        //            }
        //            else
        //            {
        //                //anything else, remove the stop from the system
        //                ChartLine.RemoveTrade(trade, db);

        //                LogTrades.WriteLine($"Stop order was rejected by Exchange for {trade.SymbolName} at {DateTime.UtcNow}");
        //            }

        //            db.SaveChanges();

        //        }
        //    }
        //    catch (Exception ex)
        //    {

        //    }
        //} 

        /*
        * Check current exchange status for all running orders. Close any which are not running on the exchange.
        * This is called once per minute, and on init
        * */
        /*
        private void CheckExchangeOrdersStatus()
        {
            
              //TODO - 
              //this would replace the call to GetUILists() in AddItem, as this would be done every minute or so, and as soon as
              //the local system is out of synch with the exchange, it would be updated
             
            lock (synchLock)
            {
                using (DataContext db = new DataContext())
                {//todo - check if exchangeInfo needs refreshing. How would this affect synchronisation? This method could be from a UI call...
                    if (exchangeInfo == null)
                    {
                        StartExchangeRequest(ExchangeRequest.EXCHANGE_INFO);// sending symbol name just validates it's presence, it still returns the lot, (ApiParamNames.SYMBOL, symbolName));
                    }
                    var liveOrders = db.Trades.AsNoTracking().Where(x =>
                        x.SymbolName == symbolName &&
                        x.GlobalStatus == TradeGlobalStatus.ACTIVE &&
                        x.ExchangeStatus == TradeExchangeStatus.ENTERED_ON_EXCHANGE &&
                        x.ItemStatus == TradeItemStatus.OPEN &&
                        x.ItemId != null); //will have an itemId if it has been sent to the exchange

                    //order may be closing. Retry to synch this. Once closure complete, won't be a live order anymore and won't hit this
                    foreach (var trade in liveOrders.Where(x =>
                           x.Action == TradeAction.CLOSE))
                    {
                        StartExchangeRequestDirect(
                            ExchangeRequest.QUERY_ORDER,
                            (ApiParamNames.SYMBOL, trade.SymbolName),
                            (ApiParamNames.TRADE_ID_ORIGINAL, trade.ItemId)
                            );
                    }

                    //order may be entering
                    //if entering, then will just retry automatically each loop until it enters or goes beyond entry range, so no need to query
                    
                    //foreach (var trade in liveOrders.Where(x => 
                    //        x.Action == TradeAction.ORDER && 
                    //        x.ExchangeRequestIntent != ExchangeRequest.COMPLETED && 
                    //        x.ExchangeRequestIntent != ExchangeRequest.TIMEOUT))
                    //{
                    //    StartExchangeRequest(
                    //        ExchangeRequest.QUERY_ORDER, 
                    //        (ApiParamNames.SYMBOL, trade.SymbolName), 
                    //        (ApiParamNames.TRADE_ID_ORIGINAL, trade.ItemId)
                    //        );
                    //}
                    

                    //stops and take profits may be live or not, or may have actually been closed (trade closed) on the exchange
                    //if they are a close-to-open order, then would need to open a new order on the exchange, but that is too complex to deal with here
                    //so this just synchs the order to the current exchange status
                    //but not doing this yet anyway
                    
                    //foreach (var trade in liveOrders.Where(x =>
                    //        x.Action == TradeAction.STOP || 
                    //        x.Action == TradeAction.TAKE_PROFIT &&
                    //        x.ExchangeRequestIntent != ExchangeRequest.NEW_ORDER_ON_CLOSE__STOP &&
                    //        x.ExchangeRequestIntent != ExchangeRequest.NEW_ORDER_ON_CLOSE__TAKEPROFIT))
                    //{
                    //    StartExchangeRequest(
                    //        ExchangeRequest.QUERY_ORDER,
                    //        (ApiParamNames.SYMBOL, trade.SymbolName),
                    //        (ApiParamNames.TRADE_ID_ORIGINAL, trade.ItemId)
                    //        );
                    //}
                    
                }
            }
        }
        */

        /*
         * OrderDataReceived
         * Called when an order has been queried
         * This will set the final order status. 
         * If it is for a stop or take profit and that has been triggered, this will close the order
         * 
        //public void OrderDataReceived(object sender, ExchangeEventArgs e)
        public void OrderDataReceived(OrderData orderData)
        {
            try
            {
                //using the Binance data directly, no filtering
                //could always map to an intermediate object if abstraction was ever needed
                if (orderData.Symbol == symbolName)
                {
                    // - no, use context synch instead - using (DataContext db = new DataContext())
                    {

                        //note, no trading pairs have STOP_LOSS or TAKE_PROFIT order types available, only the _LIMIT types
                        //so looking for Type of: MARKET, STOP_LOSS_LIMIT and TAKE_PROFIT_LIMIT 

                        var trade = db.Trades.Where(x => x.ItemId == orderData.ClientOrderId).FirstOrDefault();
                        if (trade != null)
                        {
                            switch (orderData.Type)
                            {
                                case "MARKET":

                                    //a market order will already be on the system, when it enters it will go to the exchange first and
                                    // will remain open on the system until exchange accepts order, when it will be marked as TradeItemStatus == closed
                                    // and an entry will be set

                                    switch (trade.Action)
                                    {
                                        case TradeAction.ORDER:
                                            EntryConfirmed(orderData, trade);
                                            break;
                                        case TradeAction.CLOSE:
                                            CloseEntryConfirmed(orderData, trade);
                                            break;
                                    }


                                    break;

                                case "STOP_LOSS_LIMIT":
                                case "TAKE_PROFIT_LIMIT":

                                    StopTakeProfitConfirmed(trade, orderData, db);
                                    break;

                            }

                        }

                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        */

        #endregion

    }
}
