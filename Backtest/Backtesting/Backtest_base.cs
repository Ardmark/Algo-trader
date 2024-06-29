using AlgoTrader.Live.ViewModels;
using AlgoTrader.Backtest.Windows;
using AlgoTrader.Shared;
using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Interfaces;
using AlgoTrader.Shared.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using _Trade = AlgoTrader.Shared._Trade;

namespace AlgoTrader.Backtest.Backtesting
{
    public abstract class Backtest_base
    {
        //struct used in batch backtesting
        public struct BatchBacktestTradeResult
        {
            public string name;
            public double result;

            public BatchBacktestTradeResult(string name, double result)
            {
                this.name = name;
                this.result = result;
            }
        }

        #region Constants
        protected const int RUN_TRADE_INTERVAL = 5; //time in ms between trade run iterations for backtesting
        #endregion

        #region Fields

        protected MainViewModel mainViewModel;
        protected int totBacktestIterations = 1;
        protected static int numBacktestLoops = 0;
        protected BatchResultsViewModel batchResultsViewModel;
        protected List<BatchBacktestTradeResult> tradeResults = new List<BatchBacktestTradeResult>();
        public List<_Trade> backtestTradePositions = new List<_Trade>(); //first trade positions, either MarketOrder or auto-trade entry positions
        protected int currentBacktestFrame;
        protected DispatcherTimer singleBacktestTimer = null;
        protected Chart chart;
        public TradeEngine tradeEngine;
        public bool isPaused;

        #endregion

        #region Properties
        public int TimeFrame
        {
            get
            {
                return MainObj.TimeFrame;
            }
        }

        
        public TimeSpan TimerInterval
        {
            get
            {
                 return new TimeSpan(0, 0, 0, 0, RUN_TRADE_INTERVAL / MainObj.TimeFrame);
            }
        }

        public List<_SymbolDataItem> Results
        {
            get
            {
                return tradeEngine.results;
            }
        }

        public bool BacktestRunning
        {
            get
            {
                return !isPaused;
            }
        }

        public bool BacktestPaused
        {
            get
            {
                return isPaused;
            }
        }

        public Dictionary<string, List<dynamic>> BacktestProperties
        {
            get
            {
                return tradeEngine.backtestProperties;
            }
        }

        #endregion

        #region Abstract methods

        protected abstract List<_Trade> GetAllOrders(DataContext db, string symbolName);
        public abstract bool BacktestsAvailable();

        #endregion

        #region Init
        public Backtest_base(IMainViewModel mainViewModel)
        {
            this.mainViewModel = mainViewModel as MainViewModel;
            chart = MainObj.visibleChart;
            tradeEngine = MainObj.visibleTradeEngine;
        }

        #endregion

        #region Init backtests
        public virtual void InitForBacktests(DataContext db, string symbolName)
        {
            MainObj.IsBacktesting = true;
            Chart.canDraw = false;
            mainViewModel.BacktestResultEnabled = false;
            isPaused = false;
        }

        public void ResetBacktestMenus()
        {
#if BACKTEST
            mainViewModel.SetStoppedBacktestMenu();
#endif
        }


        #endregion

        #region Get orders

        protected _Trade GetVisibleOrder()
        {
            var firstVisibleTime = chart.LastVisibleTime.AddMinutes(-Chart.ScaleBars * TimeFrame);
            IOrderedEnumerable<_Trade> trades = null;
            tradeEngine.GetUILists(null, () =>
            {
                trades = tradeEngine.UI_Trades.Where(x =>
                                                        x.Action == TradeAction.ORDER &&
                                                        x.Timestamp > firstVisibleTime &&
                                                        x.Timestamp <= chart. LastVisibleTime
                                                        ).OrderBy(x => x.TimeFrame)
                                                        ;
            });
            return trades.FirstOrDefault();
        }

