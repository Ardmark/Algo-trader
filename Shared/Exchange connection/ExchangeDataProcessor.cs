using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using static System.Net.Mime.MediaTypeNames;

namespace AlgoTrader.Shared.ExchangeConnection
{
    public abstract class ExchangeDataProcessor
    {
        const bool KillAllProcessOnStart = true; //this will kill all multiple process on connect (and reconnect after pause)
        const int CONNECTION_MONITOR_INTERVAL = 5; //seconds
        const int CONNECTION_RETRY_INTERVAL = 60; // wait time before killing connection and restarting, in seconds
        //const int INITIAL_CONNECTION_WAIT_TIME = 20; //as above but before any data is sent - it seems that the first connection always fails so a second is needed anyway

        #region Fields
        protected Process process = null;
        protected readonly ExchangeId exchangeId;
        protected string projectDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
        protected string connectorPath;
        protected DateTime lastCaptureTimestamp;
        protected int id;
        protected DateTime lastProcessTimestamp;
        protected DispatcherTimer timer;
        protected DispatcherTimer cleanupTimer;
        public bool IsConnected { get; set; } = false;
        public bool IsPaused { get; set; } = false;
        protected readonly object thisLock = new object();
        protected Dictionary<string, _SymbolDataItem> currentSymbols = new Dictionary<string, _SymbolDataItem>();
        int connectionWaitTime = 0;
        public static List<string> symbolNamesToUpdate = new List<string>();
        #endregion

        protected abstract void CaptureOutput(object sender, DataReceivedEventArgs e);

        internal virtual void AddRequest(ExchangeRequest exchangeCommand, EventHandler<ExchangeEventArgs> eventHandler, params (dynamic, dynamic)[] data)
        {
        }

        public ExchangeDataProcessor(ExchangeId exchangeId)
        {
            this.exchangeId = exchangeId;

            if (MainObj.READ_LIVE_DATA)
            {
                timer = new DispatcherTimer();
                lastProcessTimestamp = DateTime.UtcNow;

                //set up a timer to monitor the process
                //this is where the connection will be opened
                timer.Tick += new EventHandler((source, eventArgs) =>
                {
                    DateTime dt = DateTime.UtcNow;

                    //get time elapsed from last item returned to the trade engine for processing
                    double d1 = dt.Subtract(lastProcessTimestamp).TotalSeconds;

                    if (d1 > connectionWaitTime)
                    {
                        if (connectionWaitTime != 0) //INITIAL_CONNECTION_WAIT_TIME)
                        {
                            Debug.WriteLine(string.Format("{0:0.00} seconds dropout of captured data, {1:0.00} seconds of process dropout at:   {1}", d1, dt));
                            LogConnection.WriteLineTimestamp(string.Format("++++++++++++  >{0:0.00} seconds dropout of data, restarting process ++++++++++++", connectionWaitTime));
                        }
                        lastProcessTimestamp = DateTime.UtcNow;
                        connectionWaitTime = CONNECTION_RETRY_INTERVAL; //only allow one short initial connection wait
                        
                        if (!OpenConnection()) //by waiting until this loop, MainObj.processName is now available
                        {
                            LogConnection.WriteLineTimestamp("Open connection failed, retrying in " + connectionWaitTime.ToString());
                        }
                    }
                });

                cleanupTimer = new DispatcherTimer();
                cleanupTimer.Tick += new EventHandler((source, eventArgs) =>
                {
                    Process[] processes = Process.GetProcessesByName(MainObj.processName);
                    if (processes.Count() > 1)
                    {
                        LogConnection.WriteLine("Cleaning up processes: ");
                        KillAllProcesses();
                    }
                });


                //this was just eating memory and taking forever to get past, so it has been moved to MainObj
                //main problem was not going ToList() as it re-queries the db each loop otherwise, but it is still unnecessary here

                //get all the last item timestamps in one go. It saves doing it on the fly as each symbol is received for the first time,
                //which slows the app down and causes the UI to be sticky during the first few minutes, intermittently
                //LogTrades.WriteLine($"Getting last item timestamps...");
                //using (DataContext dbInit = new DataContext())
                //{
                //    var lastSaved = dbInit.SymbolDataItems.AsNoTracking().AsParallel().GroupBy(x => x.SymbolName);
                //    foreach (var v in lastSaved)
                //    {
                //        var lastItemAdded = v.OrderByDescending(x => x.Timestamp).FirstOrDefault();
                //        if (lastItemAdded != null)
                //        {
                //            var id = GetSymbolId(lastItemAdded);
                //            lastItemsAdded[id] = lastItemAdded;
                //        }
                //    }
                //}

                lastItemsAdded = MainObj.lastItemsAdded;

                LogTrades.WriteLine("Opening data connection...");
                OpenConnection();
                StartTimers();
            }
        }

