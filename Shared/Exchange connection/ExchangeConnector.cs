using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AlgoTrader.Shared.ExchangeConnection
{
    /*Initialise the Exchange Data Processor(s), pump any events from the UI, and backup DB*/

    public class ExchangeConnector
    {
        public static int NUM_INCREMENTAL_BACKUPS = 0;
        public static string BACKUP_FOLDER;
        public static int BACKUP_FREQUENCY_HOURS = 24;

        public struct FeedDataItem
        {
            internal DateTime timestamp;
            internal double price;
            internal string symbolName;
        }
        static DateTime startTime;
        DateTime lastCaptureTimestamp; //unused but leave in
        //List<ExchangeDataProcessor> exchangeDataProcessors = new List<ExchangeDataProcessor>();
        ExchangeDataProcessor exchangeDataProcessor;


        public bool IsConnected
        {
            get
            {
                //foreach (var edp in exchangeDataProcessors)
                //{
                    if (exchangeDataProcessor.IsConnected)
                    {
                        return true;
                    }
                //}
                return false;
            }
        }

        public ExchangeConnector()
        {
            switch (TradeEngine.EXCHANGE)
            {
                case ExchangeId.BINANCE:
                    {
                        exchangeDataProcessor = new ExchangeDataProcessorBinance();
                        break;
                    }
                case ExchangeId.BITMEX:
                    {
                        exchangeDataProcessor = new ExchangeDataProcessorBitmex();
                        break;
                    }
            }

            lastCaptureTimestamp = DateTime.UtcNow; //unused

            startTime = DateTime.UtcNow;
            /*
             * +++   DO NOT CHANGE THE SOLUTION NAME   +++
             * get correct node executable to start - this allows the code to kill multiple processes but only for a specific instance of the program
             * i.e. the Save To DB version can still run when the testing or debug versions start and stop
             * There are 2 versions - save to dB, and testing, both use a separate version for release and debug (so debug can run and not affect a running release version)
             *  - keep the save to DB version exclusively for just doing that, run the testing version as Release for long test, Debug for development
             *  */

            if (MainObj.BACKTEST == false && BACKUP_FOLDER != null && NUM_INCREMENTAL_BACKUPS > 0)
            {
                try
                {
                    //start backup
                    DispatcherTimer backupTimer = new DispatcherTimer();
                    DateTime lastBackupTime = DateTime.UtcNow;

                    backupTimer.Tick += new EventHandler(async (source, eventArgs) =>
                    {
                        lastBackupTime = DateTime.UtcNow;
                        await BackupDB(); 
                    });

                    backupTimer.Interval = new TimeSpan(BACKUP_FREQUENCY_HOURS, 0, 0); 
                    backupTimer.Start();
                }
                catch
                {
                    //todo - callback
                    //App.ExceptionAlert("Exception thrown in MainObj", e.ToString());
                }
            }
        }

        //todo - sort out filename from: var value = System.Configuration.ConfigurationManager.ConnectionStrings["DataContext"];
        //set backup folder in Constants (possibly rename to Config???)
        //set up for sequential backups, although disk space may be an issue in Onedrive (perhaps have more than one folder???)

        public static async Task BackupDB()
        {
            if (MainObj.initComplete == false)
            {
                return;
            }
            try
            {
                var connString = System.Configuration.ConfigurationManager.ConnectionStrings["DataContext"];
                var csb = new SqlConnectionStringBuilder(connString.ToString());
                var dbName = csb["initial catalog"].ToString();
                var timeStamp = DateTime.UtcNow.ToString("dd/MM/yy HH:mm:ss");
                
                using (DataContext dbBackup = new DataContext())
                {
                    var files = Directory.EnumerateFiles(BACKUP_FOLDER).Where(x => x.Contains(dbName) && x.Contains(".bak"));
                    while (files.Count() > 0 && files.Count() >= NUM_INCREMENTAL_BACKUPS)
                    {
                        DateTime earliestFileDate = new DateTime(DateTime.UtcNow.Year + 1, DateTime.UtcNow.Month, DateTime.UtcNow.Day);
                        string earliestFile = null;
                        foreach (var f in files)
                        {
                            //var s = file.IndexOf(dbName) + dbName.Count;
                            var creationTime = File.GetCreationTime(Path.Combine(f));
                            if (creationTime < earliestFileDate)
                            {
                                earliestFileDate = creationTime;
                                earliestFile = f;
                            }
                        }
                        if (earliestFile != null)
                        {
                            File.Delete(earliestFile);
                        }

                    }
                    //filename will be DB name combined with ticks when created
                    string fileName = Path.Combine(BACKUP_FOLDER, dbName + "_" + DateTime.UtcNow.Ticks.ToString() + ".bak");
                    //if (File.Exists(fileName))
                    //{
                    //    FileStream fs = new FileStream(fileName, FileMode.Truncate); //need to zero the file or it will get appended
                    //    fs.Close();
                    //    fs.Dispose();
                    //}
                    //else
                    //{
                    FileStream fs = new FileStream(fileName, FileMode.CreateNew); //need to zero the file or it will get appended
                    fs.Close();
                    fs.Dispose();
                    //}

                    DateTime dt = DateTime.UtcNow;
                    var result = await dbBackup.Database.ExecuteSqlCommandAsync(System.Data.Entity.TransactionalBehavior.DoNotEnsureTransaction,
                    "BACKUP DATABASE @DB_Name TO DISK = @fileName",
                        new SqlParameter("@DB_Name", dbName),
                        new SqlParameter("@fileName", fileName)
                        );
                    double backupTime = DateTime.UtcNow.Subtract(dt).TotalMilliseconds;
                    TimeSpan upTime = DateTime.UtcNow.Subtract(startTime);
                    LogBackups.WriteLine(string.Format("Database backed up at {0} in {1}ms; uptime is {2} days, {3} hours, {4} mins, {5} secs", DateTime.UtcNow.ToString(), backupTime.ToString(), upTime.Days, upTime.Hours, upTime.Minutes, upTime.Seconds));
                }
            }
            catch (Exception e)
            {
                LogBackups.WriteLine("DB backup threw exception: " + e.ToString());
            }
        }



#region unused

        //currently unused but keep
        void MeasureReturnedDataTimeDifference(dynamic data)
        {
            try
            {
                //log the time difference between the capture time and the local time, i.e. how far out of sync this is
                //remember that the local time can be up to 1 second out
                //this is NOT the same as the time between process loops, which is always > 1 second and is logged above
                /*
                 * Can get this after a delay, where data comes through rapidly:
                 * 
                First there is a delay logged in the monitor loop (due to timing, this may not be the final delay which can be longer):
                    10.4956865 seconds dropout of captured data, 3.6881521 seconds of process dropout at:   12/08/2019 12:00:24

                Then the process loop logs a dropout in the actual data feed 
                (there was a dropout preceding this, hence the shorter apparent dropout time, which could be accounted for by preceding data
                 also being behind current time):
                    7.6527636 seconds elapsed since last process loop at 12/08/2019 12:00:28

                Then the data starts to come through:
                    14.17 seconds difference between returned data time and actual time at 12/08/2019 12:00:28 
                    10.50 seconds difference between returned data time and actual time at 12/08/2019 12:00:28 
                     9.72 seconds difference between returned data time and actual time at 12/08/2019 12:00:28 
                     8.65 seconds difference between returned data time and actual time at 12/08/2019 12:00:28 
                     7.61 seconds difference between returned data time and actual time at 12/08/2019 12:00:28 
                     6.65 seconds difference between returned data time and actual time at 12/08/2019 12:00:28 
                     5.93 seconds difference between returned data time and actual time at 12/08/2019 12:00:28 
                     5.34 seconds difference between returned data time and actual time at 12/08/2019 12:00:29 
                     4.51 seconds difference between returned data time and actual time at 12/08/2019 12:00:29 
                     3.96 seconds difference between returned data time and actual time at 12/08/2019 12:00:29 
                     3.08 seconds difference between returned data time and actual time at 12/08/2019 12:00:30 
                     2.45 seconds difference between returned data time and actual time at 12/08/2019 12:00:30 
                     1.65 seconds difference between returned data time and actual time at 12/08/2019 12:00:30
                   */
                double diff1 = DateTime.UtcNow.Subtract(lastCaptureTimestamp).TotalMilliseconds / 1000;
                if (diff1 > 2) //clock can be running slow so this could be a negative number
                {
                    DateTime lastTime = data[data.Count - 1].timestamp;
                    LogConnection.WriteLine(string.Format("{0} seconds difference between returned data time and actual time at {1} ", diff1.ToString("0.00"), DateTime.UtcNow));
                }
            }
            catch { }
        }

        internal virtual void AddRequest(ExchangeRequest exchangeCommand, EventHandler<ExchangeEventArgs> eventHandler, params (dynamic, dynamic)[] data)
        {
            try
            {
                exchangeDataProcessor.AddRequest(exchangeCommand, eventHandler, data);
            }
            catch { }
        }
        //internal void AddRequest(ExchangeRequest exchangeCommand, EventHandler handler, params (dynamic, dynamic)[] data)
        //{
        //    AddRequest(exchangeCommand, handler, data);
        //}


        internal void CloseConnection()
        {
            //foreach (var edp in exchangeDataProcessors)
            //{
            exchangeDataProcessor.CloseConnection();
            //}
        }

        internal void RestartConnection()
        {
            //foreach (var edp in exchangeDataProcessors)
            //{
            exchangeDataProcessor.RestartConnection();
            //}
        }

        internal bool ToggleConnection()
        {
            bool ret = false;
            //foreach (var edp in exchangeDataProcessors)
            //{
            if (exchangeDataProcessor.ToggleConnection())
            {
                ret = true;
            }
            //}
            return ret;
        }


#endregion
    }
}