        protected _Trade GetNextOrderForBacktest(bool forward,  DataContext db)
        {
            if (!backtestTradePositions.Any()) //not going to be needed but it doesn't hurt
            {
                backtestTradePositions = GetAllOrders(db, null);
            }
            (List<_SymbolDataItem> items_chart, _) = tradeEngine.GetResults_UI(chart.ScrollBars * chart.TimeFrame, Chart.ScaleBars * chart.TimeFrame, chart.TimeFrame);
            var lastBarTime = chart.LastBarTime(items_chart);
            var visibleTrades = backtestTradePositions.Where(x => DateTime.Compare(x.Timestamp, lastBarTime) < 0 && DateTime.Compare(x.Timestamp, chart.FirstVisibleTime) >= 0);

            //todo - this is not really right, as it is using the pre-saved list of visible times but a new list of visible trades
            if (!visibleTrades.Any())
            {
                //no trades showing
                return forward
                    ? backtestTradePositions.Where(x => DateTime.Compare(x.Timestamp, chart.LastVisibleTime) >= 0).FirstOrDefault()
                    : backtestTradePositions.Where(x => DateTime.Compare(x.Timestamp, chart.LastVisibleTime) < 0).LastOrDefault();
            }
            else
            {
                //get the next trade along
                //return trade which is before the end position if scrolling back, return the next trade along if scrolling forward
                //if forward, must start from the next timeframe position along from where a trade can be displayed, because
                // that trade might be at say 10 mins in a 15 min timeframe, but the bar position would be at zero minutes
                // so > that time would just return the last visible trade on screen, not the next one along
                return forward
                    ? backtestTradePositions.Where(x => DateTime.Compare(x.Timestamp, chart.LastVisibleTime.AddMinutes((TradeEngine.MIN_RIGHT_DISPLAY_BARS_TO_SHOW_TRADE - 1) * -TimeFrame)) > 0).FirstOrDefault()
                    : backtestTradePositions.Where(x => DateTime.Compare(x.Timestamp, chart.LastVisibleTime.AddMinutes(TradeEngine.MIN_RIGHT_DISPLAY_BARS_TO_SHOW_TRADE * -TimeFrame)) < 0).LastOrDefault();

            }
        }

        protected void AddOrderToDb(_Trade trade, DataContext db)
        {
            //backtests are cleared before running a single backtest so this trade won't be in the db at this point. It must be added
            // because execution depends on an Order being present (for termination of trade)
            if (!db.Trades.Where(x => x.TradeReference == trade.TradeReference).Any())
            {
                db.Trades.Add(trade);
            }

            trade.GlobalStatus = TradeGlobalStatus.ACTIVE;
            trade.ItemStatus = TradeItemStatus.OPEN;
            trade.ExchangeStatus = TradeExchangeStatus.ENTERED_OFF_EXCHANGE;
            db.SaveChanges();
        }
        #endregion

