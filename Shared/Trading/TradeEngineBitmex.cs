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
using System.Windows.Markup;
using System.Runtime.CompilerServices;
using BitMEX;
using AlgoTrader.Backtest.Migrations;
using static BitMEX.BitMEXApi;
using System.Data.Entity.Migrations.Model;
using System.Security.AccessControl;
using System.Windows.Shapes;
using static System.Windows.Forms.LinkLabel;

namespace AlgoTrader.Shared
{
    public class TradeEngineBitmex : TradeEngine
    {
        /*
        * Very simple in bitmex: specify symbol, and either a positive or negative order quantity or a positive quantity and the Order Side (Buy, Sell)
        * When an order is filled, a position becomes open
        * Adding a filled order will just update the quantity in the open position.
        * So can have orders of these types: Market, Limit, Stop, StopLimit, MarketIfTouched, LimitIfTouched, Pegged. 
        * Defaults to 'Limit' when price is specified. Defaults to 'Stop' when stopPx is specified. Defaults to 'StopLimit' when price and stopPx are specified.
        * Market orders will fill straight away, others will remain open until filled
        * Open orders can just be cancelled with the order id.
        * */

        /* 
         * Example of leverage: 
         * Set leverage < 1, this requires trading a proportionally SMALLER amount to get the cost to match the order value
         * Set leverage > 1, this requires trading a proportionally LARGER amount to get the cost to match the order value
         * 
         * Order value: .3333 XBT
         * 
         * Leverage     Cost        Liquidation     Liquidation difference
         * 0.1x         3.3366      2728            90.1%
         * 0.2x         1.3349      6004            80%
         * 0.5x         0.6677      10011           66.6%
         * 1x           0.3341      15026           50%
         * 2x           0.1673      20046           33.3%
         * */

        /*
         * Cross margin:
         * 
         * You can trade this on 100x, by setting the purchase amount to the correct value (1/100 order value) to achieve the same 
         *  as trading the entire amount on 1x, but this would incur a very close liquidation price but would allow for trading multiple symbols
         *  */
        

        public static string KEY { get; internal set; }
        public static string SECRET { get; internal set; }
        public static string DOMAIN { get; internal set; }
        public static double DEFAULT_FLOOR_OFFSET_PERCENT { get; internal set; }
        public static double MAX_LOSS_PC { get; internal set; }
        public static double MAX_LEVERAGE { get; internal set; } = 1;
        BitMEXApi bitmexAPI;
        const double fiatDivisor = 1000000;
        const double XBTdivisor = 100000000;

        #region Init

        public TradeEngineBitmex(string symbolName, int timeInterval) : base(symbolName, timeInterval)  { }

        public override void Init(int _timeInterval)
        {
            base.Init(_timeInterval);
            bitmexAPI = new BitMEXApi(KEY, SECRET);
        }
        #endregion

        _Trade GetOppositeOrder(TradePosition position)
        {
            var oppositeSide = position == TradePosition.LONG ? TradePosition.SHORT : TradePosition.LONG;
            return AllTrades.FirstOrDefault(x =>
                                            x.Action == TradeAction.ORDER &&
                                            x.Position == oppositeSide &&
                                            x.ItemStatus == TradeItemStatus.OPEN &&
                                            x.GlobalStatus == TradeGlobalStatus.ACTIVE);
        }
        protected override void EnterTradeOnSystem_ExchangeSpecific_Synchronised()
        {

            #region Set flip from opposing trade

            //if there is an opposing trade:
            // 1) remove any stop and floor from this trade
            // 2) convert any opposing trade stop to waiting stop
            // 3) convert opposing trade to flip order

            //var oppositeSide = Order.Position == TradePosition.LONG ? TradePosition.SHORT : TradePosition.LONG;
            var firstOppositeOrder = GetOppositeOrder(Order.Position);
            if (firstOppositeOrder != null)
            {
                // 1) remove any stop and floor from this trade
                var stop = AllTrades.FirstOrDefault(x =>
                                    x.Action == TradeAction.STOP &&
                                    x.TradeReference == Order.TradeReference);
                if (stop != null)
                {
                    //just remove any line
                    var stopLine = AllLinesForSymbol.FirstOrDefault(x => x.TradeId == stop.Id);
                    if (stopLine != null)
                    {
                        SetUpdateRequest(stopLine, TradeUpdateRequest.FORCE_DELETE);
                    }

                    SetUpdateRequest(stop, TradeUpdateRequest.FORCE_DELETE);
                }
                var floor = AllTrades.FirstOrDefault(x =>
                                    x.Action == TradeAction.FLOOR &&
                                    x.TradeReference == Order.TradeReference);
                if (floor != null)
                {
                    SetUpdateRequest(floor, TradeUpdateRequest.FORCE_DELETE);
                }

                // 2) convert any opposing trade stop to waiting stop
                var oppStop = AllTrades.FirstOrDefault(x =>
                    x.TradeReference == firstOppositeOrder.TradeReference &&
                    x.Action == TradeAction.STOP);
                var oppFloor = AllTrades.FirstOrDefault(x =>
                    x.TradeReference == firstOppositeOrder.TradeReference &&
                    x.Action == TradeAction.FLOOR);
                if (oppStop != null)
                {
                    //use this stop
                    oppStop.TradeReference = Order.TradeReference;
                    oppStop.Type = TradeType.STOP_WAITING;
                    SetUpdateRequest(oppStop);//, TradeUpdateRequest.UPDATE);

                    var oppStopLine = AllLinesForSymbol.FirstOrDefault(x => x.TradeReference == oppStop.TradeReference);
                    if (oppStopLine != null)
                    {
                        oppStopLine.TradeReference = Order.TradeReference;
                        oppStopLine.Type = LineType.SUPPORT;
                        SetUpdateRequest(oppStopLine);//, TradeUpdateRequest.UPDATE);
                    }

                    //remove any floor
                    if (oppFloor != null)
                    {
                        SetUpdateRequest(oppFloor, TradeUpdateRequest.FORCE_DELETE); //we don't want a floor with a stop, the stop will have converted but the floor will not be usable
                    }
                }
                else if (oppFloor != null)
                {
                    //use this as stop
                    oppFloor.TradeReference = Order.TradeReference;
                    oppFloor.Type = TradeType.STOP_WAITING;
                    oppFloor.Action = TradeAction.STOP; //convert it to a Stop, although there is no stop line
                    SetUpdateRequest(oppFloor);//, TradeUpdateRequest.UPDATE);
                }


                // 3) convert opposing trade to flip order (which is also a stop)
                var line = AllLinesForSymbol.FirstOrDefault(x => x.TradeReference == firstOppositeOrder.TradeReference);
                if (line != null)
                {
                    line.TradeReference = Order.TradeReference;
                    line.Type = LineType.SUPPORT;
                    SetUpdateRequest(line);//, TradeUpdateRequest.UPDATE);
                }

                firstOppositeOrder.TradeReference = Order.TradeReference;
                firstOppositeOrder.Action = TradeAction.STOP;
                firstOppositeOrder.Type = TradeType.FLIP_ORDER;
                firstOppositeOrder.Position = Order.Position; //because a flip is the same side as the order it is flipping.
                SetUpdateRequest(firstOppositeOrder);//, TradeUpdateRequest.UPDATE);

            }

            #endregion
        }
        //public override void RunTrade_exchangeSpecific()
        //{
        //}

        void WriteDeliniatedMessage(string msg)
        {
            LogTrades.WriteLine($"**************************************\n{msg}\n**************************************");
        }

        //The entry point for all trades
        //This method is only called from a single point, which is within a synchronised block.
        //So it is vital that this does NOT start another synchronised section.
        public override void StartEntryOrClose_Synchronised() 
        {
            void _CancelStop(_Trade trade)
            {
                if (trade != null && !USE_SIMULATED_EXCHANGE)
                {
                    //could just delete all orders with this id instead, or can delete a list of orders (see docs)
                    var tradeClose = bitmexAPI.DeleteOrder(trade.ItemId)?.FirstOrDefault();
                    if (tradeClose != null)
                    {
                        if (!string.IsNullOrEmpty(tradeClose.OrderRejectionReason))
                        {
                            WriteDeliniatedMessage($"Failed to remove stop:\n{tradeClose.OrderRejectionReason}");
                        }
                        else
                        {
                            SetUpdateRequest(trade, TradeUpdateRequest.DELETE);
                        }
                    }
                }
            }

            //script name will already be set, use this to determine correct action
            //upon completion, clear the script so the trades will then be drawn as not pending
            if (Entry != null)
            {
                switch (Entry.ExchangeMethodScript)
                {
                    case NEW_ORDER_SCRIPT:

                        void _KillOrder(string msg, bool interrupted = false)
                        {
                            WriteDeliniatedMessage(msg);
                            KillEntryOrCloseOrder(interrupted);
                        }
                        //this will just be a 95% order for now

                        var wallet = bitmexAPI.GetWalletSingleCurrency(SymbolNames.GetCurrencyForSymbol(symbolName));
                        if (wallet != null)
                        {
                            //if this has just restarted, will try to enter any trades immediately but the price won't be available from the websocket yet.
                            //so use the REST api instead
                            var lastQuoteValue = USE_SIMULATED_EXCHANGE ? 0 : bitmexAPI.GetLastPrice(symbolName); //can't get price feed from testnet, only live
                            if (lastQuoteValue == 0)
                            {
                                (var v2, var s1) = GetValueForSymbol(symbolName); //note that the symbol name does not use the fiat asset symbol

                                if (s1 != null)
                                {
                                    LogTrades.WriteLine($"Error getting balance: {s1}");
                                    break;
                                }
                                lastQuoteValue = v2;
                            }
                            #region Set leverage
                            //the problem here is the stops are only calculated AFTER the trade has entered, including any reversal trades.
                            //So that needs to be done first

                            if (MAX_LOSS_PC > 0)
                            {
                                //need the closest of the current stop or opposing trade. If neither found, use the default floor value
                                var stopOffsetPC = DEFAULT_FLOOR_OFFSET_PERCENT; //typically the same value as max loss anyway
                                double leverageDiff = 0;

                                var _oppOrder = GetOppositeOrder(Order.Position);
                                if (_oppOrder != null)
                                {
                                    var orderLine = AllLinesForSymbol.FirstOrDefault(x => x.TradeId == _oppOrder.Id);
                                    if (orderLine != null)
                                    {
                                        (var oppOrderPrice, _) = orderLine.GetPriceAtTime(DateTime.UtcNow, true);
                                        leverageDiff = MainObj.GetPriceDiffPercent(currentDataItem, oppOrderPrice, Order.Position) * 100;
                                    }
                                 }
                                var _stop = AllTrades.FirstOrDefault(x => x.TradeReference == Order.TradeReference && x.Action == TradeAction.STOP);
                                if (_stop != null)
                                {
                                    var stopLine = AllLinesForSymbol.FirstOrDefault(x => x.TradeId == _stop.Id);
                                    if (stopLine != null)
                                    {
                                        (var stopPrice, _) = stopLine.GetPriceAtTime(DateTime.UtcNow, true);
                                        var stopDiff = MainObj.GetPriceDiffPercent(currentDataItem, stopPrice, Order.Position) * 100;
                                        if (leverageDiff == 0 || stopDiff < leverageDiff) //use smallest stop
                                        {
                                            leverageDiff = stopDiff;
                                        }
                                    }
                                }
                                var _floor = AllTrades.FirstOrDefault(x => x.TradeReference == Order.TradeReference && x.Action == TradeAction.FLOOR);
                                if (_floor != null)
                                {
                                    var floorDiff = MainObj.GetPriceDiffPercent(currentDataItem, _floor.Price, Order.Position) * 100;
                                    if (leverageDiff == 0 || floorDiff < leverageDiff) //use smallest stop
                                    {
                                        leverageDiff = floorDiff;
                                    }
                                }

                                if (leverageDiff == 0) //no stop, so use default of whatever it is set to, this would be for a market order generally
                                {
                                    leverageDiff = Leverage;
                                }
                                else //stop found, so leverage the order value as appropriate
                                {
                                    Leverage = MAX_LOSS_PC / leverageDiff;

                                    if (Leverage > MAX_LEVERAGE)
                                    {
                                        LogTrades.WriteLine($"Actual leverage would be {Leverage}, but limited to less than {MAX_LEVERAGE}");   
                                        Leverage = MAX_LEVERAGE;
                                    }
                                }
  
                            }



                            #endregion

                            //With long positions, 1X leverage will merely track the rising value of the underlying asset, so no profit can be taken.
                            //With short positions, 1X is fine as the sell will purchase the underlying asset at a higher level of equity than it was sold for
                            //e.g. Long - market rises by 10%, XBT sold for $100, XBT bought for $110, but BTC value has gone up by 10% anyway
                            //Short - market falls by 10%, XBT sold for $100, XBT bought for $110, so more XBT purchased than was originally sold
                            if (Entry.Position == TradePosition.LONG)
                            {
                                if (Leverage < 2)
                                {
                                    Leverage = 2;
                                }
                            }
                            try
                            {
                                MainObj.SetLeverageMenuState((int)Leverage); //NOTE, calling Application.Current.Dispatcher.Invoke() within this thread causes a lock condition
                            }
                            catch { }

                            //the correct order quantity is determined by leverage
                            if (bitmexAPI.SetLeverage(symbolName, Leverage) == false)
                            {
                                _KillOrder("Failed to set leverage");
                                break;
                            }

                            var orderQuantity = wallet.Amount / XBTdivisor * lastQuoteValue * 0.95; //arbitrarily use 95%

                            orderQuantity *= Entry.Position == TradePosition.LONG ? 1 : -1;
                            orderQuantity = BitMEXApi.GetTickSize(symbolName, orderQuantity);

                            if (TradeEngine.RESTRICTED_TRADE_VALUE > 0)
                            {
                                orderQuantity = Entry.Position == TradePosition.LONG ? TradeEngine.RESTRICTED_TRADE_VALUE : -TradeEngine.RESTRICTED_TRADE_VALUE;
                            }
                            if (TradeEngine.EXCHANGE == Shared.Enums.ExchangeId.BITMEX && MainObj.BACKTEST == true)
                            {
                                //testnet trading - always limit to $100
                                orderQuantity = Entry.Position == TradePosition.LONG ? 100 : -100;
                            }
                            
                            //if leverage is 2X and you want to spend $100, you need to buy / sell $200
                            orderQuantity *= Leverage;
                            //must be multiple of lot size which is $100
                            orderQuantity = (int)(orderQuantity / 100) * 100;

                            Order.Leverage = Entry.Leverage = Leverage;

                            if (USE_SIMULATED_EXCHANGE)
                            {
                                EnterTradeOnSystem_synchronised((decimal)lastQuoteValue, orderQuantity, TradeExchangeStatus.ENTERED_ON_EXCHANGE);
                            }
                            else
                            {
                                Order.ExchangeStatus = Entry.ExchangeStatus = TradeExchangeStatus.ENTERING_ON_EXCHANGE;

                                var orderEntry = bitmexAPI.SendOrder(symbolName, orderQuantity);
                                if (orderEntry != null)
                                {
                                    var orderValid = true;
                                    var orderError = string.Empty;
                                    if (!string.IsNullOrEmpty(orderEntry.OrderStatus) && orderEntry.OrderStatus != "Filled")
                                    {
                                        orderValid = false;
                                        orderError += $"Order was not filled: {orderEntry.OrderRejectionReason}\n";
                                    }
                                    //the returned orderQuantity will always be positive, but for short positions we need to save it as the original quantity (sign) so stops will be correctly signed
                                    if (!string.IsNullOrEmpty(orderEntry.OrderRejectionReason))
                                    {
                                        orderValid = false;
                                        orderError += $"Order was rejected: {orderEntry.OrderRejectionReason}\n";
                                    }
                                    if (orderEntry.AveragePrice == 0)
                                    {
                                        orderValid = false;
                                        orderError += "Order returned zero price\n";
                                    }
                                    if (orderEntry.OrderQuantity == 0)
                                    {
                                        orderValid = false;
                                        orderError += "Order returned zero cumulative quantity\n";
                                    }

                                    if (!orderValid)
                                    {
                                        _KillOrder(orderError, true);
                                        break;
                                    }
                                    var pos = Entry.Position == TradePosition.LONG ? "Long" : "Short";
                                    LogTrades.WriteLineOrder($"\n{pos} order FILLED for {Entry.SymbolName} at {DateTime.UtcNow}");

                                    Order.ItemId = Entry.ItemId = orderEntry.OrderID;

                                    EnterTradeOnSystem_synchronised((decimal)orderEntry.AveragePrice, orderQuantity, TradeExchangeStatus.ENTERED_ON_EXCHANGE);
                                }
                            }
                            EndEntryOrCloseScript();

                            //set stop
                            #region Get liquidation price (not always immediately available) unused
                            /*
                    double liquidationPrice;
                    if (USE_SIMULATED_EXCHANGE)
                    {
                        liquidationPrice = Entry.Position == TradePosition.LONG ? Entry.Price * 0.8 : Entry.Price / 0.8 ;
                    }
                    else
                    {
                        try
                        {
                            //exchange doesn't always return open position straight away, which results in no stop being set.
                            //providing order has been accepted, should always set stop anyway. Will have exited this loop before now if order was not accepted.
                            //liquidation price, however, is not available if the position is not available as this is not returned in the initial order JSON

                            var position = bitmexAPI.GetOpenPositions(symbolName);
                            if (position == null || position.Count == 0)
                            {
                                WriteDeliniatedMessage("Failed to set liquidation stop!!!!");
                                break;
                            }
                            liquidationPrice = (double)position[0].LiquidationPrice;
                        }
                        catch 
                        {
                            WriteDeliniatedMessage("Failed to set liquidation stop (error thrown)!!!!");
                            break;
                        }
                    }

                    Order.LiquidationPrice = Entry.LiquidationPrice = liquidationPrice; //update request already set


                            */
                            #endregion
                       
                            
                            var stop = tradeEngine.GetTradeForTradeReference(TradeAction.STOP, Entry.TradeReference);
                            if (stop != null)
                            {
                                Stop = stop;
                                Stop.ExchangeStatus = TradeExchangeStatus.ENTERING_ON_EXCHANGE; //or will not be entered by base method
                                Stop.Quantity = -Entry.Quantity;
                                SetStopOnExchange_Synchronised(Stop);
                                SetUpdateRequest(Stop);//, TradeUpdateRequest.UPDATE);
                            }
                            else
                            {
                                //add a floor if there is not already one. The stop will be dynamically updated, so should not be dropped, but if there was an error which caused the stop to be misplaced or lost, a default floor is needed to avoid liquidation

                                var floor = tradeEngine.GetTradeForTradeReference(TradeAction.FLOOR, Entry.TradeReference);
                                if (floor != null)
                                {
                                    Floor = floor;
                                }
                                else 
                                { 
                                    Floor = Entry.Clone();

                                    //note, the default stop is not then adjusted for leverage - leverage is just to increase returns, it's the same as trading more money
                                    // so the stop is the same
                                    var stopOffsetPC_Leveraged = DEFAULT_FLOOR_OFFSET_PERCENT;

                                    var floorPrice = Order.Position == TradePosition.LONG
                                                ? Entry.Price / (1 + stopOffsetPC_Leveraged / 100)
                                                : Entry.Price * (1 + stopOffsetPC_Leveraged / 100);

                                    if (floorPrice >= TradeEngine.MAX_SHORT_STOP)
                                    {
                                        floorPrice = MAX_SHORT_STOP;
                                    }

                                    Floor.Price = (double)floorPrice;
                                }

                                Floor.Action = TradeAction.FLOOR;
                                Floor.Quantity = -Entry.Quantity;
                                Floor.ItemId = null; //need a new ID for this as it is a new order.
                                Floor.ExchangeStatus = TradeExchangeStatus.ENTERING_ON_EXCHANGE;

                                SetStopOnExchange_Synchronised(Floor);
                                SetUpdateRequest(Floor);//, TradeUpdateRequest.UPDATE);
                            }
                        }
                        break;

                    case CLOSE_ORDER_SCRIPT:

                        if (Close == null)
                        {
                            _KillOrder($"No Close set in CLOSE_ORDER_SCRIPT for {symbolName}, cannot close trade");
                            break;
                        }

                        LogTrades.WriteLine($"Closing order(s) for {symbolName}");

                        if (USE_SIMULATED_EXCHANGE)
                        {
                            //remove any floor
                            _CancelStop(Floor);
                            _CancelStop(Stop);
                            Entry.ExchangeMethodScript = Close.ExchangeMethodScript = null;
                            SetUpdateRequest(Entry);//, TradeUpdateRequest.UPDATE);
                            (var v2, var s1) = GetValueForSymbol(symbolName);
                            LogTrades.WriteLine("Closing order on simulated exchange");
                            CloseTradeOnSystem_synchronised((decimal)v2);
                        }
                        else
                        {
                            var orderClose = bitmexAPI.CloseOrder(symbolName);
                            if (orderClose != null)
                            {
                                if (!string.IsNullOrEmpty(orderClose.OrderRejectionReason))
                                {
                                    _KillOrder($"Order was rejected:\n{orderClose.OrderRejectionReason}", false);
                                    break;
                                }
                            }

                            //remove any floor and stop from the exchange
                            //just delete all orders to be safe, this ensures no stops left behind.
                            bitmexAPI.DeleteAllOrders(symbolName);

                            if (orderClose != null)
                            {
                                if (orderClose.OrderStatus == "Canceled")
                                {
                                    LogTrades.WriteLine($"{orderClose.Text}");
                                    LogTrades.WriteLine($"Order was probably closed manually on exchange, closing trade on system");
                                    Entry.ExchangeMethodScript = CLOSED_ON_EXCHANGE;
                                    SetUpdateRequest(Entry);//, TradeUpdateRequest.UPDATE);
                                    LogTrades.WriteLine("Closing order as order status was Cancelled");
                                    CloseTradeOnSystem_synchronised((decimal)Entry.Price);
                                }
                                else if (orderClose.AveragePrice != null)
                                {
                                    LogTrades.WriteLine("Closing order with average price");
                                    CloseTradeOnSystem_synchronised((decimal)orderClose.AveragePrice);
                                }
                            }

                            LogTrades.WriteLine($"**************************************************************************************\n");
                        }
                        break;
                }
            }
        }

