using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using AlgoTrader.Shared.TradeIndicators;
using AlgoTrader.Shared.Windows;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AlgoTrader.Shared
{

    //idea is this class will generate the trade entries as and when requested, which the TradeEngine will use in place of (as well as) MarketBreak Orders 
    public class Order_Auto : Order
    {

        #region Fields
        //public readonly HeikinAshi_Indicator heikinAshiIndicator;
        const int minorTradeTimeframe = 240;
        const int majorTradeTimeframe = 1440;
        const bool AUTO_GENERATE_MAJOR_TRADE = false; //will not auto generate initial major trade if this is false
        #endregion

        #region Init
        public Order_Auto(string symbolName, int timeInterval, TradeEngine tradeEngine, Chart chart) : base(symbolName, timeInterval, tradeEngine, chart)
        {
            //MainObj.AddPanelForSymbol(heikinAshiIndicator = new HeikinAshi_Indicator(tradeEngine, chart), symbolName);

            TimeFrame = majorTradeTimeframe;
            timeIntervalFixed = true;
        }

        public override void InitTimeFrame(int timeFrame)
        {
           // TimeFrame = 240;// timeFrame, this is NOT called during init as this panel is not added in MainObj setup
        }


        #endregion

        /*
         * Group trade system:
         * The group trade (day timeframe) is just a standard order initially
         * When it enters, it just changes to a different TradeAction so it won't get picked up on restart, 
         *  and the activeOrder switches to a new (sub timeframe) trade
         * When the new trade exits, it closes as normal but then re-sets the group trade TradeAction, so it runs as a normal trade again, waiting to enter
         * When the group trade reaches exit condition, it closes along with any sub timeframe trade
         *  at which point, all sub timeframe trades can have the results zeroed, and the total can be summed in the group trade close.
         * Stops, floors and TP's are all treated as a total exit.
         * */
        #region Add item

        public override void ItemUpdated(SymbolDataItem item, DataContext db, bool itemAdded = false)
        {
            if (!AUTO_GENERATE_MAJOR_TRADE)
            {
                return;
            }
            if (!MainObj.AUTO_ORDER)
            {
                return; //to be safe
            }

            base.ItemUpdated(item, db, itemAdded);

            if (itemAdded == false)
            {
                return;
            }

            if (tradeEngine.activeOrder != null)
            {
                return;
            }

            if ((item.Timestamp.Hour * 60) % TimeFrame != 0)
            {
                return;
            }


            var heikinAshiIndicator = MainObj.GetPanelForSymbol(symbolName, typeof(HeikinAshi_Indicator)) as HeikinAshi_Indicator;
            if (heikinAshiIndicator.CanEnterTrade(TimeFrame) > 0)
            {

                Trade order = new Trade
                {
                    //defaults are active
                    TradeId = Trade.GetNewTradeId(),
                    Timestamp = item.Timestamp,
                    Price = item.Close,
                    TimeFrame = TimeFrame,
                    Type = TradeType.AUTO_ORDER,
                    Position = TradePosition.LONG,
                    Action = TradeAction.ORDER,
                    System = TradeSystem.LIVE,
                    ExecutionSystem = TradeExecutionSystem.AUTO,
                    SymbolName = item.SymbolName,
                    ExchangeId = item.ExchangeId,
                    Leverage = 1,

                };

                db.Trades.Add(order);

                //add HA stop
                //var stop = order.Clone();
                //stop.Action = TradeAction.STOP;
                //stop.Stop = TradeStop.STOP_HA;
                //db.Trades.Add(stop);

                db.SaveChanges();

                tradeEngine.SetActiveOrder(order);

                MainObj.DrawCurrentDispatcher(db);

                //tradeEngine.ExecuteTrade(order, db); //for immediate order entry

                //this is untested (ExecuteTrade rolled in to RunTrade() now)
                var frame = tradeEngine.currentFrame;
                tradeEngine.RunTrade(0, false, db);
                tradeEngine.currentFrame = frame;
            }

        }


        public override bool CanEnterTrade(SymbolDataItem item, Trade order, List<ChartLine> lines)
        {
            if (!MainObj.AUTO_ORDER)
            {
                return false;//to be safe
            }

            //todo - this should NOT be creating a new data context, and SaveChanges should be called from within the main trade engine execution loop
            using (DataContext db = new DataContext())
            {
                var heikinAshiIndicator = MainObj.GetPanelForSymbol(symbolName, typeof(HeikinAshi_Indicator)) as HeikinAshi_Indicator;

                //will test here for exit condition, as group trade will be running so no sub-timeframe trade running
                var frame = Chart.GetFrameForTimestamp(item.Timestamp);

                bool canEnter = false;

                //if this is the first test, and entry is indicated, it should be taken.
                //this way, the trade enters immediately on first run
                //Otherwise, it may wait until the next 4-hour (or whatever) candle
                if (order.Variant == TradeVariant.ENTERED_STRONG) //default is ENTERED_STRONG
                {
                    //just in case day entry has been picked incorrectly, kill it if day is down
                    if (heikinAshiIndicator.FallingTrend(majorTradeTimeframe, frame))
                    {
                        var entry = tradeEngine.entry; //db.Trades.Where(x => x.TradeId == order.TradeId && x.Action == TradeAction.ENTRY).FirstOrDefault();

                        //terminate trade entirely
                        order.GlobalStatus = TradeGlobalStatus.STOPPED;
                        order.ItemStatus = TradeItemStatus.CLOSED;

                        db.SaveChanges();

                        //terminate the trade entirely
                        MainObj.RemoveActiveOrder(tradeEngine.activeOrder);
                        tradeEngine.activeOrder = null;
                        MainObj.TradeStatusChanged();
                        AlertDialog.Show("Could not enter trade as day is down");
                        return false;
                    }

                    //change variant, so this check won't be done in future
                    order.Variant = TradeVariant.ENTERED_WEAK;
                    db.SaveChanges();

                    //immediate entry if possible, otherwise will wait for rising sub timeframe trend
                    canEnter = heikinAshiIndicator.RisingTrend(minorTradeTimeframe, frame, null);
                }
                else
                {

                    if (TradeEngine.IsOnTimeBoundary(majorTradeTimeframe, frame) && heikinAshiIndicator.CanExitTrade(majorTradeTimeframe, frame))
                    {
                        //this is the end of the (day) trade so kill everything
                        //this enforces the termination, otherwise the tendency is to hold with some sort of stop 
                        var entry = tradeEngine.entry; // db.Trades.Where(x => x.TradeId == order.TradeId && x.Action == TradeAction.ENTRY).FirstOrDefault();
                        KillGroupTrade(order, entry, db);
                    }
                    else
                    {
                        if (order.Action == TradeAction.ORDER) //to be safe, although the minor trade will execute instead of the group
                        {
                            if (TradeEngine.IsOnTimeBoundary(minorTradeTimeframe, frame) && heikinAshiIndicator.CanEnterTrade(minorTradeTimeframe, frame) > 0)
                            {
                                canEnter = true;
                            }
                        }
                    }
                }

                if (canEnter)
                {
                    var trade = order;
                    if (trade != null)
                    {
                        //first, need to suspend the major trade, and add a new order for the minor trade
                        //then the system will work as normal from the minor trade
                        var minorTrade = trade.Clone();
                        minorTrade.Action = TradeAction.ORDER;
                        minorTrade.TradeId = Guid.NewGuid().ToString();
                        minorTrade.TimeFrame = minorTradeTimeframe;
                        minorTrade.Timestamp = item.Timestamp;
                        db.Trades.Add(minorTrade);

                        trade.Action = order.Action = TradeAction.GROUP_ORDER;

                        db.SaveChanges();

                        tradeEngine.SetActiveOrder(minorTrade); //same symbol name, so will remove order from MainObj active order list as well then replace with minorTrade
                        return true;
                    }
                }
            }
            return false;

        }

        internal override void ExecuteEntry(Trade entry, Trade floor, Trade stop, SymbolDataItem currentDataItem, int currentFrame, DataContext db)
        {

            //base method only tests for spike, which will add a spike stop if required
            //this would need testing if it was to be re-used with this system
            //base.ExecuteEntry(entry, floor, stop, currentDataItem, currentFrame, db);
            var heikinAshiIndicator = MainObj.GetPanelForSymbol(symbolName, typeof(HeikinAshi_Indicator)) as HeikinAshi_Indicator;

            Trade groupTrade = null;
            if (TradeEngine.IsOnTimeBoundary(majorTradeTimeframe, currentFrame) && heikinAshiIndicator.CanExitTrade(majorTradeTimeframe, currentFrame))
            {
                bool ret = TradeEngine.IsOnTimeBoundary(majorTradeTimeframe, currentFrame);
                //heikinAshiIndicator.CanExitTrade(majorTradeTimeframe, currentFrame);
                //this is the end of the (day) trade so kill everything
                //entry will be the sub timeframe entry trade, not the group order

                groupTrade = db.Trades.Where(x => x.Action == TradeAction.GROUP_ORDER && x.GlobalStatus == TradeGlobalStatus.ACTIVE).OrderByDescending(x => x.Timestamp).FirstOrDefault();
                if (groupTrade != null)
                {
                    KillGroupTrade(groupTrade, entry, db);
                }
                else
                {
                    LogTrades.WriteLine("Failed to find group trade (1)");
                }
                return;
            }

            if (TradeEngine.IsOnTimeBoundary(minorTradeTimeframe, currentFrame) && heikinAshiIndicator.CanExitTrade(minorTradeTimeframe, currentFrame))
            {
                if (groupTrade == null)
                {
                    groupTrade = db.Trades.Where(x => x.Action == TradeAction.GROUP_ORDER && x.GlobalStatus == TradeGlobalStatus.ACTIVE).OrderByDescending(x => x.Timestamp).FirstOrDefault();
                    if (groupTrade != null)
                    {
                        //if (entry.Price < currentDataItem.Close)
                        {
                            ExitMinorTrade(groupTrade, entry, db);
                        }
                    }
                    else
                    {
                        LogTrades.WriteLine("Failed to find group trade (2)");
                    }
                }
                
                return;

            }


        }

        //private bool ExitOnMajorTimeframe(int timeFrame)
        //{
        //    //This will be called only on the major time interval
        //    //the results will be synched to the start time on the major time interval (day)
        //    //but we don't want to exit directly, rather exit manually in case trade is opening high, and would hold or set floor
        //    // - this is because trades generally enter on strength so can dip then shoot up
        //
        //    if (timeFrame == majorTradeTimeframe) //to be sure
        //    {
        //        //if backtesting this won't apply, so just exit anyway
        //        if (MainObj.IsBacktesting)
        //        {
        //            return true;
        //        }
        //        
        //    }
        //    return false;
        //}

        //this will terminate the whole trade entirely
        private void KillGroupTrade(Trade groupTrade, Trade entry, DataContext db)
        {
            //close the sub timeframe trade
            if (entry != null)
            {
                tradeEngine.CloseTrade(entry, db); //this will close the trade on the exchnge
            }

           //kill the group trade, if restart on next day entry was required, would need to change action from GROUP_ORDER to ORDER
            groupTrade.GlobalStatus = TradeGlobalStatus.STOPPED;
            groupTrade.ItemStatus = TradeItemStatus.CLOSED;

            //add a close
            var groupClose = groupTrade.Clone();
            groupClose.Action = TradeAction.CLOSE;
            db.Trades.Add(groupClose);
            db.SaveChanges();

            //terminate the trade entirely
            MainObj.RemoveActiveOrder(tradeEngine.activeOrder);
            tradeEngine.activeOrder = null;
            MainObj.TradeStatusChanged();
        }

        private void ExitMinorTrade(Trade groupTrade, Trade entry, DataContext db)
        {
            tradeEngine.CloseTrade(entry, db); //this will close the trade on the exchnge

            //re-start the group trade
            //this way, it will be picked up again on restart
            groupTrade.Action = TradeAction.ORDER;
            MainObj.RemoveActiveOrder(entry);
            tradeEngine.SetActiveOrder(groupTrade);
            db.SaveChanges();

        }

        #endregion




        internal override bool ExecuteStop(Trade order, Trade stop, Trade entry, Trade floor, SymbolDataItem currentDataItem, int currentFrame, bool itemAdded, DataContext db)
        {
            if (currentFrame > Results.Count)
            {
                return false;
            }

            //prev. candle will be down if trade entered on falling doji, so must not exit straight away
            if (currentDataItem.Timestamp.Subtract(entry.Timestamp).TotalMinutes < TimeFrame)
            {
                return false;
            }

            //this is entirely an algorithmic stop so will never be placed on the exchange
            var heikinAshiIndicator = MainObj.GetPanelForSymbol(symbolName, typeof(HeikinAshi_Indicator)) as HeikinAshi_Indicator;
            if (heikinAshiIndicator.CanExitTrade(TimeFrame))
            {
                return true;
            }

            return base.ExecuteStop(order, stop, entry, floor, currentDataItem, currentFrame, itemAdded, db);
        }

        //implement unused abstract method
        public Trade TestForTradeEntry(SymbolDataItem item, int frame = -1, bool backtest = false)
        {
            return null;
        }
    }
}
