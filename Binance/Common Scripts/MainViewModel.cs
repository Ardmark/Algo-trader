#if BACKTEST
using AlgoTrader.Backtest.Backtesting;
using AlgoTrader.Backtest.Windows;
#endif
using AlgoTrader.Shared;
using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.ExchangeConnection;
using AlgoTrader.Shared.Interfaces;
using AlgoTrader.Shared.Logging;
using AlgoTrader.Shared.TradeIndicators;
using AlgoTrader.Shared.Windows;
using AlgoTrader.Windows;
using Shared.Trading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

//won't build without the fully-qualified namespace here, referenced in MainWindow.xaml by xmlns:myAttachedProperties
namespace AlgoTrader.Live.ViewModels
{
    public class TextBoxAttachedProperties
    {
        public static bool GetAutoScrollToEnd(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoScrollToEndProperty);
        }

        public static void SetAutoScrollToEnd(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoScrollToEndProperty, value);
        }

        // Using a DependencyProperty as the backing store for AutoScrollToEnd.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AutoScrollToEndProperty =
            DependencyProperty.RegisterAttached("AutoScrollToEnd", typeof(bool), typeof(TextBoxAttachedProperties), new PropertyMetadata(false, AutoScrollToEndPropertyChanged));

        private static void AutoScrollToEndPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textbox && e.NewValue is bool mustAutoScroll && mustAutoScroll)
            {
                textbox.TextChanged += (s, ee) => AutoScrollToEnd(s, ee, textbox);
            }
        }

        private static void AutoScrollToEnd(object sender, TextChangedEventArgs e, TextBox textbox)
        {
            textbox.ScrollToEnd();
        }
    }

    public partial class MainViewModel : INotifyPropertyChanged, IMainViewModel
    {
        #region IMainViewModel implementation
        public void SetItemPrice(_SymbolDataItem item)
        {
            var s = item.Close == 0 ? WAITING_TEXT : PriceFormatter.Format(item.Close); //0 is just a flag for initialisation, see MainObj.ItemUpdated()
            SetProperty("CurrentPrice", s);
        }

        //unused
        public void ExceptionAlert(string shortMsg, string longMsg)
        {
            LogExceptions.WriteLine(string.Format("Exception alert: {0}\n{1}", shortMsg, longMsg));
        }

        public void CurrentSeconds(double seconds)
        {
            var tradeEngine = MainObj.GetTradeEngineForSymbol(SymbolName);
            if (tradeEngine != null)
            {
                tradeEngine.lastCandleCountdown -= seconds;
                var hours = (int)(tradeEngine.lastCandleCountdown / 3600);
                var mins = (int)(tradeEngine.lastCandleCountdown - hours * 3600) / 60;
                var secs = (int)(tradeEngine.lastCandleCountdown - hours * 3600 - mins * 60);
                SetProperty("CountDownText", $"{hours}h {mins}m {secs}s");
            }
        }

        public void TradeStatusChanged(List<_Trade> activeTrades, List<_Trade> waitingTrades, string symbolName, string price)
        {

            var clear = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 255, 255, 255)); try
            {
                CloseAllOrdersEnabled = activeTrades.Any();
                if (price != null)
                {
                    SetProperty("CurrentPrice", price);
                }

                foreach (var v in SymbolMenu)
                {
                    if (v.Items != null)
                    {
                        foreach (var item in v.Items)
                        {
                            if (item is MenuItem)
                            {
                                SetMenuItemState((MenuItem)item);
                            }
                        }
                    }
                    SetMenuItemState(v);
                }
                SetProperty("SymbolName", MainObj.CurrentSymbol);//this needs to be set in Init so this is a 
                SetProperty("ExchangeId", MainObj.CurrentExchange);
            }
            catch { }

            void SetMenuItemState(MenuItem v)
            {
                if (v.Header != null)
                {
                    v.IsChecked = false;
                    v.Background = clear;

                    var activeTrade = activeTrades.FirstOrDefault(x => x.SymbolName == v.Header as string);
                    if (activeTrade != null)
                    {
                        v.IsChecked = true;
                        v.Background = System.Windows.Media.Brushes.LightGreen;
                    }
                    var waitingTrade = waitingTrades.FirstOrDefault(x => x.SymbolName == v.Header as string);
                    if (waitingTrade != null)
                    {
                        v.Background = System.Windows.Media.Brushes.White;
                    }


                }
            }
        }

        void ClearLogs()
        {
            LogBase.ClearLog("LogTrades.txt");
            LogBase.ClearLog("LogConnection.txt");
            DisplayLog("");
        }
        public void DisplayLog(string s)
        {
            SetProperty("LogText", s);
        }
        public void DisplayTradeSummary(string s)
        {
            SetProperty("TradeText", s);
        }


        #endregion

        #region INotifyPropertyChanged implementation
        Type Type;
        public event PropertyChangedEventHandler PropertyChanged;
        public void SetProperty<T>(string propName, T value)
        {
            try
            {
                if (Type == null)
                {
                    Type = GetType();
                }

                Type.GetProperty(propName).SetValue(this, value);
                OnPropertyChanged(propName);
            }
            catch { }
        }


        protected void SetProperty<T>(ref T store, T value, string propName, Action onChanged = null)
        {
            if (EqualityComparer<T>.Default.Equals(store, value))
                return;
            store = value;
            onChanged?.Invoke();
            OnPropertyChanged(propName);
        }


        //rw - this is thread safe
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion

        #region Constants

        const string ENABLE_LIVE_TRADING_TEXT = "Enable live trade entry";
        const string DISABLE_LIVE_TRADING_TEXT = "Disable live trade entry";
        const string RUN_BACKTESTS_FOR_SYMBOL_TEXT = "Backtest symbol";
        const string RUN_BACKTESTS_TEXT = "Backtest all symbols";
        const string RUN_BATCH_BACKTESTS_TEXT = "Batch backtest all symbols";
        const string PAUSE_BACKTEST_TEXT = "Pause backtest";
        const string RESUME_BACKTEST_TEXT = "Resume backtest";
        const string STOP_BACKTEST_TEXT = "Stop backtest";
        const string STOP_BATCH_BACKTESTS_TEXT = "Stop batch backtest";
        const string WAITING_TEXT = "Waiting.............";

        #endregion

        #region Fields
        public MainWindow mainWindow;
        #endregion

        #region Properties

        public ObservableCollection<MenuItem> SymbolMenu { get; set; }
        public string LogText { get; set; }
        public string TradeText { get; set; }
        public bool ConnectionEnabled { get; set; }
        public bool CrossoverChecked { get; set; }
        public bool MarketOrderChecked { get; set; }
        public bool NoEntryChecked { get; set; }
        public bool TimeFrame1_Checked { get; set; }
        public bool TimeFrame2_Checked { get; set; }
        public bool TimeFrame3_Checked { get; set; }
        public bool TimeFrame4_Checked { get; set; }
        public bool ConcurrentOrders1_Checked { get; set; }
        public bool ConcurrentOrders2_Checked { get; set; }
        public bool ConcurrentOrders3_Checked { get; set; }
        public bool ConcurrentOrders4_Checked { get; set; }
        public bool ConcurrentOrders5_Checked { get; set; }
        public bool ConcurrentOrders6_Checked { get; set; }
        public bool ConcurrentOrders7_Checked { get; set; }
        public bool ConcurrentOrders8_Checked { get; set; }
        public bool ConcurrentOrders9_Checked { get; set; }
        public bool ConcurrentOrders10_Checked { get; set; }
        public bool ConcurrentOrders11_Checked { get; set; }
        public bool ConcurrentOrders12_Checked { get; set; }
        public bool ConcurrentOrders13_Checked { get; set; }
        public bool ConcurrentOrders14_Checked { get; set; }
        public bool ConcurrentOrders15_Checked { get; set; }
        public bool ConcurrentOrders16_Checked { get; set; }
        public bool ConcurrentOrders17_Checked { get; set; }
        public bool ConcurrentOrders18_Checked { get; set; }
        public bool ConcurrentOrders19_Checked { get; set; }
        public bool ConcurrentOrders20_Checked { get; set; }

        public bool Leverage1_Checked { get; set; }
        public bool Leverage2_Checked { get; set; }
        public bool Leverage3_Checked { get; set; }
        public bool Leverage4_Checked { get; set; }
        public bool Leverage5_Checked { get; set; }
        public bool Leverage6_Checked { get; set; }
        public bool Leverage7_Checked { get; set; }
        public bool Leverage8_Checked { get; set; }
        public bool Leverage9_Checked { get; set; }
        public bool Leverage10_Checked { get; set; }

        public string RunBacktestForSymbolHeader { get; set; } = RUN_BACKTESTS_FOR_SYMBOL_TEXT;
        public string RunAllBacktestHeader { get; set; } = RUN_BACKTESTS_TEXT;
        public string RunBatchBacktestHeader { get; set; } = RUN_BATCH_BACKTESTS_TEXT;
        public bool PauseBacktestEnabled { get; set; }
        public bool StopBacktestEnabled { get; set; } //need an additional stop item for single backtests
        public string PauseBacktestHeader { get; set; } = PAUSE_BACKTEST_TEXT;
        public bool BacktestResultEnabled { get; set; }
        public string EnableLiveTradingAutoHeader { get; set; } = MainObj.liveTradeEnabledAuto ? DISABLE_LIVE_TRADING_TEXT : ENABLE_LIVE_TRADING_TEXT;
        public string WindowTitle { get; set; }
        public bool SetLongOrderChecked { get; set; }
        public bool SetShortOrderChecked { get; set; }
        public bool SetNoOrdersChecked { get; set; }
        public string ConnectionMenuTitle { get; set; } = "Pause connection";
        public string CurrentPrice { get; set; } = WAITING_TEXT;
        public string CountDownText { get; set; } = "............. ";
        public string SymbolName { get; set; }
        public ExchangeId ExchangeId { get; set; }
        public bool HeikinAshi_Checked { get; set; }
        public bool ShowClosedTrades_Checked { get; set; } = true;
        public bool ShowOnlyActiveTrade_Checked { get; set; } = false;
        public bool ShowFloorTrades_Checked { get; set; } = true;
        public bool ShowLiquidations_Checked { get; set; } = true;
        public bool DrawingMode_Checked { get; set; } = false;
        public bool ShowAllTrades_Checked { get; set; } = true;

        private string progressPercent;
        public string ProgressPercent
        {
            get { return progressPercent; }
            set
            {
                progressPercent = value;
                OnPropertyChanged("ProgressPercent");
            }
        }

        private string progressPercent1;
        public string ProgressPercent1
        {
            get { return progressPercent1; }
            set
            {
                progressPercent1 = value;
                OnPropertyChanged("ProgressPercent1");
            }
        }

        string uiInterval;
        public string UI_Interval
        {
            get
            {
                return MainObj.TimeFrame.ToString() + " min";
            }
            set
            {
                uiInterval = value; //or will be a read only property and won't work
            }

        }

        private bool addStopEnabled;
        public bool AddStopEnabled
        {
            get { return addStopEnabled; }
            set
            {
                addStopEnabled = value;
                OnPropertyChanged("AddStopEnabled");
            }
        }

        private bool closeAllOrdersEnabled;
        public bool CloseAllOrdersEnabled
        {
            get { return closeAllOrdersEnabled; }
            set
            {
                closeAllOrdersEnabled = value;
                OnPropertyChanged("CloseAllOrdersEnabled");

            }
        }
        private bool backupDBEnabled;
        public bool BackupDBEnabled
        {
            get
            {
                return backupDBEnabled;
            }
            set
            {
                backupDBEnabled = value;
                OnPropertyChanged("BackupDBEnabled");
            }
        }
        #endregion

        #region Menu commands  
        #region Orders
        ICommand moveStopsCommand, killOrdersCommand;
        public ICommand MoveStops_Click => moveStopsCommand ?? (moveStopsCommand = new CommandHandler((param) => MoveStops(param), true));
        public ICommand KillOrders_Click => killOrdersCommand ?? (killOrdersCommand = new CommandHandler1(() => KillOrders(), true));

        #endregion

        #region Connection
        ICommand connectionCommand, restartCommand, backupDBCommand;
        public ICommand ConnectionCommand => connectionCommand ?? (connectionCommand = new CommandHandler1(() => ToggleConnection(), true));
        public ICommand RestartCommand => restartCommand ?? (restartCommand = new CommandHandler1(() => RestartConnection(), true));
        public ICommand BackupDBCommand => backupDBCommand ?? (backupDBCommand = new CommandHandler1(async () => await ExchangeConnector.BackupDB(), true));
        #endregion

        #region Time
        ICommand timeframeCommand;
        //when changing the menu timeframes, also remember to change the key bindings in MainWindow.xaml as well
        public ICommand TimeframeCommand => timeframeCommand ?? (timeframeCommand = new CommandHandler((param) => SetTimeframe(param), true));
        #endregion

        #region Leverages
        ICommand leverageCommand;
        //when changing the menu timeframes, also remember to change the key bindings in MainWindow.xaml as well
        public ICommand LeverageCommand => leverageCommand ?? (leverageCommand = new CommandHandler((param) => SetLeverage(param), true));
        #endregion

        #region Concurrent orders
        ICommand concurrentOrdersCommand;
        //when changing the menu timeframes, also remember to change the key bindings in MainWindow.xaml as well
        public ICommand ConcurrentOrdersCommand => concurrentOrdersCommand ?? (concurrentOrdersCommand = new CommandHandler((param) => SetConcurrentOrders(param), true));
        #endregion

        ICommand autoSetTPPercentCommand;
        public ICommand AutoSetTPPercentCommand => autoSetTPPercentCommand ?? (autoSetTPPercentCommand = new CommandHandler1(() => AutoSetTPPercent(), true));

        public void AutoSetTPPercent()
        {
            var ret = double.TryParse(SetOrderAtPriceDialog.Prompt(null, $"Set take profit percent", mouseMoveX, mouseMoveY), out double price);
            if (price < 0)
            {
                price = 0;
            }
            TradeEngine.TAKE_PROFIT_PERCENT_PC = price;
        }
        #region View
        ICommand showHeikinAshiCommand, showClosedTradesCommand, showWaitingTradesCommand, showFloorTradesCommand, showLiquidationsCommand, drawingModeCommand, showAllTradesCommand;
        public ICommand ShowHeikinAshiCommand => showHeikinAshiCommand ?? (showHeikinAshiCommand = new CommandHandler1(() => { ToggleHeikinAshi(); }, true));
        public ICommand ShowClosedTradesCommand => showClosedTradesCommand ?? (showClosedTradesCommand = new CommandHandler1(() => { ToggleShowClosedTrades(); }, true));
        public ICommand ShowWaitingTradesCommand => showWaitingTradesCommand ?? (showWaitingTradesCommand = new CommandHandler1(() => { ToggleShowOnlyActiveTrade(); }, true));
        public ICommand ShowAllTradesCommand => showAllTradesCommand ?? (showAllTradesCommand = new CommandHandler1(() => { ToggleShowAllTrades(); }, true));
        public ICommand ShowFloorTradesCommand => showFloorTradesCommand ?? (showFloorTradesCommand = new CommandHandler1(() => { ToggleShowFloorTrades(); }, true));
        public ICommand ShowLiquidationsCommand => showLiquidationsCommand ?? (showLiquidationsCommand = new CommandHandler1(() => { ToggleLiquidations(); }, true));
        public ICommand DrawingModeCommand => drawingModeCommand ?? (drawingModeCommand = new CommandHandler1(() => { ToggleDrawingMode(); }, true));


        ICommand invertChartCommand;
        public ICommand InvertChartCommand => invertChartCommand ?? (invertChartCommand = new CommandHandler1(() => { InvertChart(); }, true));

        ICommand clearLogWindow;
        public ICommand ClearLogWindow => clearLogWindow ?? (clearLogWindow = new CommandHandler1(() => ClearLogs(), true));

        ICommand changeSymbolCommand;
        public ICommand ChangeSymbolCommand => changeSymbolCommand ?? (changeSymbolCommand = new CommandHandler((param) => ChangeSymbol(param), true));

        ICommand refreshDailyPercent;
        public ICommand RefreshDailyPercent => refreshDailyPercent ?? (refreshDailyPercent = new CommandHandler1(() => MainObj.RefreshActiveTradeSummary(), true));

        ICommand clearClosedTrades;
        public ICommand ClearClosedTrades => refreshDailyPercent ?? (clearClosedTrades = new CommandHandler(async (param) => {
            await ActiveTradeSummary.ClearClosedTrades(param);
            Chart.showClosedTrades = true;
            SetProperty("ShowClosedTrades_Checked", Chart.showClosedTrades);
            MainObj.DrawImmediate();
        }, true));

        ICommand getAccountBalances;
        public ICommand GetAccountBalances => getAccountBalances ?? (getAccountBalances = new CommandHandler1(() => GetBalances(), true));

        #endregion

        #region Run / stop Orders

