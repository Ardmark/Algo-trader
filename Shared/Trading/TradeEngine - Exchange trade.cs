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
using System.Windows.Forms;

namespace AlgoTrader.Shared
{
    public partial class TradeEngine : DisplayPanel
    {
        #region Constants

        public static ExchangeId EXCHANGE;
        public static bool USE_SIMULATED_EXCHANGE; //bypasses exchange for price and quantity, and does not set stops or orders on exchange
        public static int MAX_CONCURRENT_TRADES = 1;
        public static bool ALLOW_ALT_TRADING = true;
        public static int MAX_TRADES_PER_24_HOURS;
        public static double RETAINED_BNB_PERCENTAGE;
        public static double MAX_FLIP_DIFF_PERCENT;
        public static double MAX_SHORT_STOP;
       
        public static double Leverage = 1;
        public static double MOVE_STOPS_AMT = 0.5;

        public static double RESTRICTED_TRADE_VALUE = 0;
       // public static double MAX_LOSS_PC = 0;

        #endregion

        #region Fields

        //because the stop and take profit is a double exchange request, need to block this once it starts
        // - can't read from db to get exchange intent, as the system sets that first on a trade, then queries for entry
        // - and EF will return the object just changed, rather than pull the original data from the db if queried for that
        //NOTE - these are unused, this check must be done when running the exchange sequence, rather than in the UI loop, as the trade
        // may not be added or removed from the active order
        public _Trade pendingLiveEntryStop = null;
        public _Trade pendingLiveEntryTP = null;

        public const double TIMEOUT_SECONDS = 10; //timeout for exchange data

        #endregion

        #region Script names
        //script names - rather than enums - because these are stored in the db as strings so are easier to use when debugging
        internal const string QUERY_ORDER_SCRIPT = "Query Order"; //to update a trade with live server data
        internal const string NEW_ORDER_SCRIPT = "New Order";
        internal const string CLOSE_ORDER_SCRIPT = "Close Order";
        internal const string CANCEL_ORDER_SCRIPT = "Cancel Order"; //to cancel a stop, tp or floor (or OCO)
        internal const string CLOSED_ON_EXCHANGE = "Closed on exchange";
        internal const string EXCHANGE_NOT_SYNCHED = "Exchange not synched"; //this is just used if the exchange returns no open trades, it is just for drawing

        //binance - specific
        internal const string SET_STOP_SCRIPT = "Set Stop";
        internal const string CANCEL_STOP_SCRIPT = "Cancel Stop";
        internal const string UPDATE_LIVE_STATE_SCRIPT = "Update live state";
        #endregion

        //The entry point for all trades
        //This method is only called from a single point, which is within a synchronised block.
        //So it is vital that this does NOT start another synchronised section.
        public virtual void StartEntryOrClose_Synchronised() { }

        //this is only overridden in the Binance class, to clear the account data object
        protected virtual void KillEntryOrCloseOrder(bool interrupted = false)
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
                SetUpdateRequest(trade);//, TradeUpdateRequest.UPDATE);
            }
            //DequeueOrder_synchronised();
        }

        //ditto
        protected virtual void EndEntryOrCloseScript()
        {
            //immediate market orders placed through UI will not have set ActiveTrades (Bitmex) yet, so clear these first
            if (Order != null)
            {
                Order.ExchangeMethodScript = null;
                Order.ExchangeMethodIndex = 0;
                SetUpdateRequest(Order);//, TradeUpdateRequest.UPDATE);
            }
            if (Entry != null)
            {
                Entry.ExchangeMethodScript = null;
                Entry.ExchangeMethodIndex = 0;
                SetUpdateRequest(Entry);//, TradeUpdateRequest.UPDATE);
            }
            foreach (var trade in ActiveTrades)
            {
                trade.ExchangeMethodScript = null;
                trade.ExchangeMethodIndex = 0;
                SetUpdateRequest(trade);//, TradeUpdateRequest.UPDATE);
            }

            //DequeueOrder_synchronised();
        }

        protected virtual void SetStopOnExchange_Synchronised(_Trade stop) 
        {
            stop.Timestamp = DateTime.UtcNow;
            SetUpdateRequest(stop);//, TradeUpdateRequest.UPDATE); //to prevent timeout from triggering
        }
        protected virtual void RemoveStopFromExchange_Synchronised(_Trade stop)
        {
           
        }
        //get account balances - this is for display only, it is not part of trade placement
        internal virtual void GetAccountBalances() { }

        #region Update live state from exchange 

        
        System.Timers.Timer timer = null;
        public const double CHECK_EXCHANGE_INTERVAL_SECONDS = 30; 
        public void InitLiveUpdateTimer()
        {
            if (timer == null)
            {
                try
                {
                    timer = new System.Timers.Timer(CHECK_EXCHANGE_INTERVAL_SECONDS * 1000)
                    {
                        Enabled = true
                    };
                    timer.Elapsed += new System.Timers.ElapsedEventHandler(Timer_Elapsed);

                }
                catch { }
            }
            else
            {
                if (timer.Enabled == false)
                {
                    timer.Start(); //won't hurt in case it stopped for some reason
                }
            }
        }

        public void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            UpdateLiveState();
        }
        #endregion

        protected virtual void UpdateLiveState() { }

        #region Utility methods

        protected static (double value, string msg) GetValueForSymbol(string baseAsset, string quoteAsset)
        {
            return GetValueForSymbol(baseAsset + quoteAsset);
        }

        protected static (double value, string msg) GetValueForSymbol(string symbolName)
        {
            var tradeEngine = MainObj.GetTradeEngineForSymbol(symbolName);
            if (tradeEngine == null)
            {
                return (0, $"{symbolName} not trading on system");
            }
            else
            {
                if (tradeEngine.currentDataItem == null)
                {
                    return (0, $"Waiting for connection on {symbolName}");
                }
            }
            return (tradeEngine.currentDataItem.Close, null);
        }
        #endregion
      
        #region Binance-specific methods
        protected virtual void Binance_InitEvents() { } //just sets an event handler
        protected virtual void Binance_StartSimulation() { }
        #endregion

    }
}