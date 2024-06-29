using AlgoTrader.Shared;
using AlgoTrader.Shared.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using _Trade = AlgoTrader.Shared._Trade;

namespace AlgoTrader.Backtest.Backtesting
{
    public static class Backtest_execution
    {
        #region Fields
        //public static Order_Auto autoOrderIndicator;
        public static Order_PriceBreak priceBreakOrderIndicator;
        public static Backtest_base currentTradeIndicator;
        public static Backtest_AutoOrder backtestAutoOrderObj;
        public static Backtest_MarketOrder backtestMarketOrderObj;
        public static TradeEngine tradeEngine;
        #endregion

        #region Properties
        public static bool IsRunningAllBacktests { get; set; }
        public static bool IsRunningBatchBacktest { get; set; }

        public static bool BacktestPaused
        {
            get
            {
                return backtestAutoOrderObj.BacktestPaused || backtestMarketOrderObj.BacktestPaused;
            }
        }
        #endregion

        #region Init
        static Backtest_execution()
        {
            backtestAutoOrderObj = new Backtest_AutoOrder(MainObj.mainViewModel);
            backtestMarketOrderObj = new Backtest_MarketOrder(MainObj.mainViewModel);
        }

        #endregion

        #region Start backtests

        //this is the only entry point for backtesting
        internal static void StartSingleBacktest(_Trade trade)
        {
            if (!MainObj.IsBacktesting)
            {
                //Chart.ScaleBars = 60; //to speed up drawing
                currentTradeIndicator = GetBacktestObject();//trade.ExecutionSystem);
                currentTradeIndicator.StartSingleBacktest(trade);
            }
        }
        internal static async void StartBacktestOnSymbol()//TradeExecutionSystem ex)
        {
            if (!MainObj.IsBacktesting)
            {
                currentTradeIndicator = GetBacktestObject();//ex);
                await currentTradeIndicator.StartAllBacktests(MainObj.visibleTradeEngine.symbolName);
            }
        }

        internal static async void StartAllBacktests()// TradeExecutionSystem ex)
        {
            if (!MainObj.IsBacktesting)
            {
                try
                {
                    currentTradeIndicator = GetBacktestObject();// ex);
                    await currentTradeIndicator.StartAllBacktests();
                }
                catch { }
            }
        }

        internal static async Task StartBatchBacktest()// TradeExecutionSystem ex)
        {
            if (!MainObj.IsBacktesting)
            {
                currentTradeIndicator = GetBacktestObject();// ex);
                await currentTradeIndicator.StartBatchBacktest();
            }
        }

        #endregion

        #region Run backtests

        public static void StepBacktest()
        {
            return; //this has happended during percent dragging when hitting wrong key

#pragma warning disable CS0162
            if (MainObj.IsBacktesting)
            {
                return;
            }
            backtestMarketOrderObj.StepBacktest();
        }

        public static void ResumeBacktest()
        {
            if (currentTradeIndicator != null)
            {
                currentTradeIndicator.ResumeBacktest();
            }
        }
        #endregion

        #region Stop and clear backtests
        public static void PauseBacktest()
        {
            if (currentTradeIndicator != null)
            {
                currentTradeIndicator.PauseBacktest();
            }
        }

        public static void StopBacktest()
        {
            if (currentTradeIndicator != null)
            {
                using (DataContext dbBacktest = new DataContext())
                {
                    currentTradeIndicator.StopBacktest(dbBacktest);
                }
            }

            currentTradeIndicator = null;

        }


        #endregion

        internal static Backtest_base GetBacktestObject()//TradeExecutionSystem ex)
        {
            //set the correct objects to process the orders, based on the visible trade engine
            tradeEngine = MainObj.visibleTradeEngine;
            priceBreakOrderIndicator = tradeEngine.priceBreakOrderIndicator;

            //return the backtest object to generate the orders
            //return ex == TradeExecutionSystem.MANUAL ? backtestMarketOrderObj : (Backtest_base)backtestAutoOrderObj;
            return backtestMarketOrderObj;
        }

        internal static bool BacktestsAvailable(TradeExecutionSystem ex)
        {
            var currentIndicator = GetBacktestObject();// ex);
            return currentIndicator.BacktestsAvailable();
        }

        #region Unused for some reason
        /*
         
        public bool BacktestRunning
        {
            get
            {
                return backtestAutoOrderObj.BacktestRunning || backtestMarketOrderObj.BacktestRunning || IsRunningAllBacktests || IsRunningBatchBacktest;
            }
        }



        
        bool StartBacktest(Trade trade)
        {
            if (BacktestRunning || IsRunningAllBacktests || IsRunningBatchBacktest)
            {
                return true;
            }

            StopAndClearBacktests(trade.ExecutionSystem);
            currentTradeIndicator = GetBacktestObject(trade.ExecutionSystem);
            return currentTradeIndicator.StartBacktest(false, trade);
        }

        
        //this method is unused but would remove the Order as well as the backtests themselves
        public void DeleteAllBacktests(TradeOrder ex)
        {
            db.Trades.RemoveRange(db.Trades.Where(x =>
                         x.Order == ex &&
                         x.System == TradeSystem.BACKTEST &&
                         x.Type == TradeType.MARKET_BREAK));
            db.SaveChanges();
        }*/
        #endregion
    }
}