#if BACKTEST
        ICommand setBacktestTimeframe, runBacktestsForSymbolCommand, runAllBacktestsCommand, runBatchBacktestsCommand, pauseBacktestCommand, stopBacktestCommand;
        public ICommand RunBacktestsForSymbolCommand => runBacktestsForSymbolCommand ?? (runBacktestsForSymbolCommand = new CommandHandler((param) => BacktestSymbol(param), true));
        public ICommand RunAllBacktestsCommand => runAllBacktestsCommand ?? (runAllBacktestsCommand = new CommandHandler((param) => RunAllBacktests(param), true));
        public ICommand RunBatchBacktestsCommand => runBatchBacktestsCommand ?? (runBatchBacktestsCommand = new CommandHandler((param) => RunBatchBacktests(param), true));
        public ICommand PauseBacktestCommand => pauseBacktestCommand ?? (pauseBacktestCommand = new CommandHandler1(() => PauseBacktest(), true));
        public ICommand StopBacktestCommand => stopBacktestCommand ?? (stopBacktestCommand = new CommandHandler1(() => StopBacktest(), true)); //just the stop command because Stop Market is run from menu so that command also stops
        public ICommand SetBacktestTimeframe => setBacktestTimeframe ?? (setBacktestTimeframe = new CommandHandler((param) => SetBacktestTimeframeCommand(param), true)); //just the stop command because Stop Market is run from menu so that command also stops

