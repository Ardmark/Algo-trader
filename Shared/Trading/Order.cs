using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using AlgoTrader.Shared.TradeIndicators;
using AlgoTrader.Shared.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AlgoTrader.Shared
{
    /*
     * Base class for all Order system objects (market and auto)
     * */
    public abstract partial class Order : DisplayPanel
    {
        #region Fields
        protected bool isInLongTrade = false;
        protected bool isInShortTrade = false;

        #endregion

        #region Properties
        //public Trade ActiveOrder
        //{
        //    get
        //    {
        //        return tradeEngine.activeOrder;
        //    }
        //    set
        //    {
        //        tradeEngine.activeOrder = value;
        //    }
        //}
        #endregion

        #region Init
        public Order(string symbolName, int timeInterval, TradeEngine tradeEngine, Chart chart) : base (symbolName, timeInterval, tradeEngine, chart)
        {
        }

        public override void Init(int timeFrame = 0)
        {
            base.Init(timeFrame);
        }

        public override void InitIndicators(int timeFrame)
        {
            if (!timeIntervalFixed)
            {
                TimeFrame = timeFrame;
            }
        }

        public override void InitTimeFrame(int timeFrame)
        {
            if (!timeIntervalFixed)
            {
                TimeFrame = timeFrame;
            }
            //same as display panel method but these trade indicators are different, and interfaces mess things up
/*
            foreach (var indicator in indicators)
            {
                indicator.SetData();
            }
            foreach (var indicator in indicators)
            {
                foreach (var item in tradeEngine.results)
                {
                    indicator.ItemAdded(item);
                }
            }
            */
        }

        #endregion

        #region Add / update item
        public override void ItemAdded(_SymbolDataItem item, bool canComputeTradeEntry, bool liveItem)
        {
            if (liveItem)
            {
                ItemUpdated(item, true);
            }
        }

        #endregion

        #region Set order (UI)

        internal virtual _Trade SetOrder_UI_synchronised(int timeFrame, TradeType tradeType, TradePosition tradePosition, string symbolName, bool isBacktest, double price, int frameNum = -1, int lineId = -1)
        {

            _Trade ret = null;

            //get the frame to place the market order. If this position is outside of the chart bounds, set it on the last candle
            //only get frame if not already supplied, as for line break order. Otherwise, will be regular break order so use last frame position (unless backtest)
            if (frameNum == -1)
            {
                frameNum = Chart.GetFrameNumFromLastMousePos();
            }

            double orderPrice = -1;

            switch (tradeType)
            {
                case TradeType.PRICE_BREAK_ORDER:
                case TradeType.LINE_BREAK_ORDER:

                    orderPrice = price == 0 ? Chart.GetTradePriceFromLastMouseY() : price;
                    if (!isBacktest)
                    {
                        //if (tradePosition == TradePosition.LONG && orderPrice <= tradeEngine.currentDataItem.Close)
                        //{
                        //    AlertDialog.Show("Cannot set Long break order below current price", "Not possible");
                        //    return null;
                        //}
                        //else if (tradePosition == TradePosition.SHORT && orderPrice >= tradeEngine.currentDataItem.Close)
                        //{
                        //    AlertDialog.Show("Cannot set Short break order above current price", "Not possible");
                        //    return null;
                        //}
                    }
                    break;

                case TradeType.HMA_BREAK_ORDER:

                    var hmaIndicator = MainObj.GetPanelForSymbol(chart.symbolName, typeof(HMA_Indicator));
                    if (hmaIndicator != null)
                    {
                        orderPrice = !isBacktest ? hmaIndicator.GetLastPrice(TimeFrame) : hmaIndicator.GetLastPrice(TimeFrame, frameNum);
                        if (orderPrice == -1)
                        {
                            AlertDialog.Show("No price returned from HMA indicator", "Not possible");
                            return null;
                        }
                        if (orderPrice <= tradeEngine.currentDataItem.Close)
                        {
                            AlertDialog.Show("Cannot set HMA break order below current price", "Not possible");
                            return null;
                        }
                    }
                    break;
            }

            if (orderPrice == -1)
            {
                return null;
            }

            try
            {
                DateTime timeStamp = isBacktest || frameNum != -1 ? Chart.GetTimestampFromFrameNum(frameNum) : DateTime.UtcNow;

                ret = new _Trade
                {
                    GlobalStatus = isBacktest ? TradeGlobalStatus.STOPPED : TradeGlobalStatus.ACTIVE,
                    ItemStatus = isBacktest ? TradeItemStatus.CLOSED : TradeItemStatus.OPEN,
                    Timestamp = timeStamp,
                    TradeReference = _Trade.GetNewTradeId(),
                    Type = tradeType,
                    Position = tradePosition,
                    Action = TradeAction.ORDER,
                    Price = orderPrice,
                    ActionValue = 0,
                    TimeFrame = TimeFrame,
                    SymbolName = symbolName,
                    ExchangeId = exchangeId,
                    ExecutionSystem = TradeExecutionSystem.NORMAL,
                    ExchangeStatus = TradeExchangeStatus.ENTERED_OFF_EXCHANGE
                };

            }
#pragma warning disable CS0168
            catch (Exception e)
            {
                return null;
            }

            return ret;
        }

        #endregion

        #region Enter trade
        public virtual bool CanEnterTrade_synchronised(_Trade order)
        {
            return false;
        }  

        //anything which needs to be done outside of the stop or take profit handlers
        internal virtual void ExecuteEntry_synchronised()
        {
            //may not be a stop auto-added, could be a floor instead, so test for spike here
            TestForMoveToBE_synchronised();
            
        }

        #endregion

        #region Terminate trade
        public virtual bool TerminateTrade_synchronised(_Trade trade)
        {
            return false;
        }
        #endregion

        #region Utility methods
        protected (double, double) GetPrevCurrPrices(_Trade order)
        {
            var item = tradeEngine.currentDataItem;

            //the Close of the current data item will come directly from the exchange, but there may have been a higher high between this and the prior sent data item
            //so must check the high (and low)
            double currItemPrice = tradeEngine.currentDataItem.Close;// GetMaxValue(order.Position);
            var prevItemPrice = tradeEngine.Results.Last().GetMinValue(order.Position);

            if (MainObj.IsBacktesting)
            {
                var frame = chart.GetFrameForTimestamp(item.Timestamp); //because a backtest trade can be at any point

                if (frame >= tradeEngine.results.Count)
                {
                    //this would happen if a trade was set after startup but before interpolation of missed frames. However, there is also a flag in tradeEngine to block this
                    frame = tradeEngine.Results.Count - 1;
                }
                if (frame < 1)
                {
                    return (-1, -1);
                }

                var lastItem = tradeEngine.results[frame];
                prevItemPrice = lastItem.GetMinValue(order.Position);

                //not sure about this since more changes have been made -- any problems, it is here....
                //the transition from previous to current bar can often not be smooth, with the current bar starting above the line whilst
                // the previous bar ended below it. Therefore, must use the previous bar low if that was lower than the current bar.
                //prevItemPrice = order.Position == TradePosition.LONG
                //                    ? prevItemPrice < currItemPrice ? prevItemPrice : currItemPrice
                //                    : prevItemPrice > currItemPrice ? currItemPrice : currItemPrice;

            }
            return (prevItemPrice, currItemPrice);
        }


        public static double GetBreakEvenPrice(_Trade trade, TradeEngine tradeEngine, double? percent = null)
        {
            //calculate the break-even price, as a small percentage beyond the trade entry
            double beVal = 1 + (percent == null ? TradeEngine.BREAK_EVEN_THRESHOLD / 100 : (double)percent);
            return trade.Position == TradePosition.LONG
                ? trade.Price * beVal
                : trade.Price / beVal; //needs to be smaller with smaller timeframes
        }

        protected bool PriceAboveThreshold(TradePosition position, double price, double thresholdPrice)
        {
            return position == TradePosition.LONG
                ? price > thresholdPrice
                : price < thresholdPrice;
        }

        public static double GetTakeProfitAtPC_Price(_Trade entry, double price)
        {
            return entry.Position == TradePosition.LONG
                            ? entry.Price + (price - entry.Price) * TradeEngine.TAKE_PROFIT_PERCENT_PC / 100
                                    : entry.Price - (entry.Price - price) * TradeEngine.TAKE_PROFIT_PERCENT_PC / 100;
        }

        #endregion

        #region Take profit
        
        /*
         * Set floor
         * This will NOT set a trailing floor, as it sets the action value to zero
         * */
        private _Trade SetFloorStop_synchronised(DateTime timeStamp, double price, TradeAction action)
        {
            _Trade activeOrder = tradeEngine.Order;
            _Trade trade = tradeEngine.Floor;

            int currentFrame = tradeEngine.currentFrame;

            if (trade == null)
            {
                trade = activeOrder.Clone();
            }

            trade.Action = action;
            trade.Timestamp = timeStamp;
            trade.Price = price;
            trade.ActionValue = 0;
            tradeEngine.SetUpdateRequest(trade);
            return trade;
        }

        #endregion

        #region Stops

        internal virtual (bool, bool) ExecuteStop_synchronised(bool itemAdded)
        {
            _Trade activeOrder = tradeEngine.Order;
            _Trade entry = tradeEngine.Entry;
            _Trade floor = tradeEngine.Floor;
            _Trade stop = tradeEngine.Stop;
            List<_ChartLine> lines = tradeEngine.ActiveLines;
            _SymbolDataItem currentDataItem = tradeEngine.currentDataItem;
            int currentFrame = tradeEngine.currentFrame;

            double stopPrice;
            _ChartLine line;            
            
            if (currentFrame > Results.Count)
            {
                return (false, false);
            }
            if (stop.ExchangeMethodScript != null)
            {
                return (false, false); //do nothing with it at this point
            }

            stop.Timestamp = currentDataItem.Timestamp;

            var low = tradeEngine.GetLow(stop);
            var high = tradeEngine.GetHigh(stop);

            //only execute stops on the hour, to avoid wicking out (hopefully)
            // - this cannot happen for ema break order, as that must run on the minute
            if (!(stop.Stop == TradeStop.STOP_TRAIL) && tradeEngine.EXECUTE_STOPS_ON_TIMEFRAME_CLOSE && currentFrame % tradeEngine.STOP_TIMEFRAME != 0)
            {
                return (false, false);
            }

            //var breakEvenPrice = GetBreakEvenPrice(entry);

            /*
             * NOTE - all these stops (except the trails) will NOT execute except on timeframe close, including price adjustments on-screen
             * */
            var oldPrice = stop.Price;
            switch (stop.Stop)
            {
                #region STOP_ON_LINE_BREAK

                case TradeStop.TAKE_PROFIT_PERCENT:

                    break;
                case TradeStop.STOP_ON_LINE_BREAK:

                    stopPrice = 0;
                    line = lines.GetLineForTrade(stop);
                    if (line != null)
                    {
                        (stopPrice, _) = line.GetPriceAtTime(currentDataItem.Timestamp, true);

                        stop.Price = stopPrice;// > stop.Price ? stopPrice : stop.Price; //falling support line must not lower stop price

                        tradeEngine.SetUpdateRequest(stop);

                        //check if stop price is crossed now price is set from line
                        // - in a falling wedge, stop may be initially above entry until after first execution, so must do this after stop moved
                        if (entry.Position == TradePosition.LONG
                                       ? low <= stop.Price
                                       : low >= stop.Price)
                        {
                            return (true, true); 
                        }
                    }
                    break;

                case TradeStop.TAKE_PROFIT_MA_CROSS:

                    //get the moving average
                    var MA_Indicator = MainObj.displayPanelsForSymbol[symbolName].FirstOrDefault(x => x.GetType() == typeof(SMA_Indicator));
                    if (MA_Indicator != null)
                    {
                        //only one moving average
                        var movingAverage = MA_Indicator.movingAverages[0];
                        var results = movingAverage.GetResultsFromDictionary(stop.TimeFrame);
                        var lastResult = results[results.Count - 1]; 
                        stop.Price = lastResult;
                        tradeEngine.SetUpdateRequest(stop);

                        
                        if (entry.Position == TradePosition.LONG
                           ? low <= lastResult
                           : low >= lastResult)
                        {
                            //only trigger stop if the stop price is not the wrong side of the trade, since this is a take profit only
                            bool ret = ((entry.Position == TradePosition.LONG)
                                        ? lastResult > entry.Price
                                        : lastResult < entry.Price);
                            if (ret)
                            {
                                LogTrades.WriteLine("Exiting on MA take profit");
                            }
                            return (ret) ? (true, true) : (false, true);
                        }
                    }
                    break;
                #endregion              

                #region STOP_TRAIL

                case TradeStop.STOP_TRAIL:

                    void TrailStop()
                    {
                        //adjust the stop price - the action value (percentage) can be set below the initial stop price, so only adjust price if the difference
                        // between current price and stop price is greater than action value percentage
                        double diff = MainObj.GetPriceDiffPercent(currentDataItem, stop.Price, stop.Position);
                        if (diff > stop.ActionValue)
                        {
                            //trail max. value is High if backtesting, otherwise market close
                            var limitPrice = tradeEngine.GetHigh(stop);

                            //move the stop price which will still be the same ActionValue percentage below the current close
                            //the action value can be changed manually at any point, to move the trade from weak to strong
                            double newPrice = stop.Position == TradePosition.LONG
                                ? limitPrice * (1 - stop.ActionValue)
                                : limitPrice * (1 + stop.ActionValue);

                            //update the trail price and set time as current -
                            stop.Price = newPrice;
                            tradeEngine.SetUpdateRequest(stop);
                        }
                    }

                    switch (stop.Stop)
                    {
                        case TradeStop.STOP_TRAIL:
                            //trail the stop (always do this, never switch to another stop)
                            TrailStop();
                            break;
                    }

                    //check if stop price is crossed
                    if (entry.Position == TradePosition.LONG
                                    ? low <= stop.Price
                                    : low >= stop.Price)
                    {
                        return (true, true);
                    }

                    return (false, true);

                #endregion
            }

            return (false, oldPrice != stop.Price);
        }


        #endregion

        #region Floor

        internal (bool, bool) ExecuteFloor_synchronised( bool itemAdded)
        {
            _Trade entry = tradeEngine.Entry;
            _Trade floor = tradeEngine.Floor;
            _SymbolDataItem currentDataItem = tradeEngine.currentDataItem;
            int currentFrame = tradeEngine.currentFrame;

            if (currentFrame > Results.Count)
            {
                return (false, false);
            }

            /*
             * Floor logic:
             * If an auto-trail floor is set, this will trail until TradeEngine.MAX_FLOOR_DEPTH is reached above entry price, then will hold
             * If no floor is set, but there is an EMA cross stop set, then (if SET_FLOOR_ON_EMA_CROSS_UP is set) that will insert a floor at a fraction of TradeEngine.MAX_FLOOR_DEPTH above break even,
             *  - originally, this was the auto-set stop, so a floor was needed to avoid a short good trade going negative
             * */

            bool update = false;
            //trail the floor if required
            if (floor.ActionValue > 0) //will be set by FLOOR_TRAIL for a floor position, auto-set
            {
                var pc = MainObj.GetPriceDiff(currentDataItem.Close, entry.Price, entry.Position) * 100;
                if (pc < TradeEngine.FLOOR_TRAIL)
                {
                    if (floor.Position == TradePosition.LONG)
                    {
                        var floorPrice = currentDataItem.Close * (1 - floor.ActionValue / 100);
                        if (floorPrice > floor.Price)
                        {
                            floor.Price = floorPrice;
                            update = true;
                        }
                    }
                    else
                    {
                        var floorPrice = currentDataItem.Close * (1 + floor.ActionValue / 100);
                        if (floorPrice < floor.Price)
                        {
                            floor.Price = floorPrice;
                            update = true;
                        }
                    }
                }
            }


            switch (floor.Stop)
            {
                case TradeStop.TAKE_PROFIT_PERCENT:
                    var newPrice = GetTakeProfitAtPC_Price(entry, currentDataItem.Close);
                    if (newPrice > floor.Price)
                    {
                        floor.Price = newPrice;
                        tradeEngine.SetUpdateRequest(floor);
                    }
                    break;
            }


            //this just acts as a (possibly additional) trailing stop
            var low = tradeEngine.GetLow(entry);
            return (entry.Position == TradePosition.LONG
                            ? low <= floor.Price
                            : low >= floor.Price,
                            update);
        }

        #endregion

        #region Spike and take profit at %


        internal void TestForMoveToBE_synchronised()
        {
            _Trade activeOrder = tradeEngine.Order;
            _Trade entry = tradeEngine.Entry;      

            if (entry == null)
            {
                return;
            }

            var floor = tradeEngine.Floor;
            var stop = tradeEngine.Stop;
            _Trade breakEven = null;// tradeEngine.BreakEven;
            var takeProfit = tradeEngine.TakeProfit;

            int currentFrame = tradeEngine.currentFrame;
            var currTimestamp = tradeEngine.currentDataItem.Timestamp;
            var currClose = tradeEngine.currentDataItem.Close;

            //always set a floor at an arbirary profit level


            if (TradeEngine.TAKE_PROFIT_PERCENT_PC > 0)
            {
                var breakEvenPrice = GetTakeProfitAtPC_Price(entry, tradeEngine.currentDataItem.Close);
                var pcDiff = MainObj.GetPriceDiff(currClose, breakEvenPrice, entry.Position) * 100;
                if (pcDiff > TradeEngine.SET_TAKE_PROFIT_THRESHOLD_PC)
                {
                    //if no floor, add one at b/e
                    if (floor == null)
                    {
                        floor = SetFloorStop_synchronised(currTimestamp, breakEvenPrice, TradeAction.FLOOR);
                        floor.Stop = TradeStop.TAKE_PROFIT_PERCENT;
                    }
                    else
                    {
                        //if floor set below b/e, move it - but don't move if above!!
                        if (activeOrder.Position == TradePosition.LONG ? floor.Price < breakEvenPrice : floor.Price > breakEvenPrice)
                        {
                            floor.Price = breakEvenPrice;
                            floor.Stop = TradeStop.TAKE_PROFIT_PERCENT;
                            tradeEngine.SetUpdateRequest(floor);
                        }
                    }

                    if (stop == null)
                    {
                        stop = SetFloorStop_synchronised(currTimestamp, breakEvenPrice, TradeAction.STOP);
                        stop.Stop = TradeStop.TAKE_PROFIT_MA_CROSS;
                    }
                    else
                    {
                        //if stop set below b/e, move it - but don't move if above!!
                        if (activeOrder.Position == TradePosition.LONG ? stop.Price < breakEvenPrice : stop.Price > breakEvenPrice)
                        {
                            stop.Price = breakEvenPrice;
                            stop.Stop = TradeStop.TAKE_PROFIT_MA_CROSS;
                            tradeEngine.SetUpdateRequest(stop);
                        }
                    }
                }
            }

            #region if enabled, move to break even when some bars are clear
            if (TradeEngine.NUM_BARS_FOR_FLOOR > 0)
            {
                var tradeTimeframe = entry.TimeFrame;
                var numBarsBack = tradeTimeframe % TimeFrame + tradeTimeframe * TradeEngine.NUM_BARS_FOR_FLOOR;
                var startBar = currentFrame - numBarsBack;
                var low = double.MaxValue;
                if (tradeEngine.results.Count > startBar + numBarsBack)
                {
                    for (int i = startBar; i < startBar + numBarsBack; i++)
                    {
                        if (tradeEngine.results[i].Low < low)
                        {
                            low = tradeEngine.results[i].Low;
                        }
                    }

                    low /= 1.005; //reduce the low by half a percent to give a little clearance

                    var minBreakEven = entry.Price * 1.005;
                    if (minBreakEven < low)
                    {
                        if (floor == null)
                        {
                            SetFloorStop_synchronised(currTimestamp, minBreakEven, TradeAction.FLOOR);
                            LogTrades.WriteLine($"Setting dead break even floor for {entry.SymbolName}");
                        }
                        else
                        {
                            if (floor.Price < low && floor.Price < minBreakEven)
                            {
                                floor.Price = minBreakEven;
                                tradeEngine.SetUpdateRequest(floor);//);//, TradeUpdateRequest.UPDATE);
                                LogTrades.WriteLine($"Moving to dead break even floor for {entry.SymbolName}");
                            }
                        }
                    }
                }
            }
            #endregion

            #region Spike
            if (TradeEngine.SPIKE_TRIGGER_PERCENT > 0)
            {
                //if trade has risen by an arbitrary amount within an arbitrary time, set a floor half way within this time period
                //e.g. if trade rises 10% from price x in 1 hour, set floor at half way from x to current price

                //get elapsed time since entry
                var elapsedTime = currTimestamp.Subtract(entry.Timestamp).TotalMinutes;

                //if (elapsedTime > tradeEngine.SPIKE_TRIGGER_FRAMES)
                //{

                int numFrames = elapsedTime < TradeEngine.SPIKE_TRIGGER_FRAMES ? (int)elapsedTime : TradeEngine.SPIKE_TRIGGER_FRAMES;
                if (currentFrame - numFrames > 0) 
                {
                    var prevPrice = Results[currentFrame - numFrames];
                    var pcDiff = -MainObj.GetPriceDiff(prevPrice.Close, currClose, entry.Position) * 100;

                    if (pcDiff >= TradeEngine.SPIKE_TRIGGER_PERCENT)
                    {
                        if (!MainObj.IsBacktesting && !tradeEngine.hasSpiked)
                        {
                            MainObj.PlaySound(MainObj.SPIKE_SOUND);
                            LogTrades.WriteLine($"Spike alert for {symbolName} at {DateTime.UtcNow}");
                            tradeEngine.hasSpiked = true; //just the once, this is set to false when a trade enters (only one point of entry on system)
                        }                        
                                                 
                        //will only set a new stop if in profit, not climbing out of a loss, as that would still exit in loss (has happened)
                        if (entry.Position == TradePosition.LONG && currClose > entry.Price || entry.Position == TradePosition.SHORT && currClose < entry.Price)
                        {
                            if (!MainObj.IsBacktesting)
                            {
                                var posText = entry.Position == TradePosition.LONG ? "Long" : "Short";
                                LogTrades.WriteLineOrder($"Spike triggered for {posText} {symbolName} at {DateTime.UtcNow}");
                            }
                            var stopPrice = activeOrder.Position == TradePosition.LONG
                                ? (currClose + prevPrice.Close) * (tradeEngine.SPIKE_PERCENT / 100)
                                : (currClose + prevPrice.Close) / (tradeEngine.SPIKE_PERCENT / 100);
                            
                            //if no floor, add one at b/e
                            if (floor == null)
                            {
                                SetFloorStop_synchronised(currTimestamp, stopPrice, TradeAction.FLOOR);
                            }
                            else
                            {
                                //if floor set below b/e, move it - but don't move if above!!
                                if (activeOrder.Position == TradePosition.LONG 
                                    ? floor.Price < stopPrice
                                    : floor.Price > stopPrice)
                                {
                                    floor.Price = stopPrice;
                                    floor.Timestamp = tradeEngine.GetTimeStamp();
                                    tradeEngine.SetUpdateRequest(floor);//, TradeUpdateRequest.UPDATE);
                                }
                            }

                            //remove any take profit and break even, these may conflict with a spike
                            if (takeProfit != null)
                            {
                                tradeEngine.SetUpdateRequest(takeProfit, TradeUpdateRequest.DELETE);
                            }
                            if (breakEven != null)
                            {
                                tradeEngine.SetUpdateRequest(breakEven, TradeUpdateRequest.DELETE);
                            }
                        }
                    }
                }
                // }
            }
            #endregion
        }

        #endregion

        #region Weight (not implemented)

        //will return false if the total elapsed time since the last entry is less than a certain amount
        //this is a simple scheme to limit the total number of stop orders which can be placed in a minute
        // - also applies to TP orders, since those might also be auto-assigned at some point
        //and remember that a stop or TP may take 2 orders anyway

        public bool CanStopBePlaced_synchronised(_Trade stop)
        {
            if (!MainObj.TradeRunningLocally(stop))
            {
                if (!CheckForLiveStopTPEntryOK_synchronised())
                {
                    return false;
                }
            }
            return true;
        }


        public bool CheckForLiveStopTPEntryOK_synchronised()
        {

            if (!tradeEngine.dataReady)
            {
                LogTrades.WriteLine(string.Format("{0} not entered LIVE at {1} as candle data still pending.", symbolName, DateTime.UtcNow));
                return false;
            }
            /*
            //may be using a non-static exchangeInfo now, so this would have to be pulled each time anyway
            if (TradeEngine.exchangeInfo == null)
            {
                LogTrades.WriteLine(string.Format("{0} not entered at {1} as exchangeInfo not yet received.", symbolName, DateTime.UtcNow));
                return false;
            }
            //only allow one pending request entry, and if this is happening then obviously nothing else can
            if (pendingLiveEntryTrade != null)
            {
                return false;
            }
            var _trade = db.Trades.Where(x => x.Id == trade.Id).FirstOrDefault();
            if (_trade != null && _trade.ExchangeRequestIntent != ExchangeRequest.COMPLETED)
            {
                LogTrades.WriteLine($"Order rejected as exchange intent was {_trade.ExchangeRequestIntent}");
                return false;
            }
            */
            return true;
        }

#endregion

       
    }
}