        #region Start backtests
        public virtual bool StartSingleBacktest(_Trade tradeToBacktest = null)
        {
#if !BACKTEST
return false;
#endif
            TradeEngine.USE_SIMULATED_EXCHANGE = true;

            var trade = tradeToBacktest ?? GetVisibleOrder();

            if (trade == null)
            {
                return false;
            }

            tradeEngine = MainObj.GetTradeEngineForSymbol(trade.SymbolName);
            chart = MainObj.GetChartForSymbol(trade.SymbolName);

            using (DataContext dbBacktest = new DataContext())
            {
                //we just want the frame to start at for drawing
                int currentFrame = GetCurrentBacktestFrame(trade, chart.Results.Last().Timestamp, dbBacktest);// chart.GetFrameForTrade(trade);
                if (currentFrame == -1)
                {
                    return false;
                }

                //don't want to call InitForBacktests() as that will clear everything out
                //so just do this instead
                MainObj.IsBacktesting = true;
                mainViewModel.BacktestResultEnabled = false;
                isPaused = false;

                Chart.canDraw = true;

                //remove all the other trades for this trade Id (just the entry because the initial stop / take profit needs to be left in place)
                dbBacktest.Trades.RemoveRange(dbBacktest.Trades.Where(x => 
                    x.TradeReference == trade.TradeReference && 
                    (x.Action == TradeAction.ENTRY || x.Action == TradeAction.CLOSE)
                    ));

                trade = dbBacktest.Trades.Where(x => x.Id == trade.Id).First();
                trade.ItemStatus = TradeItemStatus.OPEN;
                trade.GlobalStatus = TradeGlobalStatus.ACTIVE;

                //set all trades as backtest
                var allTrades = dbBacktest.Trades.Where(x => x.TradeReference == trade.TradeReference);
                foreach (var t in allTrades)
                {
                    t.ExchangeStatus = TradeExchangeStatus.ENTERED_OFF_EXCHANGE;
                }

                dbBacktest.SaveChanges();

                chart.yOffset = 0; //reset this or will run off screen
                currentBacktestFrame = currentFrame;
                singleBacktestTimer = new DispatcherTimer()
                {
                    Interval = TimerInterval,
                };
            }

            //run a timer to find the entry point for any of the market orders
            singleBacktestTimer.Tick += new EventHandler((source, eventArgs) =>
            {
                if (!isPaused)
                {
                    using (DataContext dbBacktest1 = new DataContext())
                    {
                        RunOneFrameForBacktest(dbBacktest1);
                    }
                }
            });

            singleBacktestTimer.Start();
            

            return true;
        }

        int GetCurrentBacktestFrame(_Trade trade, DateTime endTime, DataContext db)
        {
            return tradeEngine.Chart.GetFrameForTimestamp(trade.Timestamp);

            #region Unused
            /*
                //get start frame
                if (trade.Type == TradeType.LINE_BREAK_ORDER)
                {
                    var startTime = trade.Timestamp;
                    var startPrice = trade.Price;
                    double gradient = 0;

                    var line = db.Lines.Where(x => x.TradeId == trade.TradeId).FirstOrDefault();
                    if (line != null)
                    {
                        startTime = line.StartTimeStamp;
                        endTime = line.EndTimeStamp;
                        gradient = (line.EndPrice - line.StartPrice) / (endTime - startTime).Ticks;
                    }

                    var startItem = trade.Position == TradePosition.LONG
                        ? tradeEngine.Results.Where(x => x.Timestamp > startTime && x.High > startPrice + gradient * (x.Timestamp - startTime).Ticks).FirstOrDefault()
                        : tradeEngine.Results.Where(x => x.Timestamp > startTime && x.Low < startPrice + gradient * (x.Timestamp - startTime).Ticks).FirstOrDefault();

                    if (startItem != null)
                    {
                        backtestFrame = chart.GetFrameForTimestamp(startItem.Timestamp);
                    }
                }

                return backtestFrame;
               */ 
            #endregion
        }
        //Run trade for backtesting
        private bool RunTrade_Backtesting_synchronised(List<_SymbolDataItem> results, int frame, bool itemAdded)
        {
            if (frame >= results.Count)
            {
                return false;
            }

            tradeEngine.currentDataItem = results[frame];

            //if we are on the last frame, close trade - this is for backtesting
            if (MainObj.IsBacktesting && frame >= results.Count)
            {
                tradeEngine.CloseTradeOnSystem_synchronised((decimal)tradeEngine.currentDataItem.Close);
                return false;
            }

            bool ret = tradeEngine.RunTrade_synchronised(frame, itemAdded);

            return ret;
        }

