using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.ExchangeConnection;
using AlgoTrader.Shared.Interfaces;
using AlgoTrader.Shared.Logging;
using Shared.Trading;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AlgoTrader.Shared
{
    public static partial class MainObj
    {
        #region Per-project constants
        public static int NUM_DAYS_TO_SHOW; //this is not an exact figure, just skips all but this number of weeks then looks for the first midnight boundary
        public static bool READ_LIVE_DATA; //if true, will update items live. If writing to DB, this is ignored
        public static bool ALLOW_LIVE_TRADES = false;
        public static bool WRITE_TO_DB;
        public static int INITIAL_TIMEFRAME;
        public static int DATA_RETENTION_DAYS;

        //just to define the build type at runtime
        public static bool BACKTEST = false;

        //public static bool AUTO_ORDER = false;
        public static int START_HOUR { get; set; } = 10;
        public static int END_HOUR { get; set; } = 21;
        #endregion

        #region Private fields
        private static List<DisplayPanel> currentPanelList;
        static bool readingOldData = false;
        public static int rateCount = 0;
        public static DateTime rateTime = DateTime.UtcNow;
        #endregion

        #region Public fields
        public static readonly Dictionary<string, List<DisplayPanel>> displayPanelsForSymbol = new Dictionary<string, List<DisplayPanel>>();
        public static IMainViewModel mainViewModel;
        public static TradeEngine visibleTradeEngine;
        public static ExchangeConnector exchangeConnector;
        public static Canvas chart_Canvas, indicator1_Canvas;
        public static string processName;
        public static bool invertChart;
        public static Chart visibleChart;
        public static bool liveTradeEnabledAuto = false;
        public static List<int> timeFrames = new List<int>();
        public static Canvas sidePanel;
        public static bool initComplete = false;
        #endregion

        #region Properties

        public static string CurrentSymbolPrice
        {
            get
            {
                if (visibleTradeEngine != null && visibleTradeEngine.currentDataItem != null)
                {
                    return visibleTradeEngine.currentDataItem.Close.ToString();
                }
                return null;
            }           
         }
        private static bool isBacktesting;
        public static bool IsBacktesting
        {
            get
            {
                if (BACKTEST)
                    return isBacktesting;
                else
                    return false;
            }
            set { isBacktesting = value; }
        }
        public static string TradeSummary
        {
            set
            { 
                if (mainViewModel != null)
                {
                    mainViewModel.DisplayTradeSummary(value);
                }
            }
        }
        private static string currentSymbol;
        public static string CurrentSymbol
        {
            get { return currentSymbol; }
            set
            {
                try
                {
                    currentSymbol = value;
                    currentPanelList = displayPanelsForSymbol[currentSymbol];
                    visibleChart = GetChartForSymbol(CurrentSymbol);
                    visibleTradeEngine = GetTradeEngineForSymbol(currentSymbol);
                }
                catch { }

            }
        }

        public static ExchangeId CurrentExchange
        {
            get
            {
                return TradeEngine.EXCHANGE;
            }
        }

        private static int timeFrame;
        public static int TimeFrame
        {
            get
            {
                return timeFrame;
            }
            set
            {
                if (value != timeFrame)
                {

                    timeFrame = value;
                    InitPanels();

                    foreach (var s in SymbolNames.GetSymbolNames())
                    {
                        GetChartForSymbol(s)?.OnTimeframeChange(timeFrame, value);
                    }
                    DrawCurrentDispatcher();
                }
            }
        }

        public static int TotalItems
        {
            get
            {
                return visibleTradeEngine.results.Count;
            }
        }

        public static Dictionary<string, _SymbolDataItem> lastItemsAdded = new Dictionary<string, _SymbolDataItem>();
        #endregion

        
        //static object thisLock = new object();
        //public static List<int> obj = new List<int>();
        //public static void Test(string s)
        //{
        //    lock (thisLock)
        //    {
        //        Debug.WriteLine("++++++++++++++++++++++++++++++++++" + s);
        //        Thread.Sleep(10000);
        //    }
        //}

        
        #region Init
        static Grid _panelsContainer;
        static DateTime startTime = DateTime.UtcNow;
        public static void Setup(Grid panelsContainer, Canvas chartCanvas, Canvas timeBarCanvas, params Canvas[] indicatorCanvases)
        {
            //System.Threading.Tasks.Task.Run(() => { Test("first"); });
            //System.Threading.Tasks.Task.Run(() => { Test("second"); });

            _panelsContainer = panelsContainer;

            LogBase.StartSession();


            #region Remove duplicates
       

            //if (false)
            //{
            //    var newItems = new List<SymbolDataItem>();
            //    var all_Data = new List<SymbolDataItem>();

            //        LogConnection.WriteLine("Getting initial data list");
            //        all_Data = db.SymbolDataItems.AsNoTracking().AsParallel().ToList();
            //        LogConnection.WriteLine($"+++ all data found, {all_Data.Count()} items");
            //        LogConnection.WriteLine("+++ grouping data found");
            //        foreach (var group in initialDataList)
            //        {

            //            LogConnection.WriteLine($"+++++++ group {group.Key}");
            //            var duplicates = group.AsParallel().GroupBy(x => x.Timestamp).Where(x => x.Count() > 1).ToList();
            //            LogConnection.WriteLine($" ++ {duplicates.Count()} duplicates found");
            //            for (int i = 0; i < duplicates.Count(); i++)
            //            {
            //                var v = duplicates[i];
            //                if (v.Count() > 1)
            //                {
            //                    v.First().Close = -1;
            //                }
            //            }
            //        }

            //        LogConnection.WriteLine($"+++ all data now has {all_Data.Count()} items");


            //        newItems = all_Data.Where(x => x.Close != -1).OrderBy(x => x.Timestamp).ToList();
            //        var c = all_Data.Where(x => x.Close == -1).Count();

            //        LogConnection.WriteLine($"There are {c} items to remove");
            //        LogConnection.WriteLine($"Dropping old table");
            //        db.Configuration.AutoDetectChangesEnabled = false;
            //        var query = "drop table dbo.SymbolDataItems";
            //        db.Database.ExecuteSqlCommand(query);

            //        query = "SET ANSI_NULLS ON";
            //        db.Database.ExecuteSqlCommand(query);
            //        query = "SET QUOTED_IDENTIFIER ON";
            //        db.Database.ExecuteSqlCommand(query);

            //        query = @"CREATE TABLE [dbo].[SymbolDataItems] (
            //                    [Id]          INT IDENTITY(1, 1) NOT NULL,
            //                    [Timestamp]   DATETIME           NOT NULL,
            //                    [Open]        FLOAT(53)          NOT NULL,
            //                    [Close]       FLOAT(53)          NOT NULL,
            //                    [High]        FLOAT(53)          NOT NULL,
            //                    [Low]         FLOAT(53)          NOT NULL,
            //                    [SymbolName]  NVARCHAR(MAX)      NULL,
            //                    [ExchangeId]  INT                NOT NULL,
            //                    [Interrupted] BIT                NOT NULL
            //                    ); ";
            //        db.Database.ExecuteSqlCommand(query);
            //        db.Configuration.AutoDetectChangesEnabled = true;

            //    }

            //    LogConnection.WriteLine($"Building new table:");

            //    using (DataContext db1 = new DataContext())
            //    {
            //        int count = 0;
            //        int total = newItems.Count;
            //        int step = 1000;
            //        int stepCount = 0;
            //        db1.Configuration.AutoDetectChangesEnabled = false;
            //        foreach (var item in newItems)
            //        {
            //            item.ExecuteInsertCommand(db1);

            //            count++;
            //            stepCount++;
            //            if (stepCount == step)
            //            {
            //                var pc = ((double)count / total) * 100;
            //                LogConnection.WriteLine(pc.ToString() + "% completed");
            //                stepCount = 0;
            //            }
            //        }
            //        db1.Configuration.AutoDetectChangesEnabled = true;
            //        LogConnection.WriteLine($"Completed!");
            //    }

            //    return;
            //}

            #endregion

            #region Remove data older than set number of days

            //   This works, but could lose archiveable data, so it is commented
            /*
            if (!BACKTEST)
            {
                //remove unwanted data
                //only done on startup, as it would be difficult to clear out and restart all the indicators
                if (DATA_RETENTION_DAYS > 0)
                {
                    var earliest = DateTime.UtcNow.AddDays(-DATA_RETENTION_DAYS);
                    var s = earliest.ToString("yyyy/MM/dd HH:mm:ss");
                    using (DataContext dbInit = new DataContext())
                    {
                        var query = $"delete from [dbo].[SymbolDataItems] where timestamp < '{s}'";
                        dbInit.Database.ExecuteSqlCommand(query);
                    }
                }
            }
            */
            #endregion

            //using current thread is necessary for UI init, but will block redrawing during initialisation
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogTrades.WriteLine("Initialising...");


                #region Path calculations (unused)
                /*   
                    string p;
                    p = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    p = AppDomain.CurrentDomain.BaseDirectory;
                    p = System.IO.Directory.GetCurrentDirectory();
                    p = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
                    p = Environment.CurrentDirectory;
                    //all the same
                */
                #endregion

                //set the correct process name for this version
                //will set process name to node for live build, node_working otherwise (can also use node_testing - these are just duplicated .exe files in the node executable folder)
                #region Set process name
                //process names are simply a DUPLICATED .exe in the node executable folder, must be a separate process for each project
                if (BACKTEST)
                {
                    processName = "node_working"; //so this could be binance or bitmex testing
                }
                else
                {
                    switch (TradeEngine.EXCHANGE)
                    {
                        case ExchangeId.BINANCE:

                            processName = "node_binance"; //nothing specific to binance, this is just a duplicated executable
                            break;
                        case ExchangeId.BITMEX:
                            processName = "node_bitmex";
                            break;
                    }
                }
                #endregion

                #region Sound
                var mediaPlayer = new MediaPlayer();
                mediaPlayer.Open(new Uri(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Media", ALERT_SOUND)));
                mediaPlayers.Add(ALERT_SOUND, mediaPlayer);
                mediaPlayer = new MediaPlayer();
                mediaPlayer.Open(new Uri(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Media", SPIKE_SOUND)));
                mediaPlayers.Add(SPIKE_SOUND, mediaPlayer);
                #endregion

                chart_Canvas = chartCanvas;
                if (indicatorCanvases.Length > 0)
                {
                    indicator1_Canvas = indicatorCanvases[0];
                }

                //get all symbols and add panel sets for these

                foreach (var s in SymbolNames.GetSymbolNames())
                {
                    Chart chart;
                    TradeEngine tradeEngine = null;
                    var panels = new List<DisplayPanel>();
                    displayPanelsForSymbol[s] = panels;

                    switch (TradeEngine.EXCHANGE)
                    {
                        case ExchangeId.BITMEX:
                        AddPanel(tradeEngine = new TradeEngineBitmex(s, 0), panels); //must be added before market and auto order panels, so as to get active trade before other panels are added
                            break;
                        case ExchangeId.BINANCE:
                        AddPanel(tradeEngine = new TradeEngineBinance(s, 0), panels); //must be added before market and auto order panels, so as to get active trade before other panels are added
                            break;
                    }

                    AddPanel(chart = new Chart(s, 0, tradeEngine, panelsContainer), panels);
                    
                    //any panels which display should go in MainViewModel.InitCallback(), just for consistency, and those should declare line thickness, etc. in constructor
                   
                    #region add panels from MainViewModel callback
                    //get the displayed indicators, some of which may be required by order indicators as well
                    var displayIndicators = mainViewModel.InitCallback(tradeEngine as TradeEngine, chart as Chart);
                    foreach (var p in displayIndicators)
                    {
                        AddPanel(p, panels);
                    } 
                    #endregion

                    #region add critical panels
                    Order_PriceBreak priceBreakOrderIndicator;
                    AddPanel(priceBreakOrderIndicator = new Order_PriceBreak(s, 0, tradeEngine, chart), panels);
                    tradeEngine.priceBreakOrderIndicator = priceBreakOrderIndicator;

                    //HMA price break indicator - this requires a HMA indicator, added in the MainViewModel callback
                    //Order_PriceBreakHMA hmaBreakOrderIndicator;
                    //AddPanel(hmaBreakOrderIndicator = new Order_PriceBreakHMA(s, 0, tradeEngine, chart), panels);
                    //tradeEngine.hmaPriceBreakOrderIndicator = hmaBreakOrderIndicator;

                    //just to draw the indicator, and to keep this separate from the other panels
                    //Order_Auto autoOrderIndicator = null;
                    //AddPanel(autoOrderIndicator = new Order_Auto(s, 0, tradeEngine, chart), panels);
                    //tradeEngine.autoOrderIndicator = autoOrderIndicator;


                    tradeEngine.chart = chart;

                    if (!BACKTEST)
                    {
                        //only enable live trading if not in backtest build, otherwise will not start live trading unless manually triggered
                        liveTradeEnabledAuto = true;
                    }
                    #endregion

                }

                CurrentSymbol = SymbolNames.GetSymbolNames().FirstOrDefault();

                //add drawing objects which are owned globally and not by individual canvases
                var strokeCol = Brushes.DarkGray;
                strokeCol.Freeze();
                Chart.currentTimeMarkerLine = new System.Windows.Shapes.Line
                {
                    Y1 = 0,
                    Stroke = strokeCol,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 6, 6 },

                };
                Chart.currentTimeMarkerLine.MouseDown += (sender, e) => e.Handled = false;

                    //this seems to have stopped working, so have added time marker line to chart canvas instead
                //panelsContainer.Children.Add(Chart.currentTimeMarkerLine); //container is a GRID, but you can only add lines and move them (perhaps other UI items) but NOT canvases

                Chart.timeBarChildCanvas = new Canvas(); //this needs to be added to another canvas container to be animateable
                timeBarCanvas.Children.Add(Chart.timeBarChildCanvas);

                chart_Canvas.MouseRightButtonUp += (object sender, System.Windows.Input.MouseButtonEventArgs e) =>
                {
                    mainViewModel.PopupMenuForOrderPlacement(sender, e);
                    e.Handled = false;
                    visibleChart.MouseUp();
                };

            });


            //main cpu-hogging here, but not blocking UI thread
            if (readingOldData)
            {
                return;
            }

            DateTime start = DateTime.UtcNow.Subtract(new TimeSpan(NUM_DAYS_TO_SHOW, 0, 0, 0));

            timeFrame = INITIAL_TIMEFRAME;
            
            #region get items
            /*
                * will only calculate the initial data item time on startup
                * this data will be stored in the TradeEngines' Results lists
                * when setting up for a new timeframe, this data is then read into the new moving average, as it will be current
                * thereafter, all moving averages get data added with each AddItem
                * */

            //NOTE - ideally, should cull to NUM_DAYS_TO_SHOW dynamically, but that would require culling from all the Results lists as well (moving indicators probably OK
            // since cannot cull less than that length, but would need to check for this)
            //This will be necessary for a remote system
            //Alternatively, just restart periodically on local system or add menu link to do this
            //Also, should drop all but 60 min data periodically, to free up memory, but this is not important and would be difficult to avoid conflicting with operation


            //return the number of bars to skip from a starting time, to land on the time boundary for the current timeframe
            //this should be at midnight for all timeframes, to match TradingView


            Dictionary<string, List<_SymbolDataItem>> allItems = new Dictionary<string, List<_SymbolDataItem>>();

            //need to add a Reference to System.Configuration for this:
            var value = System.Configuration.ConfigurationManager.ConnectionStrings["DataContext"];

            #region  extract all old data from Symbol table
          /*  
            /////////////////////////////////////////////////////////////////////////////////////////
            using (SqlConnection conn = new SqlConnection(value.ToString()))
            {
                conn.Open();

                //doing the entire query here, including order by Timestamp
                //this will then sequentially add all items, creating a new list when the first different item is found

                LogTrades.WriteLine("Grouping data by symbol name...");
                var query = $"select * from [dbo].[_SymbolDataItem] order by SymbolName, [Timestamp] ";
                using (SqlCommand command = new SqlCommand(query, conn))
                {
                    command.CommandTimeout = 0; //default is 15 secs, no good for huge DB
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        LogTrades.WriteLine("SQL query complete");
                        List<_SymbolDataItem> lastList = null;
                        string lastListId = null;
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                var symbolName = (string)reader["symbolName"];
                                var item = new _SymbolDataItem(reader, symbolName);
                                if (lastListId != symbolName)
                                {
                                    if (lastList != null) 
                                    {
                                        LogTrades.WriteLine($"Fetching {symbolName} from SymbolDataItems");
                                        lastList = lastList.OrderBy(x => x.Timestamp).ToList(); //to be safe
                                    }
                                    lastList = new List<_SymbolDataItem>();
                                    lastListId = symbolName;
                                    allItems.Add(lastListId, lastList);
                                }
                                lastList.Add(item);
                            }
                        }
                    }
                }

            }
            /////////////////////////////////////////////////////////////////////////////////////////
            */
            #endregion

            //create tables if needed
            using (SqlConnection conn1 = new SqlConnection(value.ToString()))
            {
                conn1.Open();
               
                foreach (var s in SymbolNames.GetSymbolNames())
                {
                    var q = $"SELECT count(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{s}'";
                    using (SqlCommand command = new SqlCommand(q, conn1))
                    {
                        try
                        {
                            var reader = command.ExecuteReader();
                            if (reader.HasRows)
                            {
                                reader.Read();
                                var values = new Object[reader.FieldCount];
                                reader.GetValues(values);
                                if ((int)values[0] == 0)
                                {
                                    LogTrades.WriteLine($"Creating table for {s}");

                                    _SymbolDataItem.CreateTableCommand(s, conn1);
                                }


                            }
                        }
                        catch (Exception e)
                        {

                        }
                    }
                }
            }

            #region update tables to remove columns, etc.
            /*
            using (SqlConnection conn2 = new SqlConnection(value.ToString()))
            {
                conn2.Open();

                foreach (var s in SymbolNamesBinance.SymbolNames())
                {
                    var q = $"alter table {s} drop column ExchangeId";
                    using (SqlCommand command = new SqlCommand(q, conn2))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            */
            #endregion

            #region fill individual tables
            /*
            /////////////////////////////////////////////////////////////////////////////////////////

            int threadCount = 0;


            //populate individual tables from SymbolDataItems table
            //ensure NUM_DAYS_TO_SHOW is maximum and all tables are deleted first
            foreach (var itemGroup in allItems)
            {
                LogTrades.WriteLine($"Initialising table {itemGroup.Key}");
                Task.Run(() =>
                {
                    var name = itemGroup.Key;

                    if (!SymbolNamesBinance.SymbolNames().Where(x => x == name).Any())
                    {
                        return;
                    }
                    threadCount++;

                    using (SqlConnection conn2 = new SqlConnection(value.ToString()))
                    {
                        conn2.Open();
                        foreach (var item in itemGroup.Value)
                        {
                            item.ExecuteInsertCommand(itemGroup.Key, conn2);
                        }
                    }
                    threadCount--;
                });
            }

            while (threadCount > 0)
            {
                Thread.Sleep(1000);
                LogTrades.WriteLine($"{threadCount} tables remaining");
            }

            //clear data out and start with individual tables
            foreach (var v in allItems)
            {
                v.Value.Clear();
            }
            allItems.Clear();

            /////////////////////////////////////////////////////////////////////////////////////////
                */
            #endregion

            //extract data from tables
            using (SqlConnection conn3 = new SqlConnection(value.ToString()))
            {
                conn3.Open();

                LogTrades.WriteLine("Fetching symbol data...");
                foreach (var s in SymbolNames.GetSymbolNames().OrderBy(x => x))
                {

                    var missCount = 0;
                    var interpCount = 0;
                    var pulledCount = 0;
                    var list = new List<_SymbolDataItem>();
                    var query2 = $"select * from [dbo].[{s}] where [Timestamp] > '{start:yyyy-MM-dd}' order by [Timestamp]"; //order should be by timestamp anyway
                    using (SqlCommand command = new SqlCommand(query2, conn3))
                    {
                        command.CommandTimeout = 0; //default is 15 secs, no good for huge DB
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {

                                LogTrades.WriteLine($"Checking {s} for missed and duplicated items...");

                                _SymbolDataItem prevItem = null;
                                allItems.Add(s, list);

                                while (reader.Read())
                                {
                                    pulledCount++;
                                    var item = new _SymbolDataItem(reader, s);
                                    if (prevItem != null)
                                    {
                                        //this works fine because the timestamps are all set to [x] min 0 secs
                                        //not sure how this would work with seconds set though
                                        //it ideally should save the 'partial' value and use that instead of interpolating from the previous one

                                        //if item is less than 1 minute from previous item, ignore it
                                        if (item.Timestamp.Subtract(prevItem.Timestamp).TotalMinutes < 1)
                                        {
                                            missCount++;
                                            continue;
                                        }
                                        while (item.Timestamp.Subtract(prevItem.Timestamp).TotalMinutes >= 2)
                                        {
                                            var interpItem = prevItem.Clone();
                                            interpItem.Timestamp = prevItem.Timestamp.AddMinutes(1);
                                            list.Add(interpItem);
                                            prevItem = interpItem;
                                            interpCount++;
                                        }
                                    }
                                    prevItem = item;
                                    list.Add(item);
                                    //currCount++;
                                    //if (currCount == 10000)
                                    //{
                                    //    currCount = 0;
                                    //    count++;
                                    //    Debug.WriteLine($"Interpolated {count * 10000} items");
                                    //}
                                }

                                if (missCount > 0 || interpCount > 0)
                                {
                                    LogTrades.WriteLine($"Interpolation completed and live data updated: {missCount} duplicated items removed, {interpCount} missed items added");
                                    LogTrades.WriteLine("Database not updated with missed or duplicated items, uncomment code to do this.");
                                }
                                else
                                {
                                    LogTrades.WriteLine("No missed or duplicated items found.");
                                }

                            }
                        }
                    }
                    #region Drop table and rebuild with contiguous data
                    //**********************************************************************************************//
                    //if you are going to run this, do it against the ENTIRE TABLE not just the data set being pulled
                    //do it only once, there may be a couple of missed items the next time around anyway.
                    //if (missCount > 0 || interpCount > 0)
                    //{
                    //    var totItems = list.Count();
                    //    double totCompleted = 0;
                    //    var outputCount = 0;
                    //    LogTrades.WriteLine("Dropping table, rebuilding with contiguous data. This may take some time. ");
                    //    var query3 = $"delete from [dbo].[{s}]"; //order should be by timestamp anyway
                    //    using (SqlCommand command1 = new SqlCommand(query3, conn3))
                    //    {
                    //        var rows = command1.ExecuteNonQuery();
                    //    }

                    //    foreach (var p in list)
                    //    {
                    //        p.ExecuteInsertCommand(s, conn3);

                    //        outputCount++;
                    //        totCompleted++;
                    //        if (outputCount > 10000)
                    //        {
                    //            outputCount = 0;
                    //            int completed = (int)(totCompleted / totItems * 100);
                    //            LogTrades.WriteLine($"{completed}% completed");
                    //        }
                    //    }
                    //    LogTrades.WriteLine($"Table rebuild complete.");
                    //} 
                    #endregion



                }
            }

            try
            {
                LogTrades.WriteLine("Initialising timeframes...");

                foreach (var s in SymbolNames.GetSymbolNames())
                {
                    var panelList = displayPanelsForSymbol[s];

                    foreach (DisplayPanel _panel in panelList)
                    {
                        _panel.Init();
                    }
                }
                using (DataContext dbInit = new DataContext())
                {
                    //to speed up init, only intialising timeframes which are being used for active trades
                    List<int> activeTimeframes = dbInit.Trades.Where(x => x.GlobalStatus == TradeGlobalStatus.ACTIVE && x.Type != TradeType.STUB_ORDER).Select(x => x.TimeFrame).Distinct().ToList();

                    if (!activeTimeframes.Contains(INITIAL_TIMEFRAME))
                    {
                        activeTimeframes.Add(INITIAL_TIMEFRAME);
                    }
                    foreach (var activeTF in activeTimeframes)
                    {
                        foreach (var s in SymbolNames.GetSymbolNames())
                        {
                            var panelList = displayPanelsForSymbol[s];

                            foreach (DisplayPanel _panel in panelList)
                            {
                                _panel.InitIndicators(activeTF);
                            }
                        }

                        timeFrames.Add(activeTF);
                    }
                }

                foreach (var s in SymbolNames.GetSymbolNames())
                {
                    var tradeEngine = GetTradeEngineForSymbol(s);
                    tradeEngine.firstTimestamp = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day).AddDays(1); //midnight tonight
                    bool hasFirstTimestamp = false;

                    if (allItems.TryGetValue(s, out var v))
                    {
                        var lastItemAdded = v.OrderByDescending(x => x.Timestamp).FirstOrDefault();
                        if (lastItemAdded != null)
                        {
                            var id = GetSymbolId(lastItemAdded, s);
                            lastItemsAdded[id] = lastItemAdded;

                            var firstTimeStamp = v.First().Timestamp;
                            if (tradeEngine.firstTimestamp.Subtract(firstTimeStamp).TotalSeconds > 0)
                            {
                                tradeEngine.firstTimestamp = firstTimeStamp;
                                hasFirstTimestamp = true;
                            }
                        }
                    }
                    if (!hasFirstTimestamp)
                    {
                        //no data at midnight as yet
                        LogTrades.WriteLine($"{tradeEngine.symbolName} newly added, data will not be available until after midnight");
                    }
                    
                }
            }
            catch
            {

            }

            #endregion

            #region add items
            //Send all the data. Once this is done, the data is re-obtained from the trade engines, to reset the moving averages etc. for timeframe changes
            //Each panel will send this data to its moving indicators, which will compute the result series
            //the data will be sent starting at a midnight boundary, to match TradingView

            //Without threading: 4 min 20 secs for 41 symbols
            //With threading: 1 min 27 secs!

            LogTrades.WriteLine("Adding data by symbol...");

            var initCount = 0;
            var numSymbols = allItems.Count();
            List<string> extraItems = new List<String>();
            foreach (var itemGroup in allItems)
            {
                Task.Run(() =>
                {
                    try
                    {
                        bool hasInitialised = false;
                        //only create the panel group if the symbol is active from the exchange, and / or it is in the list of symbols to be traded
                        //this is required because this is pulling data from the DB not from the exchange
                        if (displayPanelsForSymbol.TryGetValue(itemGroup.Key, out List<DisplayPanel> _panelList))
                        {
                            if (_panelList.GetPanelByType(typeof(TradeEngine)) is TradeEngine tradeEngine)
                            {
                                initCount++;
                                var items = itemGroup.Value.Where(x => DateTime.Compare(x.Timestamp, tradeEngine.firstTimestamp) >= 0).ToList();

                                foreach (DisplayPanel panel in _panelList)
                                {
                                    var c = items.Count;
                                    for (int i=0;i<c;i++)
                                    {
                                        panel.ItemAdded(items[i], true, false);
                                    }

                                }

                                hasInitialised = true;
                            }
                        }
                        if (!hasInitialised)
                        {
                            extraItems.Add(itemGroup.Key);
                        }
                    }
                    finally
                    {
                        //Debug.WriteLine($"{itemGroup.Key}");
                        numSymbols--;
                    }
                }
                );
            }

            //wait for completion of threaded section
            var lastSymbolCount = allItems.Count();
            while (true)
            {
                if (numSymbols == 0)
                {
                    break;
                }
                if (lastSymbolCount > numSymbols)
                {
                    //because LogTrades.WriteLine is not thread-safe, so must not use it in threaded code
                    var plural = numSymbols > 1 ? "s" : "";
                    LogTrades.WriteLine($"{numSymbols} symbol{plural} remaining to initialise...");
                }
                lastSymbolCount = numSymbols;

                Thread.Sleep(500); //fairly long loop so 500ms sleep time
            }

            if (extraItems.Count > 0)
            {
                LogTrades.WriteLine("Following symbols did not initialise (may not be in SymbolDataItems):");
                foreach (var v in extraItems)
                {
                    LogTrades.WriteLine(v);
                }
            }


            LogTrades.WriteLine("Initialising panels...");
            //trade engines are set to correct parameters for any active trades in RunTrade()

            InitPanels(); //to set up for current timeframe (just for display purposes)

            foreach (var v in allItems)
            {
                v.Value.Clear();
            }
            allItems.Clear();
            allItems = null;


            #endregion



            //finish any UI init here

            Application.Current.Dispatcher.Invoke(() =>
            {
                //can only run a DispatcherTimer from this thread, so InitConnector here.
                //launch the exchange connector last, to allow db etc. to fully initialise
                //must be done outside of initialisation task, or connection will be dropped
                exchangeConnector = new ExchangeConnector();

                mainViewModel.SetTimeframeMenuState(TimeFrame);
                mainViewModel.SetInitialTime(timeFrame);
                mainViewModel.SetConcurrentOrdersMenuState(TradeEngine.MAX_CONCURRENT_TRADES);
                mainViewModel.SetLeverageMenuState((int)TradeEngine.Leverage);
                MainObj.ScrollToEnd();
                TradeEngine tradeEngine = GetTradeEngineForSymbol(visibleChart.symbolName);
                TradeStatusChanged(tradeEngine);
                ActiveTradeSummary.Init();
            });

            initComplete = true; //marks so DB can now be backed up
        }

        public static void SetLeverageMenuState(int leverage)
        {
            //Application.Current.Dispatcher.Invoke(() =>
            //{
                mainViewModel.SetLeverageMenuState(leverage);
            //});
        }

        public static void OutputCompletedInitialisationTime()
        {
            var elapsedTime = DateTime.UtcNow.Subtract(startTime);
            LogTrades.WriteLine($"Initialisation complete in {elapsedTime.Minutes.ToString()} mins {elapsedTime.Seconds.ToString()} secs.");

        }


        public static void InitPanels()
        {
            if (!timeFrames.Contains(TimeFrame))
            {
                //just changing the timeframe. The trade engines will have all the data, so no need to pull from the db and sort into groups
                foreach (var s in SymbolNames.GetSymbolNames())
                {
                    var panelList = displayPanelsForSymbol[s];
                    foreach (DisplayPanel _panel in panelList)
                    {
                        _panel.InitTimeFrame(TimeFrame);
                    }
                }

                timeFrames.Add(timeFrame);//set last so any AddItem which happens won't get put in an unititialised panel
            }
            else 
            {
                foreach (var s in SymbolNames.GetSymbolNames())
                {
                    var panelList = displayPanelsForSymbol[s];
                    foreach (DisplayPanel _panel in panelList)
                    {
                        _panel.ReinitIndicators(TimeFrame);
                    }
                }
            }

            rateTime = DateTime.UtcNow;
        }
        #endregion

        #region Add item

        //this is called during init for interpolated items
        internal static void AddItems(List<_SymbolDataItem> items, string symbolName)
        {
            for (int i = 0; i < items.Count; i++)
            {
                AddItemDirect(items[i], displayPanelsForSymbol[symbolName], false);
            }
        }

        //this is called live for each new item
        internal static void AddItem(_SymbolDataItem item, string symbolName, bool liveItem)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Code causing the exception or requires UI thread access
                    var panelList = displayPanelsForSymbol[symbolName]; //exchange data, so there is a panel list created

                    AddItemDirect(item, panelList, liveItem);

                    foreach (DisplayPanel panel in panelList)
                    {
                        if (symbolName == CurrentSymbol)
                        {
                            mainViewModel.SetItemPrice(item);
                        }
                        panel.ItemUpdated(item, true);
                    }
                    if (liveItem)
                    {
                        DrawCurrentDispatcher();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception in AddItem(): " + ex.ToString());
            }
        }

        internal static void AddItemDirect(_SymbolDataItem item, List<DisplayPanel> panelList, bool liveItem)
        {
            foreach (DisplayPanel panel in panelList)
            {
                try
                {
                    panel.ItemAdded(item, true, liveItem);
                }
                catch { }
            }
        }

        #endregion

        #region Update item
        public static void ItemUpdated(_SymbolDataItem _item, string symbolName)
        {
            rateCount++;

            //just in case another Kline updates the item whilst this method is executing, will work with a cloned object.
            var item = _item.Clone();

            try
            {
                // Code causing the exception or requires UI thread access
                var panelList = displayPanelsForSymbol[symbolName]; //exchange data, so there is a panel list created
                foreach (DisplayPanel panel in panelList)
                {
                    panel.ItemUpdated(item);
                    if (symbolName == CurrentSymbol)
                    {
                        if (panel is TradeEngine)
                        {
                            if ((panel as TradeEngine).dataReady)
                            {
                                mainViewModel.SetItemPrice(item); //this only updates the price display, so wait for initial interpolation
                            }
                            else
                            {
                                //just in case symbol switched after data available for one, which would change the symbol price box but then would not change for an unavailable #-data symbol
                                mainViewModel.SetItemPrice(new _SymbolDataItem { Close = 0 });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception in ItemUpdated(): " + ex.ToString());
            }
        }

        public static int GetUpdateCount()
        {
            var elapsedMins = DateTime.UtcNow.Subtract(rateTime).TotalMinutes;
            int rate = (int)(rateCount / elapsedMins);
            rateTime = DateTime.UtcNow;
            rateCount = 0;
            return rate;
        }

        #endregion

        #region Add / remove panels
        public static void AddPanelForSymbol(DisplayPanel panel, string symbolName)
        {
            var panelList = displayPanelsForSymbol[symbolName];
            AddPanel(panel, panelList);
        }

        static void AddPanel(DisplayPanel panel, List<DisplayPanel> panels)
        {
            panels.Add(panel);

            //re-order so chart is first, trade engine last
            DisplayPanel chart = null;
            DisplayPanel tradeEngine = null;
            foreach (DisplayPanel p in panels)
            {
                if (p is Chart)
                {
                    chart = p;
                    panels.Remove(p);
                    break;
                }
            }
            foreach (DisplayPanel p in panels)
            {
                if (p is TradeEngine)
                {
                    tradeEngine = p;
                    panels.Remove(p);
                    break;
                }
            }
            if (chart != null)
            {
                panels.Insert(0, chart);
            }
            if (tradeEngine != null)
            {
                panels.Add(tradeEngine);
            }
        }

        #endregion
        
        #region Draw

        //draw on the UI data context, this method can only be called from UI events as it will execute on that thread,
        // and no 2 UI events will run concurrently anyway
        internal static void DrawImmediate()
        {
            Draw();
        }
        
        internal static void DrawCurrentDispatcher()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Draw();
            });
        }

        internal static void DrawCurrentDispatcherDelayed()
        {
            var timer = new System.Timers.Timer(1000)
            {
                Enabled = true,
                AutoReset = false
            };
            timer.Elapsed += new System.Timers.ElapsedEventHandler((sender, e) => {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Draw();
                    timer.Enabled = false; //although AutoReset = false will stop it from running again anyway
                    timer.Close();
                });
            });

        }
        private static void Draw()
        {
            if (visibleChart == null || currentPanelList == null)
            {
                return; //just for old db copy 
            }

            visibleChart.InitDraw(); 
            visibleChart.Draw();
 
            foreach (DisplayPanel panel in currentPanelList)
            {
                if (panel != visibleChart)
                {
                    //any chart panel won't draw unless it is visible chart, and that one has already been drawn
                    panel.Draw(true);
                }
            }

        }

        internal static void ScrollToEnd()
        {
            visibleChart?.ScrollToEnd();
            MainObj.DrawImmediate();
        }

        internal static void ScrollToStart()
        {
            visibleChart?.ScrollToStart();
            MainObj.DrawImmediate();
        }

        internal static void ScrollForward(bool page)
        {
            visibleChart?.ScrollForward(page);
            MainObj.DrawImmediate();
        }

        internal static void ScrollBack(bool page)
        {
            visibleChart?.ScrollBack(page);
            MainObj.DrawImmediate();
        }

        internal static void ScrollBy(double scrollAmount, bool forward)
        {
            if (MainObj.IsBacktesting == true)
            {
                return;
            }
            visibleChart?.ScrollBy(scrollAmount, forward);
            MainObj.DrawImmediate();
        }

        internal static void GoToFrame(int frame, int offsetFrames = 0, string symbolName = null)
        {
            visibleChart.GoToFrame(frame, offsetFrames);
            ScrollToEnd();
            DrawCurrentDispatcher();
        }

        internal static void GoToFrame_ChangeSymbol(int frame, int offsetFrames = 0, string symbolName = null)
        {
            if (symbolName != null && (symbolName != CurrentSymbol)) //not using multiple exchanges anyway
            {
                CurrentSymbol = symbolName; //sets visible chart and tradeEngine
            }

            visibleChart.GoToFrame(frame, offsetFrames);
            ScrollToEnd();
            DrawCurrentDispatcher();

        }

        internal static async void TradeStatusChanged(TradeEngine tradeEngine, bool redraw = true)
        {
            if (isBacktesting)
            {
                return;
            }

            List<_Trade> activeTrades = null;
            List<_Trade> waitingTrades = null;
            if (tradeEngine != null)
            {
                (activeTrades, waitingTrades) = await ActiveTradeSummary.Update(tradeEngine);
            }
            var currentSymbolPrice = visibleTradeEngine.currentDataItem != null ? PriceFormatter.Format(visibleTradeEngine.currentDataItem.Close) : null;

            Application.Current.Dispatcher.Invoke(() =>
            {
                mainViewModel.TradeStatusChanged(activeTrades, waitingTrades, CurrentSymbol, currentSymbolPrice);
                if (redraw)
                {
                    DrawImmediate();
                }
            });
        }

        internal static void SetCurrentSeconds(double countdownSeconds)
        {
            //mainViewModel.CurrentSeconds(((TimeFrame - TotalItems % TimeFrame - 1) * 60 + countdownSeconds));
            mainViewModel.CurrentSeconds(countdownSeconds);
        }
        #endregion

        #region Sound

        /*
         * Default message box sound is loud and annoying, it is the Exclamation sound (WindowsBackground.wav, not Exclamation.wav...) and it has been turned off in sound control panel
         * For a range of other good alert sounds, browse the Windows/Media folder 
         * */
        public const string ALERT_SOUND = "alarm-clock-ringing-01.mp3";// will not play the .wav files in windows/media, even after conversion
        public const string SPIKE_SOUND = "alarm-clock-ringing-02.mp3";//;
        static Dictionary<string, MediaPlayer> mediaPlayers = new Dictionary<string, MediaPlayer>();


        public static void PlaySound(string soundName)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!IsWithinTradingHours())
                    {
                        return;
                    }

                    var mediaPlayer = mediaPlayers[soundName];
                    mediaPlayer.Stop();
                    mediaPlayer.Volume = 1;
                    mediaPlayer.Play();
                });
            }
            catch (Exception e)
            { 
            } 
        }

        public static void StopSound()
        {
            foreach (var mediaPlayer in mediaPlayers)
            {
                mediaPlayer.Value.Stop();
            }
        }

        #endregion

        #region Utility methods

        public static TradeEngine GetTradeEngineForSymbol(string symbolName)
        {
            try
            {
                return displayPanelsForSymbol[symbolName].GetPanelByType(typeof(TradeEngine)) as TradeEngine;
            }
            catch
            {
                return null;
            }
        }

        public static Chart GetChartForSymbol(string symbolName)
        {
            try
            {
                return displayPanelsForSymbol[symbolName].GetPanelByType(typeof(Chart)) as Chart;
            }
            catch
            {
                return null;
            }
        }

        public static DisplayPanel GetPanelForSymbol(string symbolName, Type panelType)
        {
            try
            {
                return displayPanelsForSymbol[symbolName].GetPanelByType(panelType);
            }
            catch
            {
                return null;
            }
        }

        public static bool IsWithinTradingHours()
        {
            const int startHour = 9;
            const int startMinute = 0;
            const int endHour = 22;
            const int endMinute = 30;

            return DateTime.Compare(DateTime.Now, new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, startHour, startMinute, 0)) > 0 && DateTime.Compare(DateTime.Now, new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, endHour, endMinute, 0)) < 0;
        }




        public static bool IsOnTimeBoundary(int timeFrame, int frame)
        {
            //display time is not the same as internal bar time in bst
            var offsetFrames = 0;// 60 * BSTOffset;
            if (timeFrame == 1440)
            {
                offsetFrames -= 60 * START_HOUR; //START_HOUR includes BST offset
            }

            offsetFrames += 60 * TradeEngine.BSTOffset;

            frame += offsetFrames;
            return (frame + 1) % timeFrame == 0;  //e.g. on a 5-min timeframe, we want to run on frame 4, not on frame zero

        }
        
        internal static DateTime GetNextInterval_UI(DateTime timeStamp, _SymbolDataItem currentDataItem = null)
        {
            var endTime = currentDataItem == null ? timeStamp : currentDataItem.Timestamp; //if live, always go to next period after current time
            return endTime.AddMinutes(TimeFrame * 5); //just go +7 days or things can get messed up
                                       //var midnight = new DateTime(endTime.Year, endTime.Month, endTime.Day);
                                       //var nextInterval1 = midnight.AddHours(END_HOUR).AddMinutes(1); //just to avoid any rounding errors
                                       //var nextInterval2 = midnight.AddHours(START_HOUR + 24).AddMinutes(1);
                                       // return nextInterval1 > timeStamp ? nextInterval1 : nextInterval2;
        }
        
        internal static double GetPriceDiffPercent(_SymbolDataItem item, double price, TradePosition position)
        {
            //get the difference from trade close to a price, expressed as a fraction of the close
            //e.g. Trade close is 100, price is 80, difference fraction is 0.2
            //so this is the value which shows when dragging from the close down to a stop, i.e. stop == n% * close, not close == n% * stop

            return GetPriceDiff(item.Close, price, position);
        }
        
        internal static double GetPriceDiff(double from, double to, TradePosition position)
        {
            return position == TradePosition.LONG
                                ? from != 0 ? 1 - to / from : 0
                                : to != 0 ? 1 - from / to : 0;
        }

        internal static string GetSymbolId(_SymbolDataItem item, string symbolName)
        {
            return string.Concat(symbolName, ":", TradeEngine.EXCHANGE.ToString());
        }


        internal static void Exit()
        {
            try
            {
                exchangeConnector?.CloseConnection();
            }
            catch { }
        }

        public static bool TradeRunningLocally(_Trade trade)
        {
            return MainObj.IsBacktesting
                    || trade.ExchangeStatus == TradeExchangeStatus.ENTERED_OFF_EXCHANGE
                    || ((trade.Action == TradeAction.STOP || trade.Action == TradeAction.FLOOR || trade.Action == TradeAction.CEILING || trade.Action == TradeAction.TAKE_PROFIT) && TradeEngine.USE_SIMULATED_EXCHANGE == true);
        }


        #endregion

        #region unused
        //unused

        //this is the actual delegate used in Init, there are a number of pre-defined ones in the framework
        //public delegate T MyFunc<in TradeEngine, in Chart, out T>(Chart arg);

        /*
        internal static void ClearOrders(TradeType tradeType, DataContext db)
        {
            db.Trades.RemoveRange(dataContext_UI.Trades.Where(d =>
                        d.Type == tradeType &&
                        d.GlobalStatus == TradeGlobalStatus.STOPPED));

            db.SaveChanges();
        }

        //set significant figures
        double SetSigFigs(double d, int digits)
        {
            try
            {
                if (d == 0) return d;
                //get number of zeros
                var numZeros = Math.Floor(Math.Log10(Math.Abs(d)));
                decimal scale = (decimal)Math.Pow(10, numZeros + 1);
                return ((double)(scale * Math.Round((decimal)d / scale, digits)));
            }
            catch
            {
                return d;
            }
        }
        
        internal static void DisplayLogTrades(string s)
        {
            mainViewModel.DisplayLog(s);
        }
        */
        #endregion
    }
}
