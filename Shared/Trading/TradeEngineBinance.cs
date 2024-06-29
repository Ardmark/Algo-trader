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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

/*
 * Quote-to-base trading and stub orders: This code has been removed but is still in a project-excluded copy of the original file.
 * */
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
            public string origClientOrderId;
            public long orderId;
        }

        #region Event handlers
        public event EventHandler<ExchangeEventArgs> ExchangeDataReceivedEventHandler;
        public static event EventHandler<ExchangeEventArgs> GetAccountBalancesEventHandler; //non-trade event handler (e.g. to display balances)
        #endregion

        public event EventHandler<ExchangeEventArgs> GetExchangeInfoEventHandler; //unimplemented

        #region Fields
        DateTime lastExchangeInfoRequest = new DateTime();
        public ExchangeInfoData exchangeInfo = null;
        AccountDataFiltered accountData;
        #endregion

        #region Init
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
        }

        #endregion

        /*
         * This works as follows:
         * 1) Entry is added, with ExchangeMethodScript = NEW_ORDER_SCRIPT, or for a close, CLOSE_ORDER_SCRIPT is set
         * 2) Request for Account Info is made from exchange
         * 3) When account info is received, the Entry trade can then be executed as an on-exchange order, either as a buy or sell
         * 
         * */

        #region Get account balances
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

                #region Get total equity
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
                #endregion

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

                    //var free = $"{pc}%";
                    //if (v.Free > 0 && v.Locked == 0)
                    //{
                    //    free = "100";
                    //}
                    //else if (v.Free == 0 && v.Locked > 0)
                    //{
                    //    free = "0";
                    //}
                    //else
                    //{
                    //    free = ((v.Free + v.Locked) / v.Free * 100).ToString("0.00");
                    //}

                    //free += "% free";
                    if (noSymbol)
                    {
                        //resultMsg += $"{v.Asset}:\tno symbol\n";
                    }
                    else
                    {
                        resultMsg += $"{v.Asset}:\t{pc.ToString("0.0")}%\t\t${value.ToString("0.00")}\n";
                    }
                }

                resultMsg += $"\n------------------------------------\nTotal equity:\t${totalEquity.ToString("0.00")}\n------------------------------------\n";

                var btcEngine = MainObj.GetTradeEngineForSymbol("BTCUSDT");
                if (btcEngine != null && btcEngine.currentDataItem != null)
                {
                    resultMsg += $"\nLast BTC price:\t${btcEngine.currentDataItem.Close}";
                }

                resultMsg += $"{errorMsg}";

                AlertDialog.Show(resultMsg, "Balances");
            }
        }
        #endregion

        #region Start entry or close order
        public override void StartEntryOrClose_Synchronised()
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

            accountData = null;

            if (DateTime.UtcNow.Subtract(lastExchangeInfoRequest).TotalDays > 1)
            {
                exchangeInfo = null; //fetch a new data set, once a day should be fine to keep up to date with any changes
                lastExchangeInfoRequest = DateTime.UtcNow;
            }

            MainObj.exchangeConnector.AddRequest(ExchangeRequest.ACCOUNT, ExchangeDataReceivedEventHandler, null);
            if (exchangeInfo == null)
            {
                MainObj.exchangeConnector.AddRequest(ExchangeRequest.EXCHANGE_INFO, ExchangeDataReceivedEventHandler, null);
                //can have this as a static field, and only request it once. Otherwise, refresh every time a trade executes
                // sending symbol name just validates it's presence, it still returns the lot, (ApiParamNames.SYMBOL, symbolName));
            }
        }

        protected override void KillEntryOrCloseOrder(bool interrupted = false)
        {
            base.KillEntryOrCloseOrder(interrupted);
            accountData = null;
        }

        protected override void EndEntryOrCloseScript()
        {
            base.EndEntryOrCloseScript();
            accountData = null;
        }

        #endregion

        #region Process data

        //public static _Trade TempStop = null;

        internal void OnDataReceivedForTrade_synchronised(object result)
        {
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

            //void _RunExchangeTradeRequestStop(ExchangeRequestData pendingRequest)
            //{

            //    MainObj.exchangeConnector.AddRequest(
            //        pendingRequest.exchangeRequestType,
            //        ExchangeDataReceivedEventHandler,
            //        (ApiParamNames.SYMBOL, pendingRequest.symbol),
            //        (ApiParamNames.SIDE, pendingRequest.side),
            //        (ApiParamNames.ORDER_TYPE, pendingRequest.orderType),
            //        (ApiParamNames.QUANTITY, pendingRequest.quantity),
            //        (ApiParamNames.TRADE_ID_NEW, pendingRequest.tradeIdNew),
            //        (ApiParamNames.STOP_PRICE, pendingRequest.stopPrice),
            //        (ApiParamNames.RESPONSE_TYPE, ApiParamNames.FULL)
            //        );
            //}

            void _CancelStopTradeRequest(ExchangeRequestData pendingRequest)
            {
                MainObj.exchangeConnector.AddRequest(
                       pendingRequest.exchangeRequestType,
                       ExchangeDataReceivedEventHandler,
                       (ApiParamNames.SYMBOL, pendingRequest.symbol),
                       //(ApiParamNames.ORDER_TYPE, pendingRequest.orderType),
                       (ApiParamNames.ORIG_CLIENT_ORDER_ID, pendingRequest.origClientOrderId)
                       //(ApiParamNames.RESPONSE_TYPE, ApiParamNames.FULL)
                       //(ApiParamNames.TIME_IN_FORCE,   "GTC") - default anyway
                       );
            }

            InitForDataContext(() =>
            {
                if (result is ExchangeInfoData)
                {
                    exchangeInfo = result as ExchangeInfoData;
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

                if (Entry != null && Entry.ExchangeMethodScript != null)
                {
                    switch (Entry.ExchangeMethodScript)
                    {
                        #region New order
                        case NEW_ORDER_SCRIPT:

                            //process is 
                            //1) get the account data, for the amount of BTC available
                            //2) get the price of all assets, and use account data to calculate a purchase amount

                            switch (Entry.ExchangeMethodIndex)
                            {
                                case 0:
                                    // Get quote price

                                    MainObj.exchangeConnector.AddRequest(ExchangeRequest.QUOTE_PRICE, ExchangeDataReceivedEventHandler, (ApiParamNames.SYMBOL, symbolName));
                                    SetUpdateRequest(Entry);//, TradeUpdateRequest.UPDATE);
                                    break;

                                case 1:
                                    // Generate Entry order
                                    var quoteData = result as QuoteData;
                                    if (quoteData == null)
                                    {
                                        LogTrades.WriteLine($"Quote data was null in NEW_ORDER_SCRIPT for {symbolName}, exiting OnExchangeDataReceived");
                                        KillEntryOrCloseOrder();
                                        return;
                                    }


                                    //From the API docs:
                                    // base asset refers to the asset that is the quantity of a symbol. For the symbol BTCUSDT, BTC would be the base asset.
                                    // quote asset refers to the asset that is the price of a symbol. For the symbol BTCUSDT, USDT would be the quote asset.

                                    (var baseAsset1, var quoteAsset) = SymbolNames.GetAssetsForSymbol(quoteData.Symbol);

                                    if (quoteAsset == null)
                                    {
                                        LogTrades.WriteLine($"Quote asset was NULL for {quoteData.Symbol}, exiting OnExchangeDataReceived");
                                        KillEntryOrCloseOrder();
                                        return;
                                    }
                                    var assetBalance1 = accountData.Balances.Where(x => x.Asset == quoteAsset).Select(x => x.Free).FirstOrDefault();

                                    //it is assumed only a single symbol is being traded if asset balance is being limited to 100, this will be a USDT balance as well
                                    if (TradeEngine.RESTRICTED_TRADE_VALUE > 0)
                                    {
                                        assetBalance1 = TradeEngine.RESTRICTED_TRADE_VALUE;
                                    }

                                    //the symbol price is the ratio between the base and quote assets
                                    var symbolPrice = quoteData.Price;

                                    if (symbolPrice > 0 && TradeEngine.MAX_CONCURRENT_TRADES > 0)
                                    {
                                        var displaySymbolName = Entry.SymbolName;

                                        var numLiveQuoteAssetEntries = DataContext.Trades.Count(x =>
                                                x.Id != Entry.Id && //not really necessary, since the trade to exclude would have an exchange method script anyway
                                                x.Action == TradeAction.ENTRY &&

                                                x.Type != TradeType.STUB_ORDER && //this is only on Fiat, so would not be an issue in this context anyway but doesn't hurt to leave it in for future reference
                                                (
                                                    (x.ItemStatus == TradeItemStatus.OPEN && x.GlobalStatus == TradeGlobalStatus.ACTIVE) &&
                                                    x.ExchangeMethodScript == null
                                                )
                                            );

                                        if (numLiveQuoteAssetEntries >= TradeEngine.MAX_CONCURRENT_TRADES)
                                        {
                                            //this should not happen, but just in case...
                                            KillEntryOrCloseOrder();
                                            MainObj.DrawCurrentDispatcher();
                                            LogTrades.WriteLineOrder($"Could not enter trade as there are already {numLiveQuoteAssetEntries} entries running at {DateTime.UtcNow}");
                                            LogTrades.WriteLine($"Maximum number of concurrent trades exceded for {displaySymbolName}, not completing trade (Get Quote Price).");
                                            return;
                                        }

                                        //the fractional trade amount is the amount of e.g. ADABTC which must be purchased relative to the available equity
                                        //this will be 0.25, 0.33, 0.5, 1 for a max. of 4 trades
                                        double baseAssetTradeAmount = 1 / (double)(TradeEngine.MAX_CONCURRENT_TRADES - numLiveQuoteAssetEntries);

                                        var coinBalance = assetBalance1 / symbolPrice;
                                        coinBalance *= baseAssetTradeAmount;

                                        //it is possible that the asset price will rise slightly during the order execution
                                        //this will only affect the final trade, as there will not be enough quote asset to fill the order.
                                        //Easiest way to avoid this is to leave 0.5% quote asset, this will aggregate up to 2% for the final trade.
                                        //So this would always leave a small floating balance of the quote asset (i.e. BTC) but would sell out 98%
                                        coinBalance *= 0.995;

                                        (var amount, var stepSize) = GetCorrectPrecisionForTrade(symbolName, coinBalance);

                                        if (amount > 0)
                                        {
                                            LogTrades.WriteLine($"Buying {amount} of {baseAsset1}");

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
                                    break;

                                case 2:
                                    // Set order on system
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
                                        SetUpdateRequest(Entry);//, TradeUpdateRequest.UPDATE);
                                    }

                                    switch (dataEntry.Status)
                                    {
                                        case "FILLED":
                                            LogTrades.WriteLineOrder($"\nOrder FILLED for {Entry.SymbolName} at {DateTime.UtcNow}");

                                            //this will be either the second phase from a QTB buy, or the first in a regular buy
                                            // - enter the trade on the system
                                            EnterTradeOnSystem_synchronised(dataEntry.Price, dataEntry.ExecutedQty, TradeExchangeStatus.ENTERED_ON_EXCHANGE);
                                            EndEntryOrCloseScript();

                                            break;
                                        case "CANCELED":
                                        case "REJECTED":
                                        case "EXPIRED":
                                            LogTrades.WriteLineOrder($"Order was rejected for {Entry.SymbolName} as {dataEntry.Status} at {DateTime.UtcNow}");
                                            KillEntryOrCloseOrder();
                                            break;
                                    }

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

                            switch (Entry.ExchangeMethodIndex)
                            {
                                case 0: //remove any stops from exchange
                                    if (Stop != null && Stop.ItemId != null)
                                    {
                                        _CancelStopTradeRequest(new ExchangeRequestData
                                        {
                                            exchangeRequestType = ExchangeRequest.CANCEL_ORDER,
                                            symbol = symbolName,
                                            origClientOrderId = Stop.ItemId
                                        });
                                    }
                                    //TempStop = null;
                                    break;
                                case 1:
                                    accountData = null;
                                    MainObj.exchangeConnector.AddRequest(ExchangeRequest.ACCOUNT, ExchangeDataReceivedEventHandler, null);
                                    break;

                                case 2:
                                    #region Generate Close order

                                    //a QUANTITY order specifies the amount of base asset to buy or sell, so can go directly from here

                                    LogTrades.WriteLine($"Closing order for {symbolName}");
                                    var _baseAsset = SymbolNames.GetBaseAssetForSymbol(symbolName);
                                    if (_baseAsset != null)
                                    {
                                        var coinBalance = accountData.Balances.Where(x => x.Asset == _baseAsset).Select(x => x.Free).FirstOrDefault();

                                        LogTrades.WriteLine($"Using received balance of {coinBalance} {_baseAsset}");

                                        //retain some BNB to reduce trading fees
                                        if (_baseAsset == "BNB" && RETAINED_BNB_PERCENTAGE > 0)
                                        {
                                            coinBalance *= (1 - RETAINED_BNB_PERCENTAGE / 100);
                                            LogTrades.WriteLine($"Retaining {RETAINED_BNB_PERCENTAGE}% of BNB");
                                        }

                                        (var amount1, var stepSize) = GetCorrectPrecisionForTrade(symbolName, coinBalance);

                                        if (amount1 > 0)
                                        {
                                            Close.Quantity = amount1;

                                            LogTrades.WriteLine($"Placing sell order of {coinBalance}, adjusted for step size, at {DateTime.Now}");
                                            SetUpdateRequest(Close);//, TradeUpdateRequest.UPDATE);

                                            _RunExchangeTradeRequest(new ExchangeRequestData
                                            {
                                                tradeId = Close.Id,
                                                exchangeRequestType = ExchangeRequest.NEW_ORDER,
                                                symbol = symbolName,
                                                side = ApiParamNames.SELL,
                                                orderType = ApiParamNames.MARKET,
                                                quantity = amount1,
                                                tradeIdNew = Close.ItemId,
                                            });
                                        }
                                        else
                                        {
                                            AlertDialog.Show($"Quote asset balance was zero so could not close trade for {symbolName}");
                                        }
                                    }

                                    #endregion
                                    break;
                                
                                case 3:
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

                                    switch (dataClose.Status)
                                    {
                                        case "FILLED":

                                            LogTrades.WriteLineOrder($"Close order filled, closing trade on system at {DateTime.UtcNow}");
                                            CloseTradeOnSystem_synchronised(dataClose.Price);
                                            EndEntryOrCloseScript();
                                            LogTrades.WriteLine($"**************************************************************************************\n");

                                            break;

                                        case "CANCELED":
                                        case "REJECTED":
                                        case "EXPIRED":
                                            //can try again
                                            LogTrades.WriteLineOrder($"Trade was {dataClose.Status} in CLOSE_ORDER_SCRIPT for {Close.SymbolName} at {DateTime.UtcNow}");
                                            KillEntryOrCloseOrder(true);

                                            break;
                                    }

                                    tradeStatusChanged = true;

                                    #endregion
                                    break;
                            }
                            break;
                        #endregion

                        #region Update live state
                        case UPDATE_LIVE_STATE_SCRIPT:

                            var baseAsset = SymbolNames.GetBaseAssetForSymbol(Entry.SymbolName); //i.e. BTC
                            if (baseAsset == null)
                            {
                                LogTrades.WriteLine("Could not update live state, base asset was null");
                                Entry.ExchangeMethodScript = null;
                                Entry.ExchangeMethodIndex = 0;
                                SetUpdateRequest(Entry);//, TradeUpdateRequest.UPDATE);
                                break;
                            }

                            //if the base asset balance is very small, assume the trade has exited
                            var baseAssetBalanceFree = accountData.Balances.Where(x => x.Asset == baseAsset).Select(x => x.Free).FirstOrDefault();
                            var baseAssetBalanceLocked = accountData.Balances.Where(x => x.Asset == baseAsset).Select(x => x.Locked).FirstOrDefault();

                            double minValue = 0;
                            var symbol = exchangeInfo.Symbols.Where(x => x.Symbol == symbolName).FirstOrDefault();
                            if (symbol != null)
                            {
                                var lotSize = symbol.Filters.Where(x => x.FilterType == "LOT_SIZE").FirstOrDefault();
                                if (lotSize != null)
                                {
                                    minValue = lotSize.MinQty * 2; //it should always use the lot size notional value, same as MinQty, to calculate trade quantity so would only leave < MinQty but double this to be safe
                                }
                            }

                            //todo - get the correct minimum trade value for the asset and calculate an appropriate small value
                            if (baseAssetBalanceFree < minValue && baseAssetBalanceLocked < minValue) //i.e there is no BTC
                            {
                                //kill the local trade here - unfortunately, this is not the correct price for the trade
                                //TODO - retrieve the trade and get the correct price on close from the trade history
                                LogTrades.WriteLine("Closing order as order balance is (close to) zero, please note, this is not the correct close price");
                                (var lastValue, var _) = GetValueForSymbol(symbolName);
                                CloseTradeOnSystem_synchronised((decimal)lastValue);
                            }

                            //if there is no locked balance, assume the stop has been removed from the exchange
                            else if (baseAssetBalanceLocked < minValue)
                            {
                                if (Stop != null)
                                {
                                    var st = Stop;
                                    RemoveTradeFromLine_UI_synchronised(st, false);
                                    SetUpdateRequest(st, TradeUpdateRequest.DELETE);
                                    LogTrades.WriteLine("No locked balance on exchange, assume stop cancelled, removing local stop");
                                }
                            }

                            Entry.ExchangeMethodScript = null;
                            Entry.ExchangeMethodIndex = 0;
                            SetUpdateRequest(Entry);//, TradeUpdateRequest.UPDATE);
                            break;
                            #endregion
                    }

                    if (Entry.ExchangeMethodScript != null) //if script ended, this will be null
                    {
                        if (Entry.ExchangeMethodScript != EXCHANGE_NOT_SYNCHED) //because a stop may update, and this is the final state which should not be changed
                        {
                            Entry.ExchangeMethodIndex++;
                            SetUpdateRequest(Entry);//, TradeUpdateRequest.UPDATE);
                        }
                    }
                }

                if (Stop != null && Stop.ExchangeMethodScript != null)//  || (TempStop != null && TempStop.ExchangeMethodScript != null))
                {
                    //Stop = Stop == null ? TempStop : Stop;

                    switch (Stop.ExchangeMethodScript)
                    {
                        case SET_STOP_SCRIPT:

                            //Stop will not be null here

                            //process is:
                            //1) remove any existing stop
                            //2) set a new stop to the current order value

                            //stop may not already be set. If it is, cancel first and wait for next repsonse
                            if (string.IsNullOrEmpty(Stop.ItemId))
                            {
                                Stop.ExchangeMethodIndex = 2;
                            }

                            switch (Stop.ExchangeMethodIndex)
                            {
                                case 0: //remove existing stop

                                    if (!string.IsNullOrEmpty(Stop.ItemId))
                                    {
                                        _CancelStopTradeRequest(new ExchangeRequestData
                                        {
                                            exchangeRequestType = ExchangeRequest.CANCEL_ORDER,
                                            symbol = symbolName,
                                            origClientOrderId = Stop.ItemId
                                            //orderId = 28009071367 //makes no difference
                                        });


                                    }
                                    break;
                                case 1:
                                    accountData = null;
                                    MainObj.exchangeConnector.AddRequest(ExchangeRequest.ACCOUNT, ExchangeDataReceivedEventHandler, null);
                                    break;
                                case 2: //set new stop

                                    //don't want to pull the quote data each time a stop gets updated, so will just use the curent ticker price
                                    (var lastValue, var _) = GetValueForSymbol(symbolName);

                                    Stop.ItemId = Guid.NewGuid().ToString();
                                    SetUpdateRequest(Stop);//, TradeUpdateRequest.UPDATE);
                                    var baseAsset = SymbolNames.GetBaseAssetForSymbol(symbolName);
                                    var coinBalance = accountData.Balances.Where(x => x.Asset == baseAsset).Select(x => x.Free).FirstOrDefault();

                                    (var amount, var stepSize) = GetCorrectPrecisionForTrade(symbolName, coinBalance);

                                    /*
                                    An OCO has 2 legs called the above leg and below leg.
                                    One of the legs must be a LIMIT_MAKER order and the other leg must be STOP_LOSS or STOP_LOSS_LIMIT order.
                                    Price restrictions:
                                    If the OCO is on the SELL side: LIMIT_MAKER price > Last Traded Price > stopPrice
                                    If the OCO is on the BUY side: LIMIT_MAKER price < Last Traded Price < stopPrice
                                    */

                                    //this is calling the rest.js method correctly, so the error is within this data
                                    //(-1104 "not all sent parameters were read")
                                    //(-1106 "a parameter was sent when not required")
                                    //(-1102 "A mandatory parameter was not sent, was empty/null, or malformed")
                                    //(-1128 "Combination of optional parameters invalid.")
                                    //(-2011 "Unknown order sent")
                                    //(-1013 - this was "Stop loss orders are not supported for this symbol." but is not documented as an error code (only 1014 and beyond)

                                    //can swap prices around, still get the same -1013 error
                                    //"Stop loss orders are not supported for this symbol."

                                    float abovePrice = (float)lastValue * 2; //arbitrary, could use a take profit price here
                                    float triggerPrice = (float)Stop.Price;

                                    //this is just to ensure the stop executes immediately, not as a limit order
                                    //but if the belowPrice is too low (like half the trigger price) a small order (test order) may not have a large enough notional value
                                    float belowPrice = (float)(triggerPrice * 0.9); 
                                    //var symbol = exchangeInfo.Symbols.FirstOrDefault(x => x.Symbol == symbolName);
                                    //var minSize = symbol.Filters.Where(x => x.FilterType == "NOTIONAL").FirstOrDefault();
                                    MainObj.exchangeConnector.AddRequest(
                                                ExchangeRequest.NEW_OCO,
                                                ExchangeDataReceivedEventHandler,
                                                (ApiParamNames.SYMBOL, Stop.SymbolName),
                                                (ApiParamNames.SIDE, ApiParamNames.SELL),
                                                (ApiParamNames.QUANTITY, coinBalance), //generates -1013 for btc or usdt value - minimum trade value is 0.0001 BTC
                                                (ApiParamNames.ABOVE_TYPE, ApiParamNames.LIMIT_MAKER),
                                                //(ApiParamNames.ABOVE_STOP_PRICE, 70000), //so presumably this would be for a buy, not a sell
                                                //(ApiParamNames.ABOVE_TIMEINFORCE, 1000),
                                                (ApiParamNames.ABOVE_PRICE, abovePrice), //generates -1102 if missing
                                                (ApiParamNames.BELOW_TYPE, ApiParamNames.STOP_LOSS_LIMIT),
                                                (ApiParamNames.BELOW_STOP_PRICE, triggerPrice), //generates -1128 if missing
                                                (ApiParamNames.BELOW_PRICE, belowPrice), //this is needed also
                                                (ApiParamNames.BELOW_TIMEINFORCE, "GTC")
                                                //(ApiParamNames.TRADE_ID_NEW, Guid.NewGuid().ToString()), //generates -1104 if sent
                                                //(ApiParamNames.RESPONSE_TYPE, ApiParamNames.FULL)
                                                //(ApiParamNames.TIME_IN_FORCE,   "GTC") - default anyway
                                                );

                                    break;
                                
                                case 3:
                                    var dataEntry = result as NewOCOData;
                                    if (result != null)
                                    {
                                        var id = dataEntry.OrderReports
                                            .Where(x => x.Type == "STOP_LOSS_LIMIT")
                                            .Select(x => x.ClientOrderId)
                                            .FirstOrDefault(); //if this is set, then this stop can be updated next time. Not sure if you can also use the list id instead of this
                                        Stop.ItemId = id;
                                        Stop.ExchangeMethodScript = null;
                                        Stop.ExchangeMethodIndex = 0;
                                        Stop.ExchangeStatus = TradeExchangeStatus.ENTERED_ON_EXCHANGE;
                                        SetUpdateRequest(Stop);//, TradeUpdateRequest.UPDATE);
                                    }
                                    break;
                            }


                            tradeStatusChanged = true;
                            break;

                        case CANCEL_STOP_SCRIPT:

                            switch (Stop.ExchangeMethodIndex)
                            {
                                case 0:
                                    _CancelStopTradeRequest(new ExchangeRequestData
                                    {
                                        exchangeRequestType = ExchangeRequest.CANCEL_ORDER,
                                        symbol = symbolName,
                                        origClientOrderId = Stop.ItemId// TempStop.ItemId
                                    });
                                    break;

                                case 1:
                                    RemoveTradeFromLine_UI_synchronised(Stop);
                                    SetUpdateRequest(Stop, TradeUpdateRequest.DELETE);
                                    break;
                            }
                            break;
                    }

                    if (Stop != null && Stop.ExchangeMethodScript != null) //if script ended, this will be null
                    {
                        Stop.ExchangeMethodIndex++;
                        SetUpdateRequest(Stop);//, TradeUpdateRequest.UPDATE);
                    }
                }
            });

            MainObj.DrawCurrentDispatcher();
        }
        #endregion

        #region Stop 
        protected override async void SetStopOnExchange_Synchronised(_Trade stop)
        {
            base.SetStopOnExchange_Synchronised(stop);

            if (stop.Action == TradeAction.STOP) //floors are never set on the exchange in Binance
            {
                if (stop.ExchangeStatus == TradeExchangeStatus.ENTERED_OFF_EXCHANGE)
                {
                    LogTrades.WriteLine($"Did not set stop on exchange, exchange status was {stop.ExchangeStatus} - you need to add a new stop!");
                    stop.ExchangeStatus = TradeExchangeStatus.NO_WARNING;
                    SetUpdateRequest(stop);//, TradeUpdateRequest.UPDATE);
                    return;
                }

                stop.ExchangeMethodScript = SET_STOP_SCRIPT;
                stop.ExchangeMethodIndex = !string.IsNullOrEmpty(stop.ItemId) ? 0 : 1;
                StartEntryOrClose_Synchronised();
            }
        }

        protected override void RemoveStopFromExchange_Synchronised(_Trade stop)
        {
            stop.ExchangeMethodScript = CANCEL_STOP_SCRIPT;
            stop.ExchangeMethodIndex = 0;
            StartEntryOrClose_Synchronised();
        }

        protected override void RemoveTradeBeforeExchangeRemoval_Synchronised(_Trade trade, bool removeLine, bool allowForceRemove)
        {
            if (!allowForceRemove && trade.Action == TradeAction.STOP)
            {
                return;
            }
            RemoveTradeFromLine_UI_synchronised(trade, removeLine);
            SetUpdateRequest(trade, TradeUpdateRequest.DELETE);
        }

        #endregion

        #region Update state from server

        protected override async void UpdateLiveState()
        {

            if (USE_SIMULATED_EXCHANGE)
            {
                return;
            }

            InitForDataContext(() =>
            {
                var s = Stop;
                //in the event there is a pending entry or stop operation on the exchange, do not do anything. This is unlikely but it would mess things up.
                if (Stop == null || Stop != null && Stop.ExchangeMethodScript == null)
                {
                    if (Entry != null && (Entry.ExchangeMethodScript == null || Entry.ExchangeMethodScript == UPDATE_LIVE_STATE_SCRIPT))
                    {
                        //this is tricky with Binance, because there is no live running trade - only account balances
                        //so if there is a quote asset balance, the trade is still running. Otherwise, close it entirely.
                        Entry.ExchangeMethodIndex = 0;
                        Entry.ExchangeMethodScript = UPDATE_LIVE_STATE_SCRIPT;
                        StartEntryOrClose_Synchronised();
                    }
                }
            });
        }

        #endregion

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

        #region Enqueue and dequeue orders (unused)
                    //no idea why this is here, it seems pointless so has been abandoned (but not tested like this)

                    /*
                    static List<PendingTradeRequest> pendingTradeRequests = new List<PendingTradeRequest>();
                    internal override void EnqueueOrder_synchronised(TradeAction tradeAction)
                    {
                        if (!pendingTradeRequests.Any(x => x.tradeEngine == tradeEngine))
                        {
                            pendingTradeRequests.Add(new PendingTradeRequest { tradeEngine = this, timeStamp = DateTime.UtcNow, action = tradeAction });
                            _RunNextOrder();
                        }
                    }

                    internal override void DequeueOrder_synchronised()
                    {
                        pendingTradeRequests.RemoveAll((PendingTradeRequest pendingRequest) => { return pendingRequest.tradeEngine == this; });
                        _RunNextOrder();
                    }

                    void _RunNextOrder()
                    {
                        //probably not important, but look for requests by earliest first
                        PendingTradeRequest pendingRequest = pendingTradeRequests.OrderBy(x => x.timeStamp).FirstOrDefault(x => x.inProgress == false && x.action == TradeAction.CLOSE);
                        if (pendingRequest == null)
                        {
                            pendingRequest = pendingTradeRequests.OrderBy(x => x.timeStamp).FirstOrDefault(x => x.inProgress == false && x.action == TradeAction.ENTRY);
                        }
                        if (pendingRequest != null)
                        {
                            pendingRequest.inProgress = true;
                            pendingRequest.tradeEngine.StartEntryOrClose();
                        }
                    }

                    protected override void Binance_CheckForPendingTradeRequestTimeout() 
                    {
                        if (pendingTradeRequests.Any())
                        {
                            var p = pendingTradeRequests.First();
                            //using a 25 second timeout, enough for a couple of trades to timeout
                            var timeOut = USE_SIMULATED_EXCHANGE ? 1000 : 25;
                            bool t = DateTime.UtcNow.Subtract(p.timeStamp).TotalSeconds > timeOut;
                            if (t)
                            {
                                LogTrades.WriteLine($"Pending trade timeout for {p.tradeEngine.symbolName}, at {DateTime.UtcNow:G}");
                                pendingTradeRequests.Remove(p);
                            }
                        }
                    }

                    protected override void Binance_RemovePendingTradeRequest()
                    {
                        var pendingRequest = pendingTradeRequests.FirstOrDefault(x => x.tradeEngine == this);
                        if (pendingRequest != null)
                        {
                            //this can happen when a trade cannot fill, ends up interrupted, then the pending request remains on the system
                            pendingTradeRequests.Remove(pendingRequest);
                        }
                    }
                    */
            #endregion

        }
    }