        public async virtual Task<bool> StartAllBacktests(string symbolName = null)
        {
#if !BACKTEST
return false;
#endif
            TradeEngine.USE_SIMULATED_EXCHANGE = true;

            using (DataContext dbBacktest = new DataContext())
            {
                InitForBacktests(dbBacktest, symbolName);

                if (backtestTradePositions.Any())
                {
                    double count = 0;
                    var tradeGroups = backtestTradePositions.GroupBy(x => x.SymbolName);
                    foreach (var tradeGroup in tradeGroups)
                    {
                        var trades = tradeGroup.OrderBy(x => x.Timestamp).ToList();
                        var firstTradeTime = trades.First().Timestamp;
                        var endTime = trades.Select(x => x.Timestamp).Last();
                        var tradeEngine = MainObj.GetTradeEngineForSymbol(tradeGroup.Key);
                        currentBacktestFrame = 0;
                        if (tradeEngine != null)
                        {
                            for (int i = 0; i < trades.Count; i++)
                            {
                                count++;
                                _Trade trade = trades[i];

                                //jump to the start time where the trade break line intersects the price
                                var _currentBacktestFrame = GetCurrentBacktestFrame(trade, endTime, dbBacktest);
                                if (_currentBacktestFrame <= currentBacktestFrame)
                                {
                                    continue; //skip overlapping trades
                                }
                                currentBacktestFrame = _currentBacktestFrame;

                                var results = tradeEngine.Results;

                                AddOrderToDb(trade, dbBacktest);

                                double pc = count / backtestTradePositions.Count;
                                mainViewModel.ProgressPercent = pc.ToString("0.00%");

                                await Task.Run(() =>
                                {
                                    //Debug.WriteLine($"Backtesting {trade.SymbolName} at {trade.Timestamp}, frame {currentBacktestFrame}");
                                    while (RunTrade_Backtesting_synchronised(results, currentBacktestFrame, true))
                                    {
                                        if (!MainObj.IsBacktesting)
                                        {
                                            i = trades.Count;
                                            break; //allows UI to request exit during the RunTrade loop
                                        }
                                        if (isPaused) continue;

                                        currentBacktestFrame++; //must go on the minute to find entry                  
                                        if (currentBacktestFrame >= results.Count)
                                        {
                                            currentBacktestFrame = results.Count;
                                            break;
                                        }
                                        //pc = currentBacktestFrame / results.Count;
                                        //mainViewModel.ProgressPercent = pc.ToString("0.00%");
                                    }

                                });
                            }
                        }
                    }


                    Backtest_execution.IsRunningAllBacktests = false;

                    Chart.canDraw = true;

                    //remove the stops and take profit markers
                    dbBacktest.Trades.RemoveRange(dbBacktest.Trades.Where(x => (x.Action == TradeAction.STOP || x.Action == TradeAction.CEILING || x.Action == TradeAction.FLOOR || x.Action == TradeAction.TAKE_PROFIT)));
                    dbBacktest.SaveChanges();
                    dbBacktest.SaveChanges();
                    mainViewModel.ProgressPercent = "100%";
                    mainViewModel.BacktestResultEnabled = true;
                    ResetBacktestMenus();
                    var resultsWindow = new ResultsWindow();//do the summary! Don't put this in the calling method (command-event driven) as this will execute at the end of this method anyway
                    resultsWindow.Show();
                    MainObj.DrawCurrentDispatcher();
                    MainObj.IsBacktesting = false;                    //MainObj.PlaySound(MainObj.ALERT_SOUND);
                }
            }

            return true;
        }