#endif
        ICommand resetMaxEntryTimeCommand, enableLiveTradingCommand, stopLiveTrades, stopUnenteredOrders, removeUnenteredOrders;
        public ICommand EnableLiveTradingCommand => enableLiveTradingCommand ?? (enableLiveTradingCommand = new CommandHandler1(() => EnableLiveTradingAuto(), true));
        public ICommand StopAllLiveTradesCommand => stopLiveTrades ?? (stopLiveTrades = new CommandHandler1(() => StopAllLiveTrades(), true));
        public ICommand StopUnenteredOrdersCommand => stopUnenteredOrders ?? (stopUnenteredOrders = new CommandHandler1(() => StopUnenteredOrders(), true));
        public ICommand RemoveUnenteredOrdersCommand => removeUnenteredOrders ?? (removeUnenteredOrders = new CommandHandler1(() => RemoveUnenteredOrders(), true));
        #endregion

        ICommand showResults;
        public ICommand ShowResults => showResults ?? (showResults = new CommandHandler((param) => ShowResultsWindow(param), true));

        #endregion

        #region Button commands
        ICommand showHideFloors_Command, showHideLiquidations_Command, cleanupTradesAndLines_Command, cleanupLines_Command, cleanupTrades_Command, extendLines_Command, removeUnallocatedLines_Command, setOrderButton_Command, nextTradeButton_Command, prevTradeButton_Command, scrollButton1_Command, scrollButton2_Command, scrollButton3_Command, scrollButton4_Command, scrollButton5_Command, scrollButton6_Command, scaleButton1_Command, scaleButton2_Command;
        public ICommand SetOrderButton_Click => setOrderButton_Command ?? (setOrderButton_Command = new CommandHandler((param) => ShowSetOrderAtPriceDialog(param), true));
        public ICommand ExtendLine_Click => extendLines_Command ?? (extendLines_Command = new CommandHandler((param) => ExtendLines(param), true));
        public ICommand CleanupLines_Click => cleanupLines_Command ?? (cleanupLines_Command = new CommandHandler1(() => CleanupLines(), true));
        public ICommand CleanupTrades_Click => cleanupTrades_Command ?? (cleanupTrades_Command = new CommandHandler1(() => CleanupTrades(), true));
        public ICommand CleanupTradesAndLines_Click => cleanupTradesAndLines_Command ?? (cleanupTradesAndLines_Command = new CommandHandler1(() => { CleanupTrades(); CleanupLines(); }, true));
        public ICommand ToggleFloor_Click => showHideFloors_Command ?? (showHideFloors_Command = new CommandHandler1(() => { ShowHideFloors(); CleanupLines(); }, true));
        public ICommand ToggleLiquidation_Click => showHideLiquidations_Command ?? (showHideLiquidations_Command = new CommandHandler1(() => { ShowHideLiquidations(); CleanupLines(); }, true));

        public ICommand RemoveUnallocatedLines_Click => removeUnallocatedLines_Command ?? (removeUnallocatedLines_Command = new CommandHandler1(() => RemoveUnallocatedLines(true), true));
        public ICommand Scrollbutton1_Click => scrollButton1_Command ?? (scrollButton1_Command = new CommandHandler1(() => MainObj.ScrollToStart(), true));
        public ICommand Scrollbutton2_Click => scrollButton2_Command ?? (scrollButton2_Command = new CommandHandler1(() => MainObj.ScrollForward(true), true));
        public ICommand Scrollbutton3_Click => scrollButton3_Command ?? (scrollButton3_Command = new CommandHandler1(() => MainObj.ScrollForward(false), true));
        public ICommand Scrollbutton4_Click => scrollButton4_Command ?? (scrollButton4_Command = new CommandHandler1(() => MainObj.ScrollBack(false), true));
        public ICommand Scrollbutton5_Click => scrollButton5_Command ?? (scrollButton5_Command = new CommandHandler1(() => MainObj.ScrollBack(true), true));
        public ICommand Scrollbutton6_Click => scrollButton6_Command ?? (scrollButton6_Command = new CommandHandler1(() => MainObj.ScrollToEnd(), true));
        public ICommand Scalebutton1_Click => scaleButton1_Command ?? (scaleButton1_Command = new CommandHandler1(() => Magnify(false), true));
        public ICommand Scalebutton2_Click => scaleButton2_Command ?? (scaleButton2_Command = new CommandHandler1(() => Magnify(true), true));
        public ICommand PrevTradeButton_Click => prevTradeButton_Command ?? (prevTradeButton_Command = new CommandHandler1(() => GotoNextTrade(false), true));
        public ICommand NextTradeButton_Click => nextTradeButton_Command ?? (nextTradeButton_Command = new CommandHandler1(() => GotoNextTrade(true), true));
        #endregion

        #region Pop-up window for menu
        Window popupMenuWindow = null;

        public void PopupMenuForLine(object sender, MouseButtonEventArgs e)
        {
            int GetChartLineId()
            {
                Line c = sender as Line;
                int.TryParse(c.Name.Substring(1), out int id);
                return id;
            }

            if (popupMenuWindow != null)
            {
                RemovePopupWindow(false);
            }

            var tradeEngine = MainObj.visibleTradeEngine;
            if (IsKeyDown(Key.LeftShift))
            {

                popupMenuWindow = new LineMenuLeftClick(e.GetPosition(MainWindow.instance), MainWindow.instance,

                #region Place order locally
                () =>
                {
                    AlertDialog.ShowCallback("This will set a backtest order.\n(L-shift click to set local non-backtest order)", "Set backtest order?", () =>
                    {
                        (DateTime timeStamp, double price) = MainObj.visibleChart.GetTimeAndPriceFromLastMouse(true);
                        MainObj.visibleTradeEngine.AddBreakToLineLocal_UI_synchronised(GetChartLineId(), TradePosition.LONG, price, true);
                        RemovePopupWindow();
                    });
                },
                () =>
                {
                    AlertDialog.ShowCallback("This will set a backtest order.\n(L-shift click to set local non-backtest order)", "Set backtest order?", () =>
                    {
                        (DateTime timeStamp, double price) = MainObj.visibleChart.GetTimeAndPriceFromLastMouse(true);
                        MainObj.visibleTradeEngine.AddBreakToLineLocal_UI_synchronised(GetChartLineId(), TradePosition.SHORT, price, true);
                        RemovePopupWindow();
                    });
                });
                #endregion

                KeyUp(Key.LeftShift);
                KeyUp(Key.RightShift);
                KeyUp(Key.LeftCtrl);
                KeyUp(Key.RightCtrl);
            }
            else if (IsKeyDown(Key.RightShift))
            {

                popupMenuWindow = new LineMenuShiftClick(e.GetPosition(MainWindow.instance), MainWindow.instance,

                #region Place order locally
                () =>
                {
                    AlertDialog.ShowCallback("This line break order will be placed locally, but will exit on Exchange!\nTo remove without exiting on Exchange, shift-click trade\n(R-shift click for backtest order placement)", "Set local Long Market order?", () =>
                    {
                        (DateTime timeStamp, double price) = MainObj.visibleChart.GetTimeAndPriceFromLastMouse(true);
                        MainObj.visibleTradeEngine.AddBreakToLineLocal_UI_synchronised(GetChartLineId(), TradePosition.LONG, price, false);
                        RemovePopupWindow();
                    });
                },
                () =>
                {
                    AlertDialog.ShowCallback("This line break order will be placed locally, but will exit on Exchange!\nTo remove without exiting on Exchange, shift-click trade\n(R-shift click for backtest order placement)", "Set local Short Market order?", () =>
                    {
                        (DateTime timeStamp, double price) = MainObj.visibleChart.GetTimeAndPriceFromLastMouse(true);
                        MainObj.visibleTradeEngine.AddBreakToLineLocal_UI_synchronised(GetChartLineId(), TradePosition.SHORT, price, false);
                        RemovePopupWindow();
                    });
                });
                #endregion

                KeyUp(Key.LeftShift);
                KeyUp(Key.RightShift);
                KeyUp(Key.LeftCtrl);
                KeyUp(Key.RightCtrl);
            }
            else
            {
                popupMenuWindow = new LineMenu(e.GetPosition(MainWindow.instance), MainWindow.instance,

                #region Add Long break order to line
                () =>
                {
                    tradeEngine.AddBreakToLine_UI_synchronised(GetChartLineId(), TradePosition.LONG);
                    RemovePopupWindow();
                },
                #endregion

                #region Add Short break order to line
                () =>
                {
                    tradeEngine.AddBreakToLine_UI_synchronised(GetChartLineId(), TradePosition.SHORT);
                    RemovePopupWindow();
                },
                #endregion

                #region Add flip stop to line
                () =>
                {
                    tradeEngine.AddStopToLine_UI(GetChartLineId(), TradeType.FLIP_ORDER);
                    RemovePopupWindow();
                },
                #endregion

                #region Add dead stop to line
                () =>
                {
                    tradeEngine.AddStopToLine_UI(GetChartLineId(), TradeType.NO_FLIP_ORDER);
                    RemovePopupWindow();
                },
                #endregion

                #region Add waiting dead stop to line
                () =>
                {
                    tradeEngine.AddStopToLine_UI(GetChartLineId(), TradeType.STOP_WAITING);
                    RemovePopupWindow();
                },
                #endregion

                #region Add take profit to line
                () =>
                {
                    tradeEngine.AddTakeProfitToLine_UI(GetChartLineId(), TradeType.NO_FLIP_ORDER);
                    RemovePopupWindow();
                },
                #endregion

                #region Delete line
                () =>
                {
                    tradeEngine.DeleteLine_UI_synchronised(GetChartLineId());
                    RemovePopupWindow();

                });
            }
            #endregion

            SetPopupMenuWindow(e);
        }

        private void PopupMenuForOrderExecution(object sender, MouseButtonEventArgs e, bool allowClose)
        {
            int GetTradeId()
            {
                Canvas c = sender as Canvas;
                int.TryParse(c.Name.Substring(1), out int id);
                return id;
            }

            if (popupMenuWindow != null)
            {
                RemovePopupWindow();
            }

            var tradeEngine = MainObj.visibleTradeEngine;

            // AlertDialog.Show($"Unimplemented ({new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileName()}, line {new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber()})");

            if (IsKeyDown(Key.LeftShift) || IsKeyDown(Key.RightShift))
            {
                popupMenuWindow = new OrderExecutionMenuShiftClick(e.GetPosition(MainWindow.instance), MainWindow.instance,

                #region Remove order placed locally
                () =>
                {
                    if (TradeEngine.USE_SIMULATED_EXCHANGE)
                    {
                        tradeEngine.RemoveTrade_UI_synchronised(GetTradeId(), true);
                        RemovePopupWindow();
                    }
                    else
                    {
                        AlertDialog.ShowCallback("This will remove a locally placed order, without sending a buy order to the Exchange.\nThis will also force-remove any running trade from the system.", "Remove trade locally?", () =>
                        {
                            tradeEngine.RemoveTrade_UI_synchronised(GetTradeId(), true);
                            RemovePopupWindow();
                        });
                    }
                });
                #endregion

                KeyUp(Key.LeftShift);
                KeyUp(Key.RightShift);
                KeyUp(Key.LeftCtrl);
                KeyUp(Key.RightCtrl);
            }
            else
            {
                popupMenuWindow = new OrderExecutionMenu(e.GetPosition(MainWindow.instance), MainWindow.instance,

                #region Edit trade set
            () =>
            {
                using (DataContext db = new DataContext())
                {
                    var id = GetTradeId();
                    var trade = db.Trades.FirstOrDefault(x => x.Id == id);
                    if (trade != null)
                    {
                        tradeEngine.EditTradeSet_UI(trade);

                        MainObj.DrawImmediate();
                    }
                }
                RemovePopupWindow();
            },
                #endregion

                #region Remove trade
                () =>
                {
                    var id = GetTradeId();
                    bool canDelete = true;
                    using (DataContext db = new DataContext())
                    {
                        var trade = db.Trades.FirstOrDefault(x => x.Id == id);

                        if (trade != null)
                        {

                            if ((trade.ItemStatus == TradeItemStatus.OPEN || trade.GlobalStatus == TradeGlobalStatus.ACTIVE) && trade.ExchangeStatus == TradeExchangeStatus.ENTERED_ON_EXCHANGE)
                            {
                                canDelete = false;
                                if (MessageBox.Show("This is a live trade, delete anyway?", "Remove live Trade", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                                {
                                    canDelete = true;
                                }
                            }
                        }
                    }

                    //this ensures a warning is given before deleting a floor or stop, but an entry will be blocked from deletion before it is closed anyway, with another message
                    if (canDelete)
                    {
                        tradeEngine.RemoveTrade_UI_synchronised(id);
                    }
                    RemovePopupWindow();
                },
                #endregion

                #region Remove trade and line
                () =>
                {
                    tradeEngine.RemoveTradeAndLine_UI_synchronised(GetTradeId());
                    RemovePopupWindow();
                },
                #endregion

                #region Close trade
                () =>
                {
                    if (allowClose)
                    {
                        if (MainObj.visibleTradeEngine.currentDataItem == null)
                        {
                            AlertDialog.Show("Could not close order, system not fully initialised.");
                            return;
                        }

                        using (DataContext db = new DataContext())
                        {
                            var symbolName = tradeEngine.IsEntered(db, GetTradeId());
                            if (symbolName != null && !TradeEngine.USE_SIMULATED_EXCHANGE)
                            {
                                //using MessageBox, non-blocking outside of UI thread
                                if (MessageBox.Show("This is a live trade, close on exchange?", "!!! CLOSE RUNNING TRADE !!!", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                                {
                                    return;
                                }
                            }
                        }

                        tradeEngine.CloseTrade_UI_synchronised(GetTradeId());
                    }
                    else
                    {
                        AlertDialog.Show("", "Can only close trade from Order");
                    }
                    RemovePopupWindow();
                },
                #endregion

                #region Restart trade
                //very problematic, it will only restart the floor but as soon as the screen redraws, the floor value changes dramatically and is reset to this on the exchange
                //also won't restart stops either dead or flip, so needs work, but it really isn't worth it
                //the trade ID stays the same so when you remove the old trade the new one also gets removed
                //Finally there is no warning that the trade is going to start again, so could easily be triggered accidentally
                // - it was really only a development feature anyway.
                //Needs a stack of work to get this operational and it will never be used live anyway on a closed order

                //however, restart was used when entering a trade by price, so that feature has also been dropped (buttons removed)

                //() =>
                //{
                //    tradeEngine.RestartTrade_UI_synchronised(GetTradeId());
                //    RemovePopupWindow();
                //},
                #endregion

                #region Adjust price
                //not working anyway and not valid for order, entry, close etc.
                //() =>
                //{

                //    using (DataContext db = new DataContext())
                //    {
                //        var id = GetTradeId();
                //        var trade = db.Trades.FirstOrDefault(x => x.Id == id);
                //        if (trade != null)
                //        {
                //            tradeEngine.ForceEntry(trade);
                //            //ShowChangePriceDialog(trade);
                //        }
                //    }
                //    RemovePopupWindow();
                //},
                #endregion

                #region Adjust timeframe
                () =>
                {
                    using (DataContext db = new DataContext())
                    {
                        var id = GetTradeId();
                        var trade = db.Trades.FirstOrDefault(x => x.Id == id);
                        if (trade != null)
                        {
                            ShowChangeTimeframeDialog(trade);
                        }
                    }
                    RemovePopupWindow();
                },
                #endregion

                #region Mark as synchronised
                () =>
                {
                    using (DataContext db = new DataContext())
                    {
                        var id = GetTradeId();
                        var trade = db.Trades.FirstOrDefault(x => x.Id == id);
                        if (trade != null)
                        {
                            tradeEngine.MarkAsSynched_UI_synchronised(id);
                        }
                    }
                    RemovePopupWindow();
                },
                #endregion

                #region Start backtest
                () =>
                {
#if BACKTEST
                    using (DataContext dbBacktest = new DataContext())
                    {
                        var tradeId = GetTradeId();
                        var trade = dbBacktest.Trades.FirstOrDefault(x => x.Id == tradeId);
                        if (trade != null)
                        {
                            //tradeEngine.ForceEntry(trade);
                            SetRunningBacktestMenu();
                            Backtest_execution.StartSingleBacktest(trade);
                        }

                        RemovePopupWindow();
                    }
#endif
                },
                #endregion

                () => { } //etc.
                );
            }

            //(popupMenuWindow as OrderExecutionMenu).AllowClose = allowClose;

            SetPopupMenuWindow(e);
        }

        public void PopupMenuForOrderPlacement(object sender, MouseButtonEventArgs e)
        {
            if (popupMenuWindow != null)
            {
                RemovePopupWindow(false);
            }

            var tradeEngine = MainObj.visibleTradeEngine;

            if (IsKeyDown(Key.LeftShift) || IsKeyDown(Key.RightShift))
            {
                popupMenuWindow = new OrderPlacementMenuShiftClick(e.GetPosition(MainWindow.instance), MainWindow.instance,

                #region Place order locally
                () =>
                {
                    AlertDialog.ShowCallback("This Market order will be placed locally, but will exit on Exchange!\nTo remove without exiting on Exchange, shift-click trade", "Set local Market order?", () =>
                    {
                        (DateTime timeStamp, double price) = MainObj.visibleChart.GetTimeAndPriceFromLastMouse(true);
                        MainObj.visibleTradeEngine.SetMarketOrderLocal_UI_synchronised(price, TradePosition.LONG);
                        RemovePopupWindow();
                    });
                },
                () =>
                {
                    AlertDialog.ShowCallback("This Market order will be placed locally, but will exit on Exchange!\nTo remove without exiting on Exchange, shift-click trade", "Set local Market order?", () =>
                    {
                        (DateTime timeStamp, double price) = MainObj.visibleChart.GetTimeAndPriceFromLastMouse(true);
                        MainObj.visibleTradeEngine.SetMarketOrderLocal_UI_synchronised(price, TradePosition.SHORT);
                        RemovePopupWindow();
                    });
                });
                #endregion

                KeyUp(Key.LeftShift);
                KeyUp(Key.RightShift);
                KeyUp(Key.LeftCtrl);
                KeyUp(Key.RightCtrl);
            }
            else
            {
                popupMenuWindow = new OrderPlacementMenu(e.GetPosition(MainWindow.instance), MainWindow.instance,

                #region Set price break order (Long)
                () =>
                {
                    if (MainObj.invertChart)
                    {
                        ShowInvertedChartWarning();
                    }
                    else
                    {
                        (DateTime timeStamp, double price) = MainObj.visibleChart.GetTimeAndPriceFromLastMouse(true);
                        MainObj.visibleTradeEngine.SetLineBreakOrder_UI_synchronised(timeStamp, price, TradePosition.LONG);
                    }

                    RemovePopupWindow();
                },
                #endregion

                #region Set price break order (Short)
                () =>
                {
                    if (MainObj.invertChart)
                    {
                        ShowInvertedChartWarning();
                    }
                    else
                    {
                        (DateTime timeStamp, double price) = MainObj.visibleChart.GetTimeAndPriceFromLastMouse(true);
                        MainObj.visibleTradeEngine.SetLineBreakOrder_UI_synchronised(timeStamp, price, TradePosition.SHORT);
                    }

                    RemovePopupWindow();
                },
                #endregion

                #region Set ceiling take profit
                () =>
                {

                    if (MainObj.invertChart)
                    {
                        ShowInvertedChartWarning();
                    }
                    else
                    {
                        (DateTime timeStamp, double price) = MainObj.visibleChart.GetTimeAndPriceFromLastMouse(true);
                        MainObj.visibleTradeEngine.SetCeilingAtPrice_UI_synchronised(timeStamp, price);
                    }

                    RemovePopupWindow();
                },
                #endregion

                #region Set take profit at percent

                () =>
                {
                    if (MainObj.invertChart)
                    {
                        ShowInvertedChartWarning();
                    }
                    else
                    {
                        (DateTime timeStamp, double price) = MainObj.visibleChart.GetTimeAndPriceFromLastMouse(true);
                        MainObj.visibleTradeEngine.SetTakeProfitAtPercent_UI_synchronised(timeStamp, price);
                    }
                    RemovePopupWindow();
                },
                #endregion

                #region Set floor stop

                () =>
                {
                    if (MainObj.invertChart)
                    {
                        ShowInvertedChartWarning();
                    }
                    else
                    {
                        (DateTime timeStamp, double price) = MainObj.visibleChart.GetTimeAndPriceFromLastMouse(true);
                        MainObj.visibleTradeEngine.SetFloorAtPrice_UI_synchronised(timeStamp, price);
                        Chart.showFloorTrades = true;
                    }
                    RemovePopupWindow();
                },
                #endregion

                #region Set Market order (Long)
                () =>
                {
                    if (MainObj.invertChart)
                    {
                        ShowInvertedChartWarning();
                    }
                    else
                    {

                        if (MainObj.visibleTradeEngine.currentDataItem == null)
                        {
                            AlertDialog.Show("Could not place order, system not fully initialised.");
                        }
                        else
                        {
                            AlertDialog.ShowCallback("This order will execute immediately!", "Set Long Market order?", () =>
                            {
                                try
                                {
                                    MainObj.visibleTradeEngine.SetMarketOrderAtTime_UI_synchronised(DateTime.UtcNow, MainObj.visibleTradeEngine.currentDataItem.Close, TradePosition.LONG);
                                }
                                catch
                                {
                                    AlertDialog.Show("Could not place order.");
                                }
                            });
                        }


                    }

                    RemovePopupWindow();
                },
                #endregion

                #region Set Market order (Short)
                () =>
                {
                    if (MainObj.invertChart)
                    {
                        ShowInvertedChartWarning();
                    }
                    else
                    {

                        if (MainObj.visibleTradeEngine.currentDataItem == null)
                        {
                            AlertDialog.Show("Could not place order, system not fully initialised.");
                        }
                        else
                        {
                            AlertDialog.ShowCallback("This order will execute immediately!", "Set Short Market order?", () =>
                            {
                                try
                                {
                                    MainObj.visibleTradeEngine.SetMarketOrderAtTime_UI_synchronised(DateTime.UtcNow, MainObj.visibleTradeEngine.currentDataItem.Close, TradePosition.SHORT);
                                }
                                catch
                                {
                                    AlertDialog.Show("Could not place order.");
                                }
                            });
                        }


                    }

                    RemovePopupWindow();
                },
                #endregion

                #region Set MA Stop
                () =>
                {
                    MainObj.visibleTradeEngine.SetMovingAverageStop_UI_synchronised();
                    RemovePopupWindow();
                },
                #endregion

                #region Set Break Even
                () =>
                {
                    MainObj.visibleTradeEngine.SetTakeProfitAtPercent_UI_synchronised(DateTime.UtcNow, MainObj.visibleTradeEngine.currentDataItem.Close);
                    RemovePopupWindow();
                },
                #endregion

                #region Unused 
                #region Set trailing stop
            () =>
            {
                AlertDialog.ShowCallback("", "Set Trailing Stop position?", () =>
                {
                    if (MainObj.invertChart)
                    {
                        ShowInvertedChartWarning();
                    }
                    else
                    {
                        (DateTime timeStamp, double price) = MainObj.visibleChart.GetTimeAndPriceFromLastMouse(true);
                        MainObj.visibleTradeEngine.SetStopAtPrice_UI_synchronised(timeStamp, price, TradeStop.STOP_TRAIL);
                    }
                });
                RemovePopupWindow();
            }
            #endregion 
            #endregion

            );
            }

            SetPopupMenuWindow(e);
        }


        public void PopupMenuForOrder(object sender, MouseButtonEventArgs e)
        {
            PopupMenuForOrderExecution(sender, e, true);
        }

        //similar method, but no close trade function
        public void PopupMenuForTrade(object sender, MouseButtonEventArgs e)
        {
            PopupMenuForOrderExecution(sender, e, false);
        }

        private void SetPopupMenuWindow(MouseButtonEventArgs e)
        {
            if (popupMenuWindow == null)
            {
                return;
            }
            e.Handled = true;
            popupMenuWindow.Owner = mainWindow;
            var _l = mainWindow.Left;
            var _t = mainWindow.Top;
            var _p = e.GetPosition(mainWindow);
            popupMenuWindow.Top = _t + _p.Y + 45;
            popupMenuWindow.Left = _l + _p.X;
            popupMenuWindow.Show();
        }

        public void RemovePopupWindow(bool returnMouseCapture = true)
        {
            if (popupMenuWindow != null)
            {
                popupMenuWindow.Owner = null;//or Main_Window vanishes behind everything else for some reason...
                popupMenuWindow.Close();
                popupMenuWindow = null;
            }
        }

        #endregion

        #region Init
        public MainViewModel(MainWindow mainWindow)
        {
            MainObj.mainViewModel = this;
            this.mainWindow = mainWindow;
            SetMenuOptions();

            //todo:
            //1) go through symbol menu items, set up lists
            //2) add all items to appropriate lists
            SymbolMenu = new ObservableCollection<MenuItem>();

            var listNames = SymbolNames.SYMBOL_NAMES.GroupBy(x => x.listName);
            foreach (var name in listNames)
            {
                var v = name.Key;
                var subMenu = new MenuItem
                {
                    Header = v,
                };
                foreach (var g in name)
                {
                    subMenu.Items.Add(

                        new MenuItem
                        {
                            Header = g.baseAsset + g.quoteAsset,
                            Command = ChangeSymbolCommand,
                            CommandParameter = g.baseAsset + g.quoteAsset
                        }
                    );
                }

                SymbolMenu.Add(subMenu);
            }

            SymbolMenu.Add(new MenuItem
            {
                Header = "----------",
                IsEnabled = false
            });

            foreach (var symbolName in SymbolNames.GetSymbolNames().OrderBy(x => x))
            {
                SymbolMenu.Add(

                new MenuItem
                {
                    Header = symbolName, //seems to remove leading underscore, if used in symbol name
                    Command = ChangeSymbolCommand,
                    CommandParameter = symbolName
                });


            }
            SetProperty("SymbolName", MainObj.CurrentSymbol);
        }

        public List<DisplayPanel> InitCallback(TradeEngine tradeEngine, Chart chart)
        {
            return new List<DisplayPanel>
            {
                //this is fine but there is no need for any indicators now...
                //new HMA_Indicator(0, tradeEngine, chart, 100, CandlePosition.CLOSE, 2, System.Windows.Media.Brushes.Blue),
                new SMA_Indicator(0, tradeEngine, chart, 22, CandlePosition.CLOSE, 1, System.Windows.Media.Brushes.Gray),
            };

        }
        //public void Init()
        //{

        //    #region unused display-only Panels

        //    //stochastics
        //    //     AddPanel(new Stochastic_Panel(1, this, indicator1, 14, CandlePosition.CLOSE, 1, Brushes.Blue, 1, Brushes.Red));
        //    //       AddPanel(new Stochastic_Panel(5, this, indicator2, 14, CandlePosition.CLOSE, 1, Brushes.Blue, 1, Brushes.Red));
        //    //       AddPanel(new Stochastic_Panel(15, this, indicator3, 14, CandlePosition.CLOSE, 1, Brushes.Blue, 1, Brushes.Red));
        //    //     AddPanel(new Stochastic_Panel(60, this, indicator4, 14, CandlePosition.CLOSE, 1, Brushes.Blue, 1, Brushes.Red));

        //    //macd
        //    //AddPanel(new MACD_Panel(15, this, indicator2, 14, CandlePosition.CLOSE, 1, Brushes.Blue, 1, Brushes.Red));
        //    //AddPanel(new MACD_Panel(60, this, indicator3, 14, CandlePosition.CLOSE, 1, Brushes.Blue, 1, Brushes.Red));
        //    //AddPanel(new MACD_Panel(15, this, indicator3, 14, CandlePosition.CLOSE, 1, Brushes.Blue, 1, Brushes.Red));


        //    //   AddPanel(new EMA_Panel(0, this, chartCanvas, 5, CandlePosition.HIGH, 1, Brushes.Black));
        //    //   AddPanel(new EMA_Panel(0, this, chartCanvas, 5, CandlePosition.LOW, 1, Brushes.Black));
        //    //     AddPanel(new TradingRange_Panel(this, chartCanvas));

        //    //ribbon
        //    //     

        //    /*    AddPanel(new EMA_Panel(0, this, chartCanvas, 21, CandlePosition.CLOSE, 1, Brushes.Green));
        //       AddPanel(new EMA_Panel(0, this, chartCanvas, 50, CandlePosition.CLOSE, 1, Brushes.Green));
        //       AddPanel(new SMA_Panel(0, this, chartCanvas, 99, CandlePosition.CLOSE, 2, Brushes.Black));
        //       AddPanel(new EMA_Panel(0, this, chartCanvas, 30, CandlePosition.CLOSE, 1, Brushes.Gray));
        //       AddPanel(new EMA_Panel(0, this, chartCanvas, 35, CandlePosition.CLOSE, 1, Brushes.Gray));
        //       AddPanel(new EMA_Panel(0, this, chartCanvas, 40, CandlePosition.CLOSE, 1, Brushes.Gray));
        //       AddPanel(new EMA_Panel(0, this, chartCanvas, 45, CandlePosition.CLOSE, 1, Brushes.Gray));
        //       AddPanel(new EMA_Panel(0, this, chartCanvas, 50, CandlePosition.CLOSE, 1, Brushes.Gray));
        //       */


        //    #endregion

        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        SetTimeframeMenuState(MainObj.TimeFrame);
        //        SetConcurrentOrdersMenuState(TradeEngine.MAX_CONCURRENT_TRADES);
        //        MainObj.ScrollToEnd();
        //    });
        //}

        #endregion

        #region UI methods

        public void KillOrders()
        {
            AlertDialog.ShowCallback("This will terminate all open and running orders on this symbol.", "KILL ALL ORDERS ON THIS SYMBOL?", () =>
            {
                MainObj.KillOrders();
                RemovePopupWindow();
            });
        }
        public void MoveStops(object param)
        {
            int.TryParse(param as string, out int direction);
            switch (direction)
            {
                case 0: //up
                    MainObj.MoveStops(true);
                    break;
                case 1: //down
                    MainObj.MoveStops(false);
                    break;
            }
        }

        private void InvertChart()
        {
            MainObj.invertChart = !MainObj.invertChart; MainObj.DrawImmediate();
        }

        private void GetBalances()
        {
            MainObj.visibleTradeEngine.GetAccountBalances();
        }


        private void ToggleHeikinAshi()
        {
            Chart.ShowHeikinAshi = !Chart.ShowHeikinAshi;
            SetProperty("HeikinAshi_Checked", Chart.ShowHeikinAshi);
            MainObj.DrawImmediate();
        }

        private void ToggleShowClosedTrades()
        {
            Chart.showClosedTrades = !Chart.showClosedTrades;
            SetProperty("ShowClosedTrades_Checked", Chart.showClosedTrades);
            MainObj.DrawImmediate();
        }

        private void ToggleShowOnlyActiveTrade()
        {
            Chart.showOnlyActiveTrade = !Chart.showOnlyActiveTrade;
            SetProperty("ShowOnlyActiveTrade_Checked", !Chart.showOnlyActiveTrade);
            MainObj.DrawImmediate();
        }

        private void ToggleShowAllTrades()
        {
            Chart.showTrades = !Chart.showTrades;
            SetProperty("ShowAllTrades_Checked", !Chart.showTrades);
            MainObj.DrawImmediate();
        }

        private void ToggleShowFloorTrades()
        {
            Chart.showFloorTrades = !Chart.showFloorTrades;
            SetProperty("ShowFloorTrades_Checked", Chart.showFloorTrades);
            MainObj.DrawImmediate();
        }

        private void ToggleLiquidations()
        {
            Chart.showLiquidations = !Chart.showLiquidations;
            SetProperty("ShowLiquidations_Checked", Chart.showLiquidations);
            MainObj.DrawImmediate();
        }
        private void ToggleDrawingMode()
        {
            toggleDraw = !toggleDraw;
            SetProperty("DrawingMode_Checked", toggleDraw);
            if (toggleDraw)
            {
                MainObj.visibleChart.canvas.Cursor = System.Windows.Input.Cursors.Pen;
            }
            else
            {
                MainObj.visibleChart.canvas.Cursor = System.Windows.Input.Cursors.Arrow;
            }
            //MainObj.DrawImmediate();
        }
        //set a live break order on the current visible symbol
        //there is no button to set a SMA order, although all code is in place to do that

        private void RemoveUnallocatedLines(bool param)
        {
            MainObj.RemoveUnallocatedLines_UI();
        }

        private void ExtendLines(object param)
        {
            if (int.TryParse(param as string, out int result))
            {
                AlertDialog.ShowCallback("", "Extend trade lines to next interval?", () =>
                {
                    MainObj.ExtendLines_UI(result);
                });
            }
        }

        private void CleanupTrades()
        {
            MainObj.CleanupTrades();
        }
        private void CleanupLines()
        {
            MainObj.CleanupLines();
        }
        private void ShowHideFloors()
        {
            Chart.showFloorTrades = !Chart.showFloorTrades;
            SetProperty("ShowFloorTrades_Checked", Chart.showFloorTrades);
            MainObj.DrawImmediate();
        }
        private void ShowHideLiquidations()
        {
            Chart.showLiquidations = !Chart.showLiquidations;
            SetProperty("ShowLiquidations_Checked", Chart.showLiquidations);
            MainObj.DrawImmediate();
        }
        private double SetPriceNoZeros(double priceNoZeros)
        {
            var currentPrice = MainObj.visibleTradeEngine.currentDataItem.Close;
            int leadingZeros = (int)Math.Floor(Math.Log10(Math.Abs(currentPrice))); //get number of zeros before first digit, will be negative for price > 0
            int pow = (int)Math.Floor(Math.Log10(Math.Abs(priceNoZeros)));

            var priceToUse = priceNoZeros;
            if (leadingZeros < 0)
            {
                priceToUse = priceNoZeros * Math.Pow(10, leadingZeros - pow);
            }

            return priceToUse;
        }
        //Set a trade at a specified price, trade will be set as closed.
        //for ease of data entry, only use the significant figures.
        //so if the price is 0.00000123, enter 456 and the new price will be 0.00000456
        //but if price < 0, just use the entered number
        // - will not allow price to be set below current close
        private void ShowSetOrderAtPriceDialog(object param)
        {
            if (MainObj.visibleTradeEngine != null && MainObj.visibleTradeEngine.currentDataItem != null)
            {
                int.TryParse(param as string, out int p);
                var position = p == 0 ? TradePosition.LONG : TradePosition.SHORT;
                //var currentPrice = MainObj.visibleTradeEngine.currentDataItem.Close;
                //var formattedPrice = PriceFormatter.Format(currentPrice); //unused here
                //int leadingZeros = (int)Math.Floor(Math.Log10(Math.Abs(currentPrice))); //get number of zeros before first digit, will be negative for price > 0

                var ret = double.TryParse(SetOrderAtPriceDialog.Prompt(null, $"Set {(p == 0 ? "Long" : "Short")} break order", mouseMoveX, mouseMoveY), out double price);
                if (ret)
                {
                    var priceToUse = SetPriceNoZeros(price);

                    if (position == TradePosition.LONG
                        ? priceToUse <= MainObj.visibleTradeEngine.currentDataItem.Close
                        : priceToUse >= MainObj.visibleTradeEngine.currentDataItem.Close)
                    {
                        AlertDialog.Show($"Price entered is {(position == TradePosition.LONG ? "below" : "above")} current price", "Not possible");
                    }
                    else
                    {
                        MainObj.visibleTradeEngine.SetLineBreakOrderClosed_UI_synchronised(DateTime.Now, priceToUse, position);
                    }
                }
            }
            else
            {
                AlertDialog.Show("Waiting for connection", "Not possible");
            }
        }

        private void ShowSetCeilingPriceDialog()
        {
            if (MainObj.visibleTradeEngine != null && MainObj.visibleTradeEngine.currentDataItem != null)
            {

                var ret = double.TryParse(SetOrderAtPriceDialog.Prompt(null, $"Set ceiling", mouseMoveX, mouseMoveY), out double price);
                if (ret)
                {
                    var priceToUse = SetPriceNoZeros(price);

                    if (priceToUse <= MainObj.visibleTradeEngine.currentDataItem.Close)
                    {
                        AlertDialog.Show("Price entered is below current price", "Not possible");
                    }
                    else
                    {
                        MainObj.visibleTradeEngine.SetCeilingAtPrice_UI_synchronised(DateTime.Now, priceToUse);
                    }
                }
            }
            else
            {
                AlertDialog.Show("Waiting for connection", "Not possible");
            }
        }

        private void ShowSetBreakEvenPriceDialog()
        {
            if (MainObj.visibleTradeEngine != null && MainObj.visibleTradeEngine.currentDataItem != null)
            {
                var ret = double.TryParse(SetOrderAtPriceDialog.Prompt(null, $"Set break even", mouseMoveX, mouseMoveY), out double price);
                if (ret)
                {
                    var priceToUse = SetPriceNoZeros(price);

                    if (priceToUse <= MainObj.visibleTradeEngine.currentDataItem.Close)
                    {
                        AlertDialog.Show("Price entered is below current price", "Not possible");
                    }
                    else
                    {
                        MainObj.visibleTradeEngine.SetBreakEvenAtPrice_UI_synchronised(DateTime.Now, priceToUse);
                    }
                }
            }
            else
            {
                AlertDialog.Show("Waiting for connection", "Not possible");
            }
        }

        private void ShowSetFloorPriceDialog()
        {
            if (MainObj.visibleTradeEngine != null && MainObj.visibleTradeEngine.currentDataItem != null)
            {

                var ret = double.TryParse(SetOrderAtPriceDialog.Prompt(null, $"Set floor", mouseMoveX, mouseMoveY), out double price);
                if (ret)
                {
                    var priceToUse = SetPriceNoZeros(price);

                    if (priceToUse >= MainObj.visibleTradeEngine.currentDataItem.Close)
                    {
                        AlertDialog.Show("Price entered is above current price", "Not possible");
                    }
                    else
                    {
                        MainObj.visibleTradeEngine.SetFloorAtPrice_UI_synchronised(DateTime.Now, priceToUse);
                    }
                }
            }
            else
            {
                AlertDialog.Show("Waiting for connection", "Not possible");
            }
        }
        private void ShowChangePriceDialog(_Trade trade)
        {
            if (MainObj.visibleTradeEngine != null && MainObj.visibleTradeEngine.currentDataItem != null)
            {
                var ret = double.TryParse(SetOrderAtPriceDialog.Prompt(null, $"Change price", mouseMoveX, mouseMoveY), out double price);
                if (ret)
                {
                    MainObj.visibleTradeEngine.ChangePrice_UI_Synchronised(trade, price);
                }
            }
        }
        private void ShowChangeTimeframeDialog(_Trade trade)
        {
            if (MainObj.visibleTradeEngine != null && MainObj.visibleTradeEngine.currentDataItem != null)
            {
                var ret = double.TryParse(SetOrderAtPriceDialog.Prompt(null, $"Change timeframe", mouseMoveX, mouseMoveY), out double price);
                if (ret)
                {
                    MainObj.visibleTradeEngine.ChangeTimeframe_UI_Synchronised(trade, (int)price);
                }
            }
        }
        private void Magnify(bool v)
        {

            if (MainObj.IsBacktesting == true)
            {
                return;
            }
            MainObj.visibleChart?.Magnify(v);
        }

        private void Enlarge(bool v)
        {

            if (MainObj.IsBacktesting == true)
            {
                return;
            }
            MainObj.visibleChart?.Enlarge(v);
        }

        void ChangeSymbol(object name)
        {
            RemovePopupWindow();
            MainObj.ChangeSymbol_UI(name as string);
        }

        void GoToNextWaitingSymbol()
        {
            RemovePopupWindow();
            MainObj.GoToNextWaitingSymbol_UI();
        }

        void GotoNextClosedSymbol()
        {
            RemovePopupWindow();
            MainObj.GoToNextClosedSymbol_UI();
        }

        void GoToNextRunningSymbol()
        {
            RemovePopupWindow();
            MainObj.GoToNextRunningSymbol_UI();
        }

        void GoToPrevSymbol()
        {
            RemovePopupWindow();
            MainObj.GoToPrevSymbol_UI();
        }

        void GoToNextSymbol()
        {
            RemovePopupWindow();
            MainObj.GoToNextSymbol_UI();
        }

        private void SetMenuOptions()
        {
            SetProperty("ConnectionEnabled", MainObj.READ_LIVE_DATA);
            SetProperty("TimeFrame1_Checked", true);
            SetProperty("ShowLiquidations_Checked", Chart.showLiquidations);
            AddStopEnabled = false;
        }

        private void StopUnenteredOrders()
        {
            AlertDialog.ShowCallback("This will stop all unentered orders on the system - are you sure you want to do this?", "Stop unentered orders", () =>
            {
                MainObj.StopWaitingTrades_UI();
            });
        }

        private void RemoveUnenteredOrders()
        {
            AlertDialog.ShowCallback("Remove all unentered orders from the system?", "Remove unentered orders", () =>
            {
                MainObj.RemoveWaitingTrades_UI();
            });
        }

        private void StopAllLiveTrades()
        {
            bool isRunningLive = false;

            using (DataContext db = new DataContext())
            {
                isRunningLive = TradeEngine.AnyEnteredLive(db);
            } //so we don't have a db context floating around, these can be pooled and reused internally

            if (isRunningLive)
            {
                //using MessageBox, non-blocking outside of UI thread
                if (MessageBox.Show("There are one or more live trades, close all on exchange?", "!!! CLOSE LIVE TRADES !!!", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                {
                    return;
                }
            }
            else
            {
                if (MessageBox.Show("Close all running trades? Non are live on exchange", "Close running trades", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                {
                    return;
                }
            }

            MainObj.CloseAllTrades_UI();
        }


#if AUTO_ORDER
        private void EnableLiveTradingAuto()
        {
            MainObj.liveTradeEnabledAuto = !MainObj.liveTradeEnabledAuto;  
            SetProperty("EnableLiveTradingAutoHeader", MainObj.liveTradeEnabledAuto ? DISABLE_LIVE_TRADING_TEXT : ENABLE_LIVE_TRADING_TEXT);
            SetWindowTitle();
        }

#else
        private void EnableLiveTradingAuto() { }
#endif


        private void ShowResultsWindow(object param)
        {
            var resultsWindow = new ResultsWindow();
            resultsWindow.Show();
        }


        //navigate back and forward to the previous or next trades,
        // or navigate back and forward to the previous or next potential trade entries (i.e. indicator positions)
        // - this depends on the trade entry mode
        private void GotoNextTrade(bool forward)
        {
            if (MainObj.IsBacktesting == true) return;
            OnMouseUp(); //to stop drag

            MainObj.visibleChart.ScrollToNextVisibleTrade(forward);
            MainObj.DrawImmediate();
        }

        private void ToggleConnection()
        {
            OnMouseUp(); //to stop drag

            if (MainObj.exchangeConnector.IsConnected)
            {
                AlertDialog.ShowCallback("Pause data feed", "Are you sure?", () =>
                {
                    bool ret = MainObj.exchangeConnector.ToggleConnection();
                    string title = ret ? "Pause connection" : "Start connection";
                    SetProperty("ConnectionMenuTitle", title);
                    if (ret == false)
                    {
                        SetProperty("CountDownText", "PAUSED");
                    }
                    MainObj.DrawCurrentDispatcher();
                });
            }
            else
            {
                MainObj.exchangeConnector.RestartConnection();
                SetProperty("ConnectionMenuTitle", "Pause connection");
                MainObj.DrawCurrentDispatcher();
            }

        }

        private void RestartConnection()
        {
            OnMouseUp(); //to stop drag

            AlertDialog.ShowCallback("This will restart data feed", "Restart connection", () =>
            {
                MainObj.exchangeConnector.RestartConnection();
                SetProperty("ConnectionMenuTitle", "Pause connection");
            });
        }


        public void SetTimeframe(object param)
        {
            int.TryParse(param as string, out int timeFrame);
            MainObj.TimeFrame = timeFrame; //will draw
            SetTimeframeMenuState(timeFrame);
            OnMouseUpNoDraw(); //to stop drag
            MainObj.visibleChart?.ScrollToEnd();
            MainObj.DrawCurrentDispatcher(); //now draw, as chart is updated so TradeEngine can draw lines correctly
        }

        public void SetLeverage(object param)
        {
            int.TryParse(param as string, out int leverage);
            TradeEngine.Leverage = leverage;
            SetLeverageMenuState(leverage);
        }

        public void SetConcurrentOrders(object param)
        {
            int.TryParse(param as string, out int concurrentOrders);
            TradeEngine.MAX_CONCURRENT_TRADES = concurrentOrders;
            SetConcurrentOrdersMenuState(concurrentOrders);
        }

        public void SetInitialTime(int timeFrame)
        {
            SetProperty("UI_Interval", timeFrame.ToString()); //this is an alternative to calling the method and putting OnPropertyChanged there
            mouseDown = false; //because the mouse down events fire and set it to true during initialisation

            BackupDBEnabled = true; //cannot do this during init, obviously

        }

        public void SetTimeframeMenuState(int timeFrame)
        {
            switch (timeFrame)
            {
                case 5:
                    SetProperty("TimeFrame1_Checked", true); SetProperty("TimeFrame2_Checked", false); SetProperty("TimeFrame3_Checked", false); SetProperty("TimeFrame4_Checked", false); SetProperty("TimeFrame4_Checked", false);
                    break;
                case 15:
                    SetProperty("TimeFrame1_Checked", false); SetProperty("TimeFrame2_Checked", true); SetProperty("TimeFrame3_Checked", false); SetProperty("TimeFrame4_Checked", false); SetProperty("TimeFrame4_Checked", false);
                    break;
                case 60:
                    SetProperty("TimeFrame1_Checked", false); SetProperty("TimeFrame2_Checked", false); SetProperty("TimeFrame3_Checked", true); SetProperty("TimeFrame4_Checked", false); SetProperty("TimeFrame4_Checked", false);
                    break;
                case 240:
                    SetProperty("TimeFrame1_Checked", false); SetProperty("TimeFrame2_Checked", false); SetProperty("TimeFrame3_Checked", false); SetProperty("TimeFrame4_Checked", true); SetProperty("TimeFrame4_Checked", false);
                    break;
                case 1440:
                    SetProperty("TimeFrame1_Checked", false); SetProperty("TimeFrame2_Checked", false); SetProperty("TimeFrame3_Checked", false); SetProperty("TimeFrame4_Checked", false); SetProperty("TimeFrame4_Checked", true);
                    break;
            }
            SetProperty("UI_Interval", timeFrame.ToString()); //this is an alternative to calling the method and putting OnPropertyChanged there

        }

        public void SetConcurrentOrdersMenuState(int m)
        {
            const int maxConcurrentOrders = 12;

            for (var i = 1; i < maxConcurrentOrders + 1; i++)
            {
                if (i != m)
                {
                    var propName = $"ConcurrentOrders{i}_Checked";
                    SetProperty(propName, false);
                }
            }
            SetProperty($"ConcurrentOrders{m}_Checked", true);
        }

        public void SetLeverageMenuState(int m)
        {
            const int leverage = 10;

            for (var i = 1; i < leverage + 1; i++)
            {
                if (i != m)
                {
                    var propName = $"Leverage{i}_Checked";
                    SetProperty(propName, false);
                }
            }
            SetProperty($"Leverage{m}_Checked", true);

            SetWindowTitle();
        }

        #endregion

        #region Keyboard
        public List<Key> keysDown = new List<Key>();

        public void KeyDown(KeyEventArgs e)
        {
            KeyDown(e.Key);
        }

        public bool toggleDraw = false;
        public void KeyDown(Key key)
        {
            //don't use SPACE - it holds focus on the last button clicked.
            switch (key)
            {
                case Key.LeftCtrl:
                    MainObj.visibleChart.canvas.Cursor = System.Windows.Input.Cursors.Pen;
                    break;
                case Key.D:
                    toggleDraw = !toggleDraw;
                    if (toggleDraw)
                    {
                        MainObj.visibleChart.canvas.Cursor = System.Windows.Input.Cursors.Pen;
                    }
                    else
                    {
                        MainObj.visibleChart.canvas.Cursor = System.Windows.Input.Cursors.Arrow;
                    }
                    SetProperty("DrawingMode_Checked", toggleDraw);
                    break;
                case Key.X:
                    //if (IsKeyDown(Key.LeftCtrl))
                    {
                        if (_ChartLine.CurrentLine != null)
                        {
                            int.TryParse(_ChartLine.CurrentLine.Name.Substring(1), out int id);
                            MainObj.visibleTradeEngine.DeleteLine_UI_synchronised(id);
                        }
                        if (Chart.CurrentTrade != null)
                        {
                            MainObj.visibleTradeEngine.RemoveTrade_UI_synchronised(Chart.CurrentTrade.Id);
                            RemovePopupWindow();
                        }
                    }
                    break;
                //would have rather used F7 - F10, but F10 not working on this keyboard!
                case Key.D7:
                case Key.NumPad7:
                    //ShowSetOrderAtPriceDialog();
                    break;
                case Key.D8:
                case Key.NumPad8:
                    ShowSetBreakEvenPriceDialog();
                    break;
                case Key.D9:
                case Key.NumPad9:
                    ShowSetCeilingPriceDialog();
                    break;
                case Key.D0:
                case Key.NumPad0:
                    ShowSetFloorPriceDialog();
                    break;

                case Key.H:
                    ToggleHeikinAshi();
                    break;

                case Key.B:
                    GetBalances();
                    break;

                case Key.A:
                    Chart.showOnlyActiveTrade = !Chart.showOnlyActiveTrade;
                    SetProperty("ShowOnlyActiveTrade_Checked", Chart.showOnlyActiveTrade);
                    //GoToNextWaitingSymbol();
                    MainObj.DrawImmediate();
                    break;

                case Key.V:
                    Chart.showTrades = !Chart.showTrades;
                    SetProperty("ShowAllTrades_Checked", Chart.showTrades);
                    MainObj.DrawImmediate();
                    break;

                case Key.C:
                    Chart.showClosedTrades = !Chart.showClosedTrades;
                    SetProperty("ShowClosedTrades_Checked", Chart.showClosedTrades);
                    //GotoNextClosedSymbol();
                    MainObj.DrawImmediate();
                    break;
                case Key.F:
                    if (!keysDown.Contains(key))
                    {
                        ShowHideFloors();
                    }
                    break;
                case Key.L:
                    if (!keysDown.Contains(key))
                    {
                        ShowHideLiquidations();
                    }
                    break;

                case Key.Z:
                    if (!keysDown.Contains(key))
                    {
#if BACKTEST
                        Backtest_execution.StepBacktest();
#endif
                    }
                    break;
                //problem with the lines trapping mouse events, and using PreviewMouseDown to bubble the event does not help.
                //so just show the lines when the alt key is pressed
                case Key.LeftShift:

                    // MainObj.chart.ShowCrossLines();
                    break;

                case Key.Escape:

                    MainObj.visibleChart.ShowBox = false;
                    keysDown.Clear();
                    MainObj.visibleChart.currentLine = null;
                    _ChartLine.movingLine = null;

                    MainObj.StopSound();
                    break;

                case Key.S: //this only fires once for some reason????
                    if (IsKeyDown(Key.LeftShift))
                    {
                        GoToPrevSymbol();
                    }
                    else
                    {
                        GoToNextSymbol();
                    }
                    break;

                case Key.R:
                    GoToNextRunningSymbol();
                    break;

                case Key.K:
                    if (MainObj.BACKTEST)
                    {
#if BACKTEST
                        BacktestSymbol("2");
#endif
                    }
                    break;

                case Key.I:
                    InvertChart();
                    break;
            }

            if (!keysDown.Contains(key))
            {
                keysDown.Add(key);
            }
        }
        public void KeyUp(KeyEventArgs e)
        {

            keysDown.Remove(e.Key);

            if (e.Key != Key.D && !toggleDraw) //stay in drawing mode
            {
                MainObj.visibleChart.canvas.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }
        public void KeyUp(Key key)
        {
            keysDown.Remove(key);
        }

        internal bool IsKeyDown(Key key)
        {
            return keysDown.Contains(key);
        }
        #endregion

        #region Mouse

        #region Xaml bound properties for mouse events

        bool mouseDown = false;
        public System.Windows.Point MouseDown
        {
            set
            {
                mouseDown = true;
                Window_MouseDown();
                //       OnPropertyChanged();
            }
        }

        public System.Windows.Point MouseRightButtonDown
        {
            set
            {
                mouseDown = true;
                Window_MouseDown();
                //       OnPropertyChanged();
            }
        }

        public System.Windows.Point MouseUp
        {
            set
            {
                Window_MouseUp(); //this is now window-level, from xaml.cs, so mouse up outside of canvas will stop scroll
            }
        }

        private int mouseWheel;
        public int MouseWheel
        {
            set
            {
                mouseWheel = value;
                if (IsKeyDown(Key.LeftShift))
                {
                    Enlarge(value > 0);
                }
                else
                {
                    Magnify(value > 0);
                }
            }
            get
            {
                return mouseWheel;
            }
        }



        private double mouseMoveX;
        public double MouseMoveX
        {
            get { return mouseMoveX; }
            set
            {
                if (value.Equals(mouseMoveX)) return;
                OnPropertyChanged();
                Window_MouseMove(value, MouseMoveY);
                mouseMoveX = value;
            }
        }

        private double mouseMoveY;
        public double MouseMoveY
        {
            get { return mouseMoveY; }
            set
            {
                if (value.Equals(mouseMoveY)) return;
                OnPropertyChanged();
                Window_MouseMove(MouseMoveX, value);
                mouseMoveY = value;
            }
        }

        #endregion


        //when various things get clicked, it is necessary to set mouseDown to false to kill dragging, and show the marker lines, etc.
        public void OnMouseUp()
        {
            mouseDown = false;
            if (MainObj.visibleChart != null)
            {
                MainObj.visibleChart.MouseUp();
            }
            //MainObj.DrawImmediate(); //calls line draw calculation again

        }
        public void OnMouseUpNoDraw()
        {
            mouseDown = false;
            if (MainObj.visibleChart != null)
            {
                MainObj.visibleChart.MouseUp();
            }
        }


        void Window_MouseUp()
        {
            //OnMouseUp();
        }

        void Window_MouseMove(double mouseX, double mouseY)
        {
            //mainWindow.CaptureMouse();

            if (mouseDown)
            {
                RemovePopupWindow();
            }

            if (MainObj.IsBacktesting == true)
            {
                return;
            }
#if BACKTEST
            if (MainObj.visibleTradeEngine != null && MainObj.IsBacktesting)
            {
                return; //this causes an exception when accessing the db in TradeEngine otherwise
            }
#endif
            if (popupMenuWindow == null)
            {
                MainObj.visibleChart?.MouseMove(mouseX, mouseY, mouseDown, IsKeyDown(Key.LeftShift) || IsKeyDown(Key.RightShift), IsKeyDown(Key.LeftCtrl) || IsKeyDown(Key.RightCtrl), toggleDraw);
            }
        }

        //because the crosshair lines will capture mouse events once they show, we need to route the mouse down event directly to the chart to show the overlay box
        void Window_MouseDown()
        {
            if (popupMenuWindow != null)
            {
                popupMenuWindow.Close();
                popupMenuWindow = null;
            }
            MainObj.visibleChart?.MouseDown(IsKeyDown(Key.LeftShift) || IsKeyDown(Key.RightShift), IsKeyDown(Key.LeftCtrl) || IsKeyDown(Key.RightCtrl), toggleDraw);
        }

        public void MouseLeave()
        {
            keysDown.Clear();

            //OnMouseUp();
        }

        #endregion

        #region Utility methods

        private void ShowInvertedChartWarning()
        {
            AlertDialog.Show("Chart is inverted, orders are not set in this view mode", "Cannot set order");
        }

        public void SetWindowTitle()
        {

            var executionType = MainObj.BACKTEST ? " Testnet trading " : " LIVE TRADING ";
            var orderLimit = TradeEngine.RESTRICTED_TRADE_VALUE > 0 ? $", ${TradeEngine.RESTRICTED_TRADE_VALUE} trade limit " : "";
            var exchange = TradeEngine.EXCHANGE.GetEnumDisplayName();
            string title = $"{exchange} " + (TradeEngine.USE_SIMULATED_EXCHANGE ? "using SIMULATED EXCHANGE" : $"{executionType} - {TradeEngine.Leverage}X leverage {orderLimit}");

#if BACKTEST
            title += " - Backtest build";
#endif
#if AUTO_ORDER
            tiel += " - auto order enabled";
            if (MainObj.liveTradeEnabledAuto)
            {
                title += " - live auto trading ";
            }
#endif

            SetProperty("WindowTitle", title);
        }
        #endregion

        #region Backtesting
#if BACKTEST


        public void SetStoppedBacktestMenu()
        {
            SetProperty("RunAllBacktestHeader", RUN_BACKTESTS_TEXT);
            SetProperty("RunBatchBacktestHeader", RUN_BATCH_BACKTESTS_TEXT);
            SetProperty("RunBacktestForSymbolHeader", RUN_BACKTESTS_FOR_SYMBOL_TEXT);
            SetProperty("PauseBacktestHeader", PAUSE_BACKTEST_TEXT);
            SetProperty("PauseBacktestEnabled", false);
            SetProperty("StopBacktestEnabled", false);
        }


        public void SetRunningBacktestMenu()
        {
            SetProperty("RunAllBacktestHeader", STOP_BACKTEST_TEXT);
            SetProperty("PauseBacktestHeader", PAUSE_BACKTEST_TEXT);
            SetProperty("PauseBacktestEnabled", true);
            SetProperty("StopBacktestEnabled", true);
        }

        public void SetRunningBatchBacktestsMenu()
        {
            SetProperty("RunBatchBacktestAutoHeader", STOP_BATCH_BACKTESTS_TEXT);
        }


        private void BacktestSymbol(object tradeType)
        {
            if (MainObj.IsBacktesting) //trade is running
            {
                AlertDialog.ShowCallback("Abort all backtests?", "Stop", () =>
                {
                    Backtest_execution.StopBacktest();
                    SetStoppedBacktestMenu();
                });
            }
            else //trade is not running
            {
                SetRunningBacktestMenu();
                Backtest_execution.StartBacktestOnSymbol();// _Trade.GetExecutionSystem(tradeType));
            }
        }
        private void SetBacktestTimeframeCommand(object param)
        {
            int.TryParse(param as string, out int timeFrame);
            //TradeEngine.BACKTESTING_TIMEFRAME = timeFrame;

        }
        private void RunAllBacktests(object tradeType)
        {
            if (MainObj.IsBacktesting) //trade is running
            {
                AlertDialog.ShowCallback("Abort all backtests?", "Stop", () =>
                {
                    Backtest_execution.StopBacktest();
                    SetStoppedBacktestMenu();
                });
            }
            else //trade is not running
            {
                AlertDialog.ShowCallback("Run backtests?", "Start", () =>
                {
                    SetRunningBacktestMenu();
                    Backtest_execution.StartAllBacktests();// _Trade.GetExecutionSystem(tradeType));
                });
            }
        }

        private async void RunBatchBacktests(object param)
        {
            if (MainObj.IsBacktesting)
            {
                AlertDialog.ShowCallback("Abort batch backtest?", "Stop", () =>
                {
                    Backtest_execution.StopBacktest();
                    SetStoppedBacktestMenu();
                });
            }
            else
            {
                AlertDialog.ShowCallback("Start batch backtest (using visible timeframe interval, will skip frames if > 1 min)?", "Start", async () =>
                {
                    SetRunningBatchBacktestsMenu();
                    await Backtest_execution.StartBatchBacktest();// _Trade.GetExecutionSystem(param));
                    AlertDialog.Show("Batch backtest complete!");
                });
            }
        }

        private void PauseBacktest()
        {
            if (Backtest_execution.BacktestPaused)
            {
                SetProperty("PauseBacktestHeader", PAUSE_BACKTEST_TEXT);
                Backtest_execution.ResumeBacktest();
            }
            else
            {
                SetProperty("PauseBacktestHeader", RESUME_BACKTEST_TEXT);
                Backtest_execution.PauseBacktest();
            }
        }
        private void StopBacktest()
        {
            if (MainObj.IsBacktesting) //trade is running
            {
                //AlertDialog.Show("Abort all backtest?", "Stop", () =>
                {
                    Backtest_execution.StopBacktest();
                    SetStoppedBacktestMenu();
                }
            }
        }

#endif
        #endregion
    }

}