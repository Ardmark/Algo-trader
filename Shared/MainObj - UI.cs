using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.ExchangeConnection;
using AlgoTrader.Shared.Interfaces;
using AlgoTrader.Shared.Logging;
using AlgoTrader.Shared.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AlgoTrader.Shared
{
    /*
     * All methods only called from mainViewModel, in response to UI input
     * */
    public static partial class MainObj
    {
        #region MainViewModel-invoked methods

        internal static void CloseAllTrades_UI()
        {
            foreach (var s in SymbolNames.GetSymbolNames())
            {
                TradeEngine tradeEngine = GetTradeEngineForSymbol(s);
                tradeEngine.CancelTrade_UI_synchronised();
                TradeStatusChanged(tradeEngine, false);
            }
            DrawCurrentDispatcher();
        }

        internal static void StopWaitingTrades_UI()
        {
            if (!IsBacktesting)
            {
                foreach (var s in SymbolNames.GetSymbolNames())
                {
                    var tradeEngine = GetTradeEngineForSymbol(s);
                    tradeEngine?.StopWaitingTrades_UI_synchronised();
                    TradeStatusChanged(tradeEngine, false);
                }
                DrawCurrentDispatcher();
            }
        }

        internal static void RemoveWaitingTrades_UI()
        {
#pragma warning disable CS0162
            if (!IsBacktesting)
            {
                foreach (var s in SymbolNames.GetSymbolNames())
                {
                    var tradeEngine = GetTradeEngineForSymbol(s);
                    tradeEngine?.RemoveWaitingTrades_UI_synchronised();
                    TradeStatusChanged(tradeEngine, false);
                }
                DrawCurrentDispatcher();
            }
        }
        
        internal static void RemoveUnallocatedLines_UI()
        {
            visibleTradeEngine.RemoveUnallocatedLines_UI_synchronised();
            DrawCurrentDispatcher();
        }

        internal static void ExtendLines_UI(int numIntervals)
        {
            var nextEarliest = GetNextInterval_UI(DateTime.UtcNow);
            for (int i = 0; i < numIntervals; i++)
            {
                nextEarliest = GetNextInterval_UI(nextEarliest);
            }            
            
            foreach (var s in SymbolNames.GetSymbolNames())
            {
                GetTradeEngineForSymbol(s)?.ExtendLines_UI_synchronised(nextEarliest);
            }
 
            DrawCurrentDispatcher();
        }

        internal static void CleanupTrades()
        {
            //remove closed stops, take profits, and trade lines
            visibleTradeEngine.CleanupTrades_UI_Synchronised();
        }

        internal static void CleanupLines()
        {
            //remove closed stops, take profits, and trade lines
            visibleTradeEngine.CleanupLines_UI_Synchronised();
        }
        internal static void MoveStops(bool up)
        {
            visibleTradeEngine?.MoveStops_UI_Synchronised(up);
        }
        internal static void KillOrders()
        {
            visibleTradeEngine?.KillOrders_UI_Synchronised();
        }
        //internal static void ResetMaxEntryTime_UI()
        //{
        //    if (!IsBacktesting)
        //    {
        //        foreach (var s in SymbolNamesBinance.SymbolNames())
        //        {
        //            GetTradeEngineForSymbol(s)?.ResetEntryTime_UI_synchronised();
        //        }
        //        DrawCurrentDispatcher();
        //    }
        //}

        #region DataContext calling methods
        internal static void GoToNextWaitingSymbol_UI()
        {
            using (DataContext db = new DataContext())
            {
                var waitingSymbols = db.Trades.AsNoTracking().Where(x =>
                x.GlobalStatus == TradeGlobalStatus.ACTIVE &&
                x.Action == TradeAction.ORDER &&
                x.ItemStatus == TradeItemStatus.OPEN)
                    .OrderBy(x => x.SymbolName).ToList();

                if (waitingSymbols.Count() == 0)
                {
                    return;
                }

                int i = 0;
                for (; i < waitingSymbols.Count; i++) 
                {
                    var s = waitingSymbols[i].SymbolName;
                    if (s == CurrentSymbol)
                    {
                        //can have multiple trades waiting on current symbol
                        for (i++; i < waitingSymbols.Count; i++)
                        {
                            s = waitingSymbols[i].SymbolName;
                            if (s != CurrentSymbol)
                            {
                                break;
                            }
                        }
                        break;
                    }
                }
                if (i == waitingSymbols.Count())
                {
                    i = 0;
                }
                ChangeSymbolHandler_UI(waitingSymbols.ElementAt(i));
            }
        }

        internal static int lastClosedSymbolIndex;
        internal static void GoToNextClosedSymbol_UI()
        {
            using (DataContext db = new DataContext())
            {
                var earliest = ActiveTradeSummary.LastClosedTimeToShow;
                var closedSymbols = db.Trades.AsNoTracking().Where(x =>
                    x.Action == TradeAction.CLOSE &&
                    x.Timestamp > earliest)
                    .OrderByDescending(x => x.Timestamp).ToList();

                if (closedSymbols.Count() == 0)
                {
                    return;
                }

                ChangeSymbolHandler_UI(closedSymbols.ElementAt(lastClosedSymbolIndex));

                lastClosedSymbolIndex++;
                lastClosedSymbolIndex %= closedSymbols.Count();
            }
        }

        internal static void GoToNextRunningSymbol_UI()
        {
            using (DataContext db = new DataContext())
            {
                var activeSymbols = db.Trades.AsNoTracking().Where(x =>
                    x.GlobalStatus == TradeGlobalStatus.ACTIVE &&
                    x.ItemStatus == TradeItemStatus.CLOSED &&
                    x.Action == TradeAction.ORDER)
                    .OrderBy(x => x.SymbolName).ToList();

                if (activeSymbols.Count() == 0)
                {
                    return;
                }

                int index = 0;
                foreach (var s in activeSymbols)
                {
                    if (s.SymbolName == CurrentSymbol)
                    {
                        break;
                    }
                    index++;
                }
                index++;
                if (index >= activeSymbols.Count())
                {
                    index = 0;
                }
                ChangeSymbolHandler_UI(activeSymbols.ElementAt(index)); //ElementAt() does not work on IQueryable...
            }
        } 
        #endregion

        internal static void GoToNextSymbol_UI()
        {
            int index = 0;
            foreach (var s in displayPanelsForSymbol)
            {
                if (s.Key == CurrentSymbol)
                {
                    break;
                }
                index++;
            }
            index++;
            if (index >= displayPanelsForSymbol.Count())
            {
                index = 0;
            }
            ChangeSymbolHandler_UI(displayPanelsForSymbol.ElementAt(index).Key);
        }

        internal static void GoToPrevSymbol_UI()
        {
            int index = 0;
            foreach (var s in displayPanelsForSymbol)
            {
                if (s.Key == CurrentSymbol)
                {
                    break;
                }
                index++;
            }
            index -= 2;
            if (index < 0)
            {
                index = displayPanelsForSymbol.Count() - 1;
            }
            ChangeSymbolHandler_UI(displayPanelsForSymbol.ElementAt(index).Key);
        }

        internal static async void RefreshActiveTradeSummary()
        {
            await ActiveTradeSummary.Update(null);
        }
       
        internal static void ChangeSymbol_UI(string symbolName)
        {
            ChangeSymbolHandler_UI(symbolName);
        }

        #endregion
        //change symbol handler and switch to symbol timeframe
        internal static void ChangeSymbolHandler_UI(_Trade trade)
        {
            Chart.OnBeforeSymbolHandlerChange();
            var tradeEngine = GetTradeEngineForSymbol(trade.SymbolName);
            CurrentSymbol = trade.SymbolName; //sets visible chart and tradeEngine
            TradeStatusChanged(tradeEngine);
            TimeFrame = trade.TimeFrame; //will draw
            visibleChart?.ScrollToEnd();
            mainViewModel.SetTimeframeMenuState(timeFrame);

        }
        internal static void ChangeSymbolHandler_UI(string symbolName)
        {
            Chart.OnBeforeSymbolHandlerChange();
            var tradeEngine = GetTradeEngineForSymbol(symbolName);
            CurrentSymbol = symbolName; //sets visible chart and tradeEngine
            ScrollToEnd();
            TradeStatusChanged(tradeEngine);
        }
    }
}