        public async Task StartBatchBacktest()
        {
            if (MainObj.IsBacktesting)
            {
                return;
            }

            TradeEngine.USE_SIMULATED_EXCHANGE = true;

            using (DataContext dbBacktest = new DataContext())
            {
                Backtest_execution.tradeEngine = null;

                mainViewModel.SetStoppedBacktestMenu();

                mainViewModel.ProgressPercent = string.Empty;

                isPaused = false;
                tradeResults.Clear();
                Backtest_execution.IsRunningBatchBacktest = true;
                MainObj.IsBacktesting = true;
                numBacktestLoops = 0;
                totBacktestIterations = 1;
                tradeEngine.SetBacktestPropertyDictionary();

                foreach (var backtestParamSet in BacktestProperties)
                {
                    totBacktestIterations *= backtestParamSet.Value.Count;
                }

                DateTime start = DateTime.UtcNow;

                //run all backtests
                await Task.Run(() => RunBacktestForProperty(0, dbBacktest));

                TimeSpan elapsed = DateTime.UtcNow.Subtract(start);

                mainViewModel.SetStoppedBacktestMenu();

                Backtest_execution.IsRunningBatchBacktest = false;
                //RemoveConcurrent(db);
                tradeResults.Sort((BatchBacktestTradeResult a, BatchBacktestTradeResult b) => { return b.result.CompareTo(a.result); }); //b.compareto(a) reverses the order
                string header = string.Format("{0}   Batch backtest results ({4} iterations, took {1} hours {2} mins {3} secs)", DateTime.Now, elapsed.Hours, elapsed.Minutes, elapsed.Seconds, totBacktestIterations);
                BatchResultsWindow batchResultsWindow = new BatchResultsWindow(mainViewModel);
                batchResultsViewModel = batchResultsWindow.batchResultsViewModel;
                batchResultsViewModel.Header(header);

                foreach (BatchBacktestTradeResult results in tradeResults)
                {
                    batchResultsViewModel.AddEntry(results.name, results.result);
                }

                batchResultsWindow.Show();
                ResetBacktestMenus();
                MainObj.IsBacktesting = false;
            }
        }
#endregion

#region Run backtests


        public void StepBacktest()
        {
            TradeEngine.USE_SIMULATED_EXCHANGE = true;

            using (DataContext dbBacktest = new DataContext())
            {
                ResumeBacktest();
                RunOneFrameForBacktest(dbBacktest);
            }
        }

        void RunOneFrameForBacktest(DataContext db)
        {
            if (mainViewModel.IsKeyDown(System.Windows.Input.Key.Q))
            {
                //kill the entire backtest process
                StopBacktest(db);
                ResetBacktestMenus();
                MainObj.DrawCurrentDispatcher(); //to show correct colours
                return;
            }
            ResumeBacktest();
            //tradeEngine.SetAlertText();

            for (int i = 0; i < TimeFrame; i++)
            {
                if (currentBacktestFrame >= Results.Count) //run out of frames, at the end of the chart now
                {
                    //kill the entire backtest process
                    StopBacktest(db);
                    ResetBacktestMenus();
                    MainObj.DrawCurrentDispatcher(); ; //to show correct colours
                    break;
                }

                tradeEngine.DataContext = db;

                bool complete = RunTrade_Backtesting_synchronised(Results, currentBacktestFrame, true);
                var timeStamp = Results[currentBacktestFrame].Timestamp;
                mainViewModel.SetProperty("ProgressPercent", timeStamp.ToString("dd/MM/yy HH:mm"));

                if (complete)
                {
                    //no more trades
                    StopBacktest(db);
                    ResetBacktestMenus();
                    break;
                }

                currentBacktestFrame++;
            }

            MainObj.GoToFrame_ChangeSymbol(currentBacktestFrame); //do this e.g. every 60 frames, so if not entered won't crawl along
        }