        #region Stop 
        protected override async void SetStopOnExchange_Synchronised(_Trade stop)
        {

            base.SetStopOnExchange_Synchronised(stop);

            if (USE_SIMULATED_EXCHANGE)
            {
                return;
            }

            var s = $"{stop.Type} ";
            s += stop.Action == TradeAction.STOP ? "stop" : "floor";

            if (stop.ExchangeStatus == TradeExchangeStatus.ENTERED_OFF_EXCHANGE && stop.Type != TradeType.STOP_WAITING)
            {
                LogTrades.WriteLine($"Did not set {s} on exchange, exchange status was {stop.ExchangeStatus} - you need to add a new stop!");
                stop.ExchangeStatus = TradeExchangeStatus.NO_WARNING;
                SetUpdateRequest(stop);//, TradeUpdateRequest.UPDATE);
                return;
            }
            if (stop.Action == TradeAction.WAITING_STOP)
            {
                return;
            }

            //just an extra safety check...
            (var lastValue, var _) = GetValueForSymbol(symbolName);

            if (stop.Position == TradePosition.SHORT && stop.Price < lastValue || stop.Position == TradePosition.LONG && stop.Price > lastValue)
            {
                LogTrades.WriteLine($"Did not set {s} on exchange, price was wrong side of candle data");
                return;
            }

            if (stop.Quantity == 0 && Entry != null)
            {
                //for some reason yet to be determined, the stop does not have an order quantity so is rejected and will continue to be sent like this.
                //also, the floor is missing in this situation
                stop.Quantity = -Entry.Quantity;
                var __s = string.Empty;
                switch (stop.Action)
                {
                    case TradeAction.STOP:
                        __s = "Stop";
                        break;
                    case TradeAction.FLOOR:
                        __s = "Floor";
                        break;
                }
                
                LogTrades.WriteLine($"{__s} quantity was zero, {__s} not yet set, setting {__s}...");
                
            }


            //since this is locked at the ExchangeDataProcessor.ProcessData() level, this method cannot be called again until the stop completes
            //but this is locking the entire system until it returns.

            //if this is a new stop, run synchronously
            //if this is to update the stop price, run asynchronously or will hang the app unpredictably
            //note, the stop may not be an active running trade so may not be the Stop object


            //if trade is not running, then just exit
            //if trade is running, get any existing stop and update. Otherwise, set new stop

            //NOTE - if the quantity is the wrong side, it will trigger an immediate exit.
            //e.g. if a long stop has a positive quantity, this will trigger an immediate sell, the same as if the stop was placed above the current price.
            // not sure why Bitmex does this, 'Close on trigger' will just prevent increasing position size but the stop would therefore be the same as another market order?
            // so surely it would just be rejected?
            void SetStop(_Trade st)
            {
                if (st == null || st.Quantity == 0) //can be zero if attempting to move a closed stop
                {
                    return; //happens with locally placed order
                }
                bool isUpdating = st.ItemId != null;

                 BitMEXApi.Order orderStop = isUpdating
                    ? bitmexAPI.AmendStopOrder(st)
                    : bitmexAPI.SendOrder(symbolName, st.Quantity, null, 0, st.Price);

                if (orderStop != null)
                {
                    if (!string.IsNullOrEmpty(orderStop.OrderRejectionReason))
                    {
                        if (!isUpdating) //if this is a new stop order and it gets rejected, remove the stop. Otherwise leave it with an error message
                        {
                            RemoveTradeFromLine_UI_synchronised(st, false);
                            SetUpdateRequest(st, TradeUpdateRequest.DELETE);

                            //st.ExchangeStatus = TradeExchangeStatus.ENTERED_OFF_EXCHANGE; //just reset to default
                            WriteDeliniatedMessage($"Stop or Floor order was rejected: {orderStop.OrderRejectionReason}");
                        }
                        else
                        {
                            WriteDeliniatedMessage($"Stop or Floor order was not updated: {orderStop.OrderRejectionReason}");
                        }
                    }
                    else
                    {
                        st.ItemId = orderStop.OrderID;

                        //the tick price calculation will return as a double, but the exchange tick price will be rounded for BTC
                        // - this will result in sending a new update request when one is not needed, which will return an error lots of times
                        // - and we want to keep showing errors in the log, so by setting the stop price to the actual stopPx value, this will be avoided
                        st.Price = (double)orderStop.StopTriggerPrice;


                        st.ExchangeStatus = TradeExchangeStatus.ENTERED_ON_EXCHANGE;
                        SetUpdateRequest(st);//, TradeUpdateRequest.UPDATE);
                    }
                }
                else
                {
                    //this happens a lot, about once every 4 mins typically - probably because the stop price is unchanged and a new order is not required.
                    //LogTrades.WriteLine($"Did not set {s} on exchange, orderStop was null at {DateTime.UtcNow:G}");
                }
            }


            if (stop.ExchangeStatus == TradeExchangeStatus.ENTERING_ON_EXCHANGE)
            {
                //trade is entering, run synchronously and allow enclosing InitForDataContext() to save changes
                try
                {
                    SetStop(stop);
                }
                catch (Exception ex)
                {
                }
            }
            else
            {
                //trade is running, update stop asynchronously using a new data context
                //however, this method will be called on both Stop and Floor, so only update the stop as the Floor is always flat
                if (stop.Action == TradeAction.STOP)
                {
                    await Task.Run(() =>
                    {
                        InitForDataContext(() =>
                        {
                            try
                            {
                                SetStop(Stop);
                            }
                            catch (Exception ex) 
                            { 
                            }
                        });

                    });
                }
            }

            return;
        }

        protected override void RemoveStopFromExchange_Synchronised(_Trade stop)
        {
            if (stop == null)
            {
                return;
            }
            if (!(stop.Action == TradeAction.STOP || stop.Action == TradeAction.FLOOR))
            {
                return;
            }

            //if stop or floor is on the exchange, remove it

            var ret = bitmexAPI.DeleteOrder(stop.ItemId);

            if (ret != null && ret.Any() && ret[0].OrderStatus == "Canceled")
            {
                SetUpdateRequest(stop, TradeUpdateRequest.DELETE);

                RemoveTradeFromLine_UI_synchronised(stop);
            }
            else
            {
                //stop may be missing from exchange, or there may be a different stop there which this one cannot delete.
                LogTrades.WriteLine("Failed to remove stop or floor from the exchange, check exchange status!");
            }
        }

        protected override void RemoveTradeBeforeExchangeRemoval_Synchronised(_Trade trade, bool removeLine, bool allowForceRemove)
        {
            if (!allowForceRemove && (trade.Action == TradeAction.STOP || trade.Action == TradeAction.FLOOR))
            {
                return;
            }
            RemoveTradeFromLine_UI_synchronised(trade, removeLine);
            SetUpdateRequest(trade, TradeUpdateRequest.DELETE);
        }
        #endregion