        #region Timer
        void StartTimers()
        {
            if (timer != null && !timer.IsEnabled)
            {
                timer.Interval = new TimeSpan(0, 0, CONNECTION_MONITOR_INTERVAL);
                timer.Start();
            }

            if (cleanupTimer != null && !cleanupTimer.IsEnabled)
            {
                cleanupTimer.Interval = new TimeSpan(24, 0, 0);
                cleanupTimer.Start();
            }
        }

        void StopTimers()
        {
            if (timer != null && timer.IsEnabled)
            {
                timer.Stop();
            }

            if (cleanupTimer != null && cleanupTimer.IsEnabled)
            {
                cleanupTimer.Stop();
            }
        }
        #endregion

        #region Connection

#pragma warning disable CS0162

        protected virtual bool OpenConnection()
        {
            try
            {
                //don't use process.Close() first
                if (KillAllProcessOnStart)
                {
                    KillAllProcesses();
                }
                else
                {
                    CloseConnection();
                }


                string filename = string.Format(@"c:/Program Files/nodejs/{0}.exe", MainObj.processName);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    UseShellExecute = false,   // This is important
                    CreateNoWindow = true,     // This is what hides the command window.
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    FileName = filename,
                    Arguments = connectorPath,
                    RedirectStandardInput = true,
                };


                process = Process.Start(psi);

                id = process.Id;
                LogConnection.WriteLine(string.Format("Starting process {0} at {1}", id, DateTime.UtcNow.ToString()));

                process.OutputDataReceived += CaptureOutput;
                process.ErrorDataReceived += CaptureError;

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                IsConnected = true;
                lastCaptureTimestamp = DateTime.UtcNow;
                LogConnection.WriteLine("Successfully opened connection");

                return IsConnected;
            }
            catch { }
            return false;
        }


        //this won't kill processes other than those started in this app
        internal void KillAllProcesses()
        {
            Process[] processes = Process.GetProcessesByName(MainObj.processName);
            if (processes.Count() > 0)
            {
                LogConnection.WriteLine(string.Format("Killing {0} process{1}", processes.Count(), processes.Count() > 1 ? "es" : ""));
            }
            foreach (Process p in processes)
            {
                try
                {
                    //don't use p.Close() first
                    p.Kill();
                    p.Dispose();
                }
                catch 
                {
                }
            }
            process = null;
        }

        internal void CloseConnection()
        {
            if (process != null)
            {
                try
                {
                    LogConnection.WriteLine(string.Format("Stopping process {0} at {1}", id, DateTime.UtcNow.ToString()));
                    process.Kill(); //otherwise will get a long list of these still-running processes during development, which will slow the first one down until it is no longer reading data at current time, but some time before that (presumably the stream is cached)
                    process.Dispose();
                    process = null;
                }
                catch 
                {
                }
            }
            IsConnected = false;
        }

        internal bool ToggleConnection()
        {
            if (IsConnected)
            {
                LogConnection.WriteLine("Toggled connection to OFF");
                CloseConnection();
                StopTimers();
                return false;
            }
            else
            {
                LogConnection.WriteLine("Toggled connection to ON");
                OpenConnection();
                StartTimers();
                return true;
            }
        }

        internal void RestartConnection()
        {
            LogConnection.WriteLine("Restarted connection");
            OpenConnection();
            StartTimers();
        }
        #endregion

        #region Capture error
        DateTime lastCaptureTime = DateTime.UtcNow.AddMinutes(-1);
        void CaptureError(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    string s = string.Format($"{Enum.GetName(typeof(ExchangeId), exchangeId)} CaptureError at {DateTime.UtcNow:G}");
                    LogConnection.WriteLine($"{s} : {e.Data}");
                  
                    if (DateTime.UtcNow.Subtract(lastCaptureTime).TotalMinutes > 1) //or will spit out many lines
                    {
                        //LogConnection.WriteLine($"{s} : {e.Data}");
                        lastCaptureTime = DateTime.UtcNow;
                         LogConnection.WriteLine($"Restarting connection...");
                         RestartConnection();
                    }

                }
            }
            catch { }
        }
        #endregion

        #region Serialization

        /*
         * The tables are filled with 'Interrupted' elements where necessary, so we have to see how many should be added each time, but
         *  don't want to poll the entire db to get the last one all the time, so will save the last one each time in a list
         *  */

        //will combine the exchange and the symbol name into a single string, to identify symbol 
        Dictionary<string, _SymbolDataItem> lastItemsAdded;// = new Dictionary<string, SymbolDataItem>();

        //internal string GetSymbolId(_SymbolDataItem item)
        //{
        //    return string.Concat(item.SymbolName, ":", item.ExchangeId.ToString());
        //}
        

        protected List<_SymbolDataItem> GetItemsToSave(_SymbolDataItem item, string symbolName)
        {
            //var itemsToAdd = GetMissedItems(item); //fill in any gaps

            var itemsToAdd = new List<_SymbolDataItem>();
            var id = MainObj.GetSymbolId(item, symbolName);

            //lastItemsAdded is pre-populated from the db
            bool hasPreviousItem = lastItemsAdded.TryGetValue(id, out _SymbolDataItem lastItemAdded);

            //get number of minutes missed and create that number of SymbolDataItems, with values copied from param
            //here there are rounding issues: the timestamp for the item will be 59.99999 seconds
            //despite this, it saves out rounded up and behaves here as if it is rounded up???
            //so to be safe, round it up anyway

            if (hasPreviousItem)
            {
                var numMins = Math.Ceiling(item.Timestamp.Subtract(lastItemAdded.Timestamp).TotalMinutes);
                while (numMins-- > 1)
                {
                    var newItem = lastItemAdded.Clone();
                    newItem.Interrupted = true;
                    lastItemAdded.Timestamp = lastItemAdded.Timestamp.AddMinutes(1);
                    newItem.Timestamp = lastItemAdded.Timestamp;
                    itemsToAdd.Add(newItem);
                }

                MainObj.AddItems(itemsToAdd, symbolName); //add as non-live items
                MainObj.AddItem(item, symbolName, true); //add the live item
                itemsToAdd.Add(item); //return all items to add
            }
            //update last item added in dictionary
            lastItemsAdded[id] = item;
            return itemsToAdd;
        }

#endregion

        protected _SymbolDataItem GetCurrentSymbolDataItem(string symbol)
        {
            if (!currentSymbols.ContainsKey(symbol))
            {
                currentSymbols.Add(symbol, new _SymbolDataItem
                {
                    High = double.MinValue,
                    Low = double.MaxValue,
                    Open = 0,
                    Close = 0,
                    //ExchangeId = exchangeId,
                    //SymbolName = symbol
                }
                );
            }
            return currentSymbols[symbol];
        }

        ////no idea why this is not used in the Binance connector, but the saved symbol should be reset 
        //protected void SetCurrentSymbolDataItem(string symbol,_SymbolDataItem symbolDataItem)
        //{
        //    currentSymbols[symbol] = symbolDataItem;
        //}

        public virtual void SendCommandToExchange(string command, dynamic data)  { }


        //internal List<SymbolDataItem> GetMissedItems(SymbolDataItem item) 
        //{
        //    var ret = new List<SymbolDataItem>();
        //    var id = GetSymbolId(item);

        //    bool hasPreviousItem = lastItemsAdded.TryGetValue(id, out SymbolDataItem lastItemAdded);
        //    if (!hasPreviousItem)
        //    {
        //        //get last saved value from db
        //        using (DataContext db = new DataContext())
        //        {
        //            var lastSaved = db.SymbolDataItems.AsNoTracking().AsParallel().Where(x => x.ExchangeId == item.ExchangeId && x.SymbolName == item.SymbolName).OrderByDescending(x => x.Timestamp).FirstOrDefault();
        //            if (lastSaved != null)
        //            {
        //                lastItemsAdded[id] = lastItemAdded = lastSaved;
        //                hasPreviousItem = true;
        //            }
        //        }
        //    }
        //    //get number of minutes missed and create that number of SymbolDataItems, with values copied from param
        //    //here there are rounding issues: the timestamp for the item will be 59.99999 seconds
        //    //despite this, it saves out rounded up and behaves here as if it is rounded up???
        //    //so to be save, round it up anyway

        //    if (hasPreviousItem)
        //    {
        //        var numMins = Math.Ceiling(item.Timestamp.Subtract(lastItemAdded.Timestamp).TotalMinutes);
        //        while (numMins-- > 1)
        //        {
        //            var newItem = lastItemAdded.Clone();
        //            newItem.Interrupted = true;
        //            lastItemAdded.Timestamp = lastItemAdded.Timestamp.AddMinutes(1); 
        //            newItem.Timestamp = lastItemAdded.Timestamp;
        //            ret.Add(newItem);
        //        }

        //    }

        //    //update last item added in dictionary
        //    lastItemsAdded[id] = item;

        //    //return list so others can be appended, then the whole lot saved in one go (don't include the item passed in param)
        //    return ret;
        //}

    }



}