        void RunBacktestForProperty(int keyIndex, DataContext db)
        {
            string propName = BacktestProperties.Keys.ElementAt(keyIndex);
            List<dynamic> backtestProperty = BacktestProperties[propName];
            foreach (dynamic val in backtestProperty)
            {
                GetType().GetProperty(propName).SetValue(this, val);
                if (keyIndex < BacktestProperties.Keys.Count - 1)
                {
                    RunBacktestForProperty(keyIndex + 1, db);
                }

                if (!Backtest_execution.IsRunningBatchBacktest) //allows UI to request exit during the RunTrade loop
                {
                    break;
                }

                //only run the trade test on the very last recursion, or there are unnecessary loops (and quite a lot)
                if (keyIndex == BacktestProperties.Keys.Count - 1)
                {
                    //run one complete set of backtests
                    InitForBacktests(db, null);

                    var trades = backtestTradePositions;
                    bool first = true;
                    int startingFrame = 0;
                    double len = Results.Count;
                    var endTime = trades.Select(x => x.Timestamp).Last();
                    foreach (AlgoTrader.Shared._Trade trade in trades)
                    {
                        if (!Backtest_execution.IsRunningBatchBacktest) //allows UI to request exit during the RunTrade loop
                        {
                            break;
                        }

                        AddOrderToDb(trade, db);

                        int currentBacktestFrame = GetCurrentBacktestFrame(trade, endTime, db);
                        if (first)
                        {
                            startingFrame = currentBacktestFrame;
                            first = false;
                        }

                        while (RunTrade_Backtesting_synchronised(Results, currentBacktestFrame, true))
                        {
                            currentBacktestFrame++; //must go on the minute to find entry
                            
                            if (currentBacktestFrame >= Results.Count)
                            {
                                currentBacktestFrame = Results.Count;
                                break;
                            }

                            double _pc = ((currentBacktestFrame - startingFrame) / (len - startingFrame));
                            mainViewModel.ProgressPercent = _pc.ToString("0.0%");
                            if (!Backtest_execution.IsRunningBatchBacktest) //allows UI to request exit during the RunTrade loop
                            {
                                break;
                            }
                        }
                    }

                    // Debug.WriteLine(string.Format("OFFSET_PERCENT:{0}, BREAKEVEN_PERCENT:{1}, INITIAL_STOP_PERCENT:{2}", OFFSET_PERCENT, BREAKEVEN_PERCENT, INITIAL_STOP_PERCENT));
                    var total = (from d in db.Trades
                                 where
                                  d.ItemStatus == TradeItemStatus.CLOSED &&
                                  d.Action == TradeAction.CLOSE &&
                                  d.TimeFrame == TimeFrame
                                 select d.Result).Sum();

                    //get backtest property info
                    string info = string.Empty;
                    int i = 0;
                    foreach (var backtestParamSet in BacktestProperties)
                    {
                        string _propName = backtestParamSet.Key;
                        dynamic value = GetType().GetProperty(_propName).GetValue(this, null);
                        info += string.Format("{0} : {1}", _propName, value.ToString());
                        if (i < BacktestProperties.Count - 1)
                        {
                            info += ", ";
                        }
                        i++;
                    }

                    tradeResults.Add(new BatchBacktestTradeResult(info, total));
                    numBacktestLoops++;
                    double pc = 100 * numBacktestLoops / totBacktestIterations;
                    mainViewModel.ProgressPercent1 = pc.ToString("0") + "%";
                }
            }
        }
        #endregion

        #region Stop, pause, resume backtests

        public void ResumeBacktest()
        {
            isPaused = false;
            if (singleBacktestTimer != null)
            {
                singleBacktestTimer.IsEnabled = true;
            }
        }

        public void PauseBacktest()
        {
            isPaused = true;
            if (singleBacktestTimer != null)
            {
                singleBacktestTimer.IsEnabled = false;
            }
        }

        public virtual void StopBacktest(DataContext db)
        {
            if (singleBacktestTimer != null)
            {
                singleBacktestTimer.Stop();
                singleBacktestTimer = null;
            }

            if (MainObj.IsBacktesting) 
            {
                //just kill everything
                tradeEngine.DataContext = db;
                var trades = tradeEngine.AllTrades;
                foreach (var trade in trades)
                {
                    //set all to STOPPED, including market entry
                    trade.GlobalStatus = TradeGlobalStatus.STOPPED;
                    trade.ItemStatus = TradeItemStatus.CLOSED;
                }
                db.SaveChanges();
            }

            mainViewModel.BacktestResultEnabled = false;
            MainObj.IsBacktesting = false;
            //MainObj.TradeStatusChanged();
            Chart.canDraw = true;
            isPaused = false;
            MainObj.DrawCurrentDispatcher();
        }

        

        #endregion

    }
}