        #region Flip trade
        protected override void DoFlip_Synchronised()
        {
            var flipStop = Stop;// AllTrades.FirstOrDefault(t => t.Type == TradeType.FLIP_ORDER && t.TradeReference == Stop.TradeReference);
            if (flipStop != null)
            {
                //get the current price and the stop price
                var stopPrice = flipStop.Price;
                var position = flipStop.Position == TradePosition.LONG ? TradePosition.SHORT : TradePosition.LONG;

                var wallet = bitmexAPI.GetWalletSingleCurrency(SymbolNames.GetCurrencyForSymbol(symbolName));
                if (wallet != null)
                {
                    //if this has just restarted, will try to enter any trades immediately but the price won't be available from the websocket yet.
                    //so use the REST api instead
                    var lastQuoteValue = bitmexAPI.GetLastPrice(symbolName); //can't get price feed from testnet, only live
                    if (lastQuoteValue != 0)
                    {
                        var diff = Math.Abs(MainObj.GetPriceDiff(stopPrice, lastQuoteValue, Stop.Position) * 100);

                        //determine if this is within an acceptable range

                        bool isWithinRange = true;
                        if (diff > TradeEngine.MAX_FLIP_DIFF_PERCENT) 
                        {
                            LogTrades.WriteLineOrder("Did not flip trade as price difference was too great; converting flip order to a line break order");

                            //so the idea here is if the range is too great, it will just convert flip to a line break then if it returns to that level,
                            // it can enter. Not perfect but a potential trade entry where one would otherwise have been missed.
                            isWithinRange = false;
                            //return;
                        }


                        //re-calculate the order quantity, as the returned amount from the stop should be higher than the previous order
                        var orderQuantity = wallet.Amount / XBTdivisor * lastQuoteValue * 0.95; //arbitrarily use 95%
                        orderQuantity *= flipStop.Position == TradePosition.LONG ? 1 : -1;
                        orderQuantity = BitMEXApi.GetTickSize(symbolName, orderQuantity);

                        //must be multiple of lot size which is $100
                        orderQuantity = (int)(orderQuantity / 100) * 100;

                        //note, the stop quantity is reversed when the order is sent to the exchange, but it is a positive quantity here
                        if (MainObj.BACKTEST)
                        {
                            //using the live price may result in insufficient funds, so only use the lot size here
                            orderQuantity = 100 * (flipStop.Position == TradePosition.LONG ? 1 : -1);
                        }

                        if (bitmexAPI.SetLeverage(symbolName, Leverage) == false)
                        {
                            WriteDeliniatedMessage("Failed to set leverage for flip trade");
                            return;
                        }

                        flipStop.Leverage = Leverage;

                        //get new trade reference for this order
                        var newReference = _Trade.GetNewTradeId();

                        var nextStop = AllTrades.FirstOrDefault(t => t.Type == TradeType.STOP_WAITING && t.TradeReference == Stop.TradeReference);
                        var line = AllLinesForSymbol.FirstOrDefault(x => x.TradeId == flipStop.Id);

                        //update the line trade reference (tradeId will still be valid)
                        if (line != null)
                        {
                            line.TradeReference = newReference;
                            SetUpdateRequest(line);//, TradeUpdateRequest.UPDATE);
                        }

                        //create a new order from the stop, so the Id is still valid for the line
                        var order = flipStop;

                        order.Timestamp = DateTime.UtcNow;
                        order.TradeReference = newReference;
                        order.Type = isWithinRange ? TradeType.MARKET_ORDER : TradeType.LINE_BREAK_ORDER;
                        order.Position = position;
                        order.Quantity = orderQuantity;
                        order.Action = TradeAction.ORDER;
                        order.Price = lastQuoteValue;
                        order.GlobalStatus = TradeGlobalStatus.ACTIVE;
                        order.ItemStatus = TradeItemStatus.OPEN;

                        SetUpdateRequest(order);//, TradeUpdateRequest.UPDATE);

                        //set the next stop from the waiting stop
                        if (nextStop != null)
                        {
                            nextStop.Action = TradeAction.STOP;
                            nextStop.Timestamp = DateTime.UtcNow;
                            nextStop.Type = TradeType.NO_FLIP_ORDER; //because there can't be another stop set yet, so this must not be a flip
                            nextStop.Position = position;
                            nextStop.Quantity = -orderQuantity;
                            nextStop.TradeReference = newReference;
                            nextStop.GlobalStatus = TradeGlobalStatus.ACTIVE;
                            nextStop.ItemStatus = TradeItemStatus.OPEN;
                            SetUpdateRequest(nextStop);//, TradeUpdateRequest.UPDATE);

                            //this will now be the new Stop
                            Stop = nextStop;
                        }

                        //why is entry still open at this point?
                        Entry = null;
                        Close = null;
                        Floor = null;
                        Ceiling = null;
                        Stop = null;

                        LogTrades.WriteLineOrder($"Flipping order if possible...");
                        if (isWithinRange)
                        {
                            (bool hasEntered, bool tradeKilled) = EnterIfPossible_synchronised(order, null);
                            if (tradeKilled)
                            {
                                order.ItemStatus = TradeItemStatus.CLOSED;
                                order.GlobalStatus = TradeGlobalStatus.STOPPED;
                            }
                            if (hasEntered)
                            {
                                Order = order;
                            }
                        }
                    }
                }
            }
        }
        #endregion


        #region Update state from server

        protected override async void UpdateLiveState()
        {
            //return;
            if (USE_SIMULATED_EXCHANGE)
            {
                return;
            }
            await Task.Run(() =>
            {
                //check if live trade is still running on the server. 
                //The position could have been closed and the stop still left running, but this would have been done manually
                //in any case, there should be only one position running at a time
                InitForDataContext(() =>
                {
                    bool _CloseOrderOrUpdateStop(_Trade floorOrStop, string name)
                    {
                        if (floorOrStop == null)
                        {
                            return false;
                        }
                        try
                        {
                            var _order = bitmexAPI.GetOrder(symbolName, floorOrStop.ItemId);
                            if (_order != null)
                            {
                                //order is found, so set state back to default, which will be correct for any stop running on the exchange (not trail, etc.)
                                if (floorOrStop.ExecutionSystem == TradeExecutionSystem.EXCHANGE_UNSYNCHED)
                                {
                                    floorOrStop.ExecutionSystem = TradeExecutionSystem.NORMAL;
                                    SetUpdateRequest(floorOrStop);//, TradeUpdateRequest.UPDATE);
                                }

                                if (_order.OrderStatus != null && _order.OrderStatus == "Filled")
                                {
                                    //stop is filled
                                    LogTrades.WriteLine("Closing order as order status is Filled");
                                    CloseTradeOnSystem_synchronised((decimal)_order.AveragePrice);
                                    EndEntryOrCloseScript();
                                    return true;
                                }
                                else if (_order.OrderStatus != null && _order.OrderStatus == "Canceled") //NOTE THE MISSPELLING!! NOT AN ERROR
                                {
                                    //stop has been removed from exchange manually (cancelled)
                                    WriteDeliniatedMessage($"{name} CANCELLED ON EXCHANGE, REMOVING LOCAL TRADE");
                                    RemoveTradeFromLine_UI_synchronised(floorOrStop, false);
                                    SetUpdateRequest(floorOrStop, TradeUpdateRequest.DELETE);
                                }
                                else
                                {
                                    //just set the price
                                    var orderStop = bitmexAPI.AmendStopOrder(floorOrStop);
                                    //the tick price calculation will return as a double, but the exchange tick price will be rounded for BTC
                                    // - this will result in sending a new update request when one is not needed, which will return an error lots of times
                                    // - and we want to keep showing errors in the log, so by setting the stop price to the actual stopPx value, this will be avoided
                                    if (orderStop != null)
                                    {
                                        floorOrStop.Price = (double)orderStop.StopTriggerPrice;
                                        SetUpdateRequest(floorOrStop);//, TradeUpdateRequest.UPDATE);
                                    }
                                }
                            }

                            else //_order was not found, but this will happen initially as exchange may not have yet registered order (first pass)
                            {
                                if (floorOrStop.ExchangeStatus == TradeExchangeStatus.ENTERED_ON_EXCHANGE)
                                {
                                    //stop was never placed on exchange
                                    //LogTrades.WriteLine($"{name} MISSING FROM OR NOT YET REGISTERED ON EXCHANGE");

                                    floorOrStop.ExecutionSystem = TradeExecutionSystem.EXCHANGE_UNSYNCHED;
                                    SetUpdateRequest(floorOrStop);//, TradeUpdateRequest.UPDATE);
                                }
                            }
                        }
                        catch
                        {
                            //do not close, this could be a connection error so closing would terminate the order
                        }
                        return false;
                    }

                    bool tradeRunning = true;
                    List<Position> openPositions = null;

                    if (Entry != null)
                    {
                        if (!USE_SIMULATED_EXCHANGE)
                        {
                            openPositions = bitmexAPI.GetOpenPositions(symbolName);
                        }
                        //first check for triggered stops, as this will give the correct close price.
                        //if stops untriggered, then close trade locally
                        //then check for stops on exchange, if missing then remove the stops locally. Otherwise, synch price to be safe

                        var floorClosed = false;
                        var stopClosed = false;

                        floorClosed = _CloseOrderOrUpdateStop(Floor, "FLOOR");
                        if (!floorClosed)
                        {
                            stopClosed = _CloseOrderOrUpdateStop(Stop, "STOP");
                        }

                        //stops will be updated with current line price if they are still running, otherwise trade will have been closed on the system
                        if (floorClosed || stopClosed)
                        {
                            //stop has been triggered on exchange, and the order will be closed locally
                            Entry.ExchangeMethodScript = null;
                            SetUpdateRequest(Entry);//, TradeUpdateRequest.UPDATE);

                            if (!floorClosed)
                            {
                                RemoveStopFromExchange_Synchronised(Floor);
                            }
                            if (!stopClosed)
                            {
                                RemoveStopFromExchange_Synchronised(Stop);
                            }

                            //now need to do a flip trade if one is set. However, must have a maximum price deviation to allow for this, 
                            // in case the system has been shut down for some time
                            if (stopClosed && Stop != null && Stop.Type == TradeType.FLIP_ORDER)
                            {
                                LogTrades.WriteLine("Starting flip trade from server update:");
                                DoFlip_Synchronised();

                            }
                            MainObj.DrawCurrentDispatcherDelayed();
                            return;
                        }

                        if (!USE_SIMULATED_EXCHANGE)
                        {
                            try
                            {

                                bool save = false;
                                if (Entry.ExchangeMethodScript == TradeEngine.EXCHANGE_NOT_SYNCHED)
                                {
                                    Entry.ExchangeMethodScript = null;
                                    save = true;
                                }


                                if (openPositions != null && !openPositions.Any())
                                {
                                    //exchange may not return open position immediately after order placement, so wait for a few seconds before displaying a warning message
                                    var timeDiff = DateTime.UtcNow.Subtract(Entry.Timestamp).TotalSeconds;
                                    if (timeDiff > CHECK_EXCHANGE_INTERVAL_SECONDS + 1)
                                    {
                                        WriteDeliniatedMessage("NO OPEN POSITIONS ON EXCHANGE, ALL STOPS HAVE BEEN CLOSED. CHECK EXCHANGE!!!");
                                        Entry.ExchangeMethodScript = TradeEngine.EXCHANGE_NOT_SYNCHED;
                                        bitmexAPI.DeleteAllOrders(symbolName);
                                        tradeRunning = false;
                                        save = true;

                                    }
                                }
                                if (save)
                                {
                                    SetUpdateRequest(Entry);//, TradeUpdateRequest.UPDATE);
                                    MainObj.DrawCurrentDispatcherDelayed();
                                }
                            } 
                            catch (Exception)
                            {
                                //don't close on error
                            }
                        }

                    }

                    if (tradeRunning)
                    {
                        #region Get liquidation price (not always immediately available)

                        //this is done here as it needs to be done only occasionally and not in every update loop, or will get rate limited - 
                        // this happens if trade is placed locally so there is never a liquidation price
                        //This could be done on ItemAdded, but there is already a check for open positions here

                        if (Order != null && Order.LiquidationPrice == 0) //should never be null but this can happen
                        {
                            double liquidationPrice = 0;
                            if (USE_SIMULATED_EXCHANGE)
                            {
                                liquidationPrice = Entry.Position == TradePosition.LONG ? Entry.Price * 0.8 : Entry.Price / 0.8;
                            }
                            else
                            {
                                try
                                {
                                    //exchange doesn't always return open position straight away, which results in no stop being set.
                                    //providing order has been accepted, should always set stop anyway. Will have exited this loop before now if order was not accepted.
                                    //liquidation price, however, is not available if the position is not available as this is not returned in the initial order JSON

                                    if (openPositions != null && openPositions.Count > 0)
                                    {
                                        liquidationPrice = (double)openPositions[0].LiquidationPrice;
                                        Order.LiquidationPrice = Entry.LiquidationPrice = liquidationPrice; //update request already set
                                        SetUpdateRequest(Order);//, TradeUpdateRequest.UPDATE);
                                    }
                                }
                                catch
                                {
                                    WriteDeliniatedMessage("Failed to set liquidation stop (error thrown)!!!!");
                                }
                            }
                        }

                        #endregion
                    }
                });
            });
        }
        #endregion

        #region Get account balances
        //get account balances - this is for display only, it is not part of trade placement
        internal override void GetAccountBalances() 
        {
            UpdateLiveState(); //can do this anyway here
            //var EntryPrice = 30000;
            //var stopPrice =  EntryPrice - (EntryPrice - 20000) * LIQUIDATION_STOP_OFFSET;
            //var trade = new _Trade { Price = EntryPrice };
            //SetStopOnExchange(trade);

            //Balance is the total of XBt + USDt. There is no need to read open orders, they are included in XBt
            //The available balance would just be the total less the open orders
            string errorMsg = string.Empty;
            string resultMsg = string.Empty;
            double totalEquity = 0;
            
            var wallet = bitmexAPI.GetWallet(1);
            if (wallet != null)
            {
                // Total equity
                foreach (var w in wallet)
                {
                    if (w.Currency.ToUpper() == SymbolNames.FIAT_ASSET)
                    {
                        //just the fiat balance
                        totalEquity += w.Amount / fiatDivisor;
                    }
                    else
                    {
                        (var lastQuoteValue, var s1) = GetValueForSymbol(w.Currency.ToUpper(), SymbolNames.GetQuoteAssetForSymbol(symbolName)); //note that the symbol name does not use the fiat asset symbol

                        if (s1 != null)
                        {
                            //errorMsg += $"\n{s1}"; //since we are only trading a single symbol, no ned for any errors for symbols not trading on this system
                        }
                        else
                        {
                            totalEquity += w.Amount / XBTdivisor * lastQuoteValue;
                        }
                    }
                }

                // Individual balances
                foreach (var w in wallet)
                {
                    double value = 0;
                    if (w.Currency.ToUpper() == SymbolNames.FIAT_ASSET)
                    {
                        //just the fiat balance
                        value = w.Amount / fiatDivisor;
                    }
                    else
                    {
                        (var lastQuoteValue, var s1) = GetValueForSymbol(w.Currency.ToUpper(), "USD");

                        if (s1 != null)
                        {
                            //errorMsg += $"\n{s1}";
                        }
                        else
                        {
                            value = w.Amount / XBTdivisor * lastQuoteValue;
                        }
                    }

                    var pc = value / totalEquity * 100;
                    resultMsg += $"{w.Currency}:\t{pc.ToString("0.0")}%\t${value.ToString("0.00")}\t{(w.Amount / XBTdivisor).ToString()}\n";
                }

            }

            //open orders
            double quantity = 0;
            try
            {
                var positions = bitmexAPI.GetOpenPositions(null, 1);
                if (positions != null)
                {
                    foreach (var position in positions)
                    {
                        quantity += Math.Abs((double)position.CurrentQuantity);
                    }
                }
            }
            catch { }

            resultMsg += $"\n------------------------------------\nTotal equity:\t${totalEquity.ToString("0.00")}\n------------------------------------\n";
            resultMsg += $"Open orders:\t${quantity.ToString("0.00")}\n------------------------------------\n";

            var btcEngine = MainObj.GetTradeEngineForSymbol("XBTUSD");
            if (btcEngine != null && btcEngine.currentDataItem != null)
            {
                resultMsg += $"\nLast BTC price:\t${btcEngine.currentDataItem.Close}";
            }

            resultMsg += $"{errorMsg}";

            AlertDialog.Show(resultMsg, "Balances (server also updated)");
        }
        #endregion

    }
}