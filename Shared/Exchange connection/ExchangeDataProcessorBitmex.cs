using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using BitMEX;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace AlgoTrader.Shared.ExchangeConnection
{
    //this is a working system as of 7/11/2020
    //Not sure how to set orders, that uses websockets and that has not been investigated or implemented
    public class ExchangeDataProcessorBitmex : ExchangeDataProcessor
    {

        //Bitmex sends a data set of past events, so it is necessary to read only the events which have not yet been aggregated, take the last one,
        // and use that data. Candle timing is arbitrary so need to work on 1-min data sets and save out to DB when a complete minute has been taken.
        //update the currentItem field whenever new data is available, and write data out in compleded 1-minute chunks


        ExchangeConnector.FeedDataItem lastReadItem;
        DateTime lastSavedItemTime, lastReadItemTime;
        //for debugging...
        public int __iterationCount;
        public int __numItemsSaved;
        bool first = true;


        public ExchangeDataProcessorBitmex() : base(ExchangeId.BITMEX)
        {
            connectorPath = string.Format(@"""{0}""", Path.Combine(projectDirectory, "Bitmex connectors", "NodeConnector", "connector.js"));    //note the escaped " to allow spaces
            symbolNamesToUpdate = SymbolNames.GetSymbolNames().ToList();
        }

        //send command to the connector via StandardInput
        public override void SendCommandToExchange(string command, dynamic data) { } //unimplemented. Note, Binance has a websocket ticker like the bitmex data stream. 


        protected override void CaptureOutput(object sender, DataReceivedEventArgs e) 
        {
            if (e == null || e.Data == null) //seems to happen after breaking for some time
            {
                LogConnection.WriteLine("e.Data was NULL");
                return;
            }

            try
            {
                if (e.Data.Contains("WebSocket")) //this is sent as the first item after connection
                {
                    LogConnection.WriteLineTimestamp(e.Data);
                    return;
                }

                dynamic dyn = JsonConvert.DeserializeObject(e.Data);
                if (dyn.error != null)
                {
                    try
                    {
                        LogConnection.WriteLineTimestamp(dyn.error);
                    }
                    catch { }
                    //Bitmex errors, data is served as normal
                }
                if (dyn.message != null)
                {
                    try
                    {
                        LogConnection.WriteLineTimestamp(dyn.message);
                    }
                    catch { }
                    //any arbitrary message
                }
                if (dyn.symbol != null && dyn.table != null)
                {
                    double diff = DateTime.UtcNow.Subtract(lastProcessTimestamp).TotalMilliseconds / 1000;

                    if (diff > 60) //the process loop is always 1 second or longer
                    {
                        LogConnection.WriteLine(string.Format("{0:0.00} seconds elapsed since last process loop at {1}", diff, DateTime.UtcNow));
                        LogConnection.WriteLine("Restarting connection...");
                        RestartConnection();
                    }

                    lastProcessTimestamp = DateTime.UtcNow;
                    //the actual data
                    var symbol = dyn.symbol;
                    var table = dyn.table;

                    lock (thisLock)
                    {
                        //have had intermittent exceptions from all UI access without this
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                _SymbolDataItem currentItem = ProcessData(dyn);

                                if (currentItem != null)
                                {
                                    //var t = DateTime.UtcNow.Subtract(lastCaptureTimestamp).TotalSeconds;
                                    //if (t > 20)
                                    //{
                                    //    /*
                                    //     * this is always with 1000 data elements and last element is < element count, e.g.
                                    //     * - 5.08 seconds elapsed since last item timestamp; 1 items added, 1 iterations, 965 last iteration, 1000 data elements
                                    //     * This is preceded by a shorter difference in returned data time and actual time, perhaps this gets worse then suddenly
                                    //     * catches up, resulting in a sudden gap in current and last capture time. Often no items are added, indicating data count has reset to zero.
                                    //     * */
                                    //    LogConnection.WriteLine(string.Format("{0:0.00} seconds elapsed since last item timestamp; {1} items added, {2} iterations, last capture time: {3}, current time: {4}", t, __numItemsSaved, __iterationCount, lastCaptureTimestamp, DateTime.UtcNow));
                                    //}

                                    /*
                                        * NOTE:
                                        * The last capture timestamp is always the same as the last element timestamp in data (tested)
                                        * However, the latter will be behind the current time by a varying degree.
                                        * This can cause a dropout > allowed time which will force a process restart, but that is necessary as it has been found
                                        * that (with multiple processes, which seem to start by themselves) there can be a huge lag in time.
                                        * The auto-cleanup should sort that out but it could still happen during a single 24 hour period
                                        * */
                                    lastCaptureTimestamp = DateTime.UtcNow;

                                    //There is often a 2 to 4 second difference between returned data time and actual time
                                    //MeasureReturnedDataTimeDifference(data);

                                }
                                else
                                {
                                    LogConnection.WriteLineTimestamp("item returned was NULL");
                                }
                            }
                            catch { }
                        });
                    }
                }

            }
            catch (Exception ex)
            {
                LogConnection.WriteLineTimestamp("Bitmex capture output exception: " + ex.ToString());
            }
        }
        List<_SymbolDataItem> itemsToSave = new List<_SymbolDataItem>();
        protected _SymbolDataItem ProcessData(dynamic dyn)
        {
            __iterationCount = 0;

            _SymbolDataItem ret = new _SymbolDataItem();
            itemsToSave.Clear();

            string log = string.Empty;

            using (DataContext db = new DataContext())
            {
                try
                {
                    var symbol      = dyn.symbol;
                    //var table       = dyn.table;
                    var data        = dyn.data;
                    int dataCount   = data.Count;

                    if (dataCount == 0 || data[0] == null || data[dataCount - 1] == null)
                    {
                        LogConnection.WriteLineTimestamp("Data is null or count is zero");
                        return null;
                    }

                    DateTime ts = data[dataCount - 1].timestamp;

                    //if just launched, initialise with the very first data item, as if this was the previous item saved to the DB, so it will be in the previous minute
                    if (first)
                    {
                        DateTime ds = data[0].timestamp;
                        lastReadItem = new ExchangeConnector.FeedDataItem
                        {
                            timestamp = ds,
                            price = data[0].bidPrice,
                            symbolName = symbol
                        };
                        lastSavedItemTime = lastReadItemTime = lastReadItem.timestamp;

                        first = false;
                    }

                    _SymbolDataItem currentSymbolDataItem = GetCurrentSymbolDataItem(symbol.Value);
                   

                    //find the next entry in the data which is later than the last saved entry in the FeedDataItems list
                    //testing shows that it reads around 400 elements from the end of the data list each time, so stick with 1000 as the number downloaded
                    //find the first item which is imediately after the FeedDataItem param, by doing a binary partition search (or whatever it's called, quicksort type algorithm?)
                    var lastReadItemTimestamp = lastReadItem.timestamp;
                    int count = data.Count;
                    int end = count - 1;
                    int start = 0;
                    bool OK = true;

                    while (end - start > 0)
                    {
                        if (data[start] == null)
                        {
                            OK = false;
                            break;
                        }

                        if (end - start < 2)
                        {
                            //can only move one step foward now anyway, so this is the termination point
                            //but if start was set before end in the back step, start and end will be zero on the next back step
                            //this would result in an endless loop otherwise
                            start = end; //start was before timestamp, so end is the return value, or both are the same anyway (zero index)
                            break;
                        }

                        if (DateTime.Compare((DateTime)data[start].timestamp, lastReadItemTimestamp) <= 0)
                        {
                            start = start + (end - start) / 2;
                        }
                        else
                        {
                            //moving back, so new end is old start (since this now defines the bounds of the new cell), new start is start - (half way between start and end)
                            int _start = start;
                            start = start - (end - start) / 2;
                            end = _start;

                            //if some data has been dropped, the earliest timestamp might be later than the lastReadItem, which will result in a negative start position (obviously, it tries to partition before zero)
                            if (start < 0)
                            {
                                start = 0;
                            }
                        }
                    }
                    if (OK)
                    {
                        if (start < count)
                        {
                            lastReadItem.timestamp = data[start].timestamp;
                            lastReadItem.price = data[start].bidPrice;
                            lastReadItem.symbolName = data[start].symbol;
                            var symbolName = data[start].symbol.Value;

                            __iterationCount++;
                            DateTime nextMinute = GetAsMinute(lastSavedItemTime).AddMinutes(1);

                            //read forward from this position until rolling over to next minute, then save out
                            for (dataCount = start; dataCount < count; dataCount++)
                            {
                                dynamic obj = data[dataCount];
                                DateTime timestamp = GetAsMinute(obj.timestamp);

                                lastReadItem.price      = obj.bidPrice;
                                lastReadItem.timestamp  = obj.timestamp;
                                lastReadItem.symbolName = obj.symbol;

                                if (DateTime.Compare(nextMinute, timestamp) > 0)
                                {
                                    /*
                                     * edge case: runs to end of data, last element goes to next minute so item is saved.
                                     * next data set adds just one element, or no elements (i.e. same data set sent):
                                     *  - FindNextItem returns the last element, so there is just one iteration, currentItem is set to new BitmexDataItem so timestamp is 
                                     *    never changed. This could happen repeatedly so it will result in a long period of 'dropout' of captured data, apparently
                                     *    So must always set timestamp for returned object (which is done in currentItem.Update(...))
                                     * */

                                    currentSymbolDataItem.Update(lastReadItem);
                                    currentSymbolDataItem.CopyTo(ret); //just assignment and timestamp will be reset in ret when currentItem loses its seconds in SetTimestamp()
                                    //must clone currentItem as it will be changed next loop so that could change data somewhere else with unexpected results
                                    MainObj.ItemUpdated(currentSymbolDataItem.Clone(), symbolName); //don't want to do this dozens of times each update, just for the final aggregated value
                                }
                                else
                                {
                                    /*
                                    * edge case: runs to end of data, last element goes to next minute so item is saved.
                                    * next data set adds just one element, or no elements (i.e. same data set sent):
                                    *  - currentItem is null and ret is returned as a new BitmexDataItem with the timestamp of 0001,01,01
                                    *    so need to set the timestamp to the current item timestamp at this point
                                    *    */


                                    currentSymbolDataItem.SetTimestamp(); //will remove seconds to correctly set timestamp as start of minute

                                    //must clone currentItem as it will be changed next loop so that could change data somewhere else with unexpected results
                                    itemsToSave.AddRange(GetItemsToSave(currentSymbolDataItem.Clone(), symbolName)); //this adds to the private list in case this is called in a loop

                                    //Debug.WriteLine($"{currentSymbolDataItem.Open}, {currentSymbolDataItem.High}, {currentSymbolDataItem.Low}");    
                                    lastSavedItemTime = lastReadItem.timestamp;
                                    nextMinute = timestamp.AddMinutes(1);

                                    //currentSymbolDataItem is a reference from the currentSymbols[] array, so need to hold on to this - 
                                    // creating a new object will prevent the retrieved object from being updated, obviously
                                    currentSymbolDataItem.High = currentSymbolDataItem.Low = currentSymbolDataItem.Open = currentSymbolDataItem.Close = lastReadItem.price;
                                    currentSymbolDataItem.CopyTo(ret);
                                }
                            }


                            __numItemsSaved = itemsToSave.Count;
                            if (MainObj.WRITE_TO_DB == true)
                            {
                                if (itemsToSave.Count > 0)
                                {
                                    var value = System.Configuration.ConfigurationManager.ConnectionStrings["DataContext"];
                                    using (SqlConnection conn = new SqlConnection(value.ToString()))
                                    {
                                        conn.Open();

                                        foreach (var p in itemsToSave)
                                        {
                                            lastProcessTimestamp = DateTime.UtcNow; //stops reconnection during long saves, probably not an issue with this but EF methods just took hours
                                            p.ExecuteInsertCommand(symbolName, conn);

                                        }
                                    }
                                }
                            }
                            if (symbolNamesToUpdate.Count > 0 && symbolNamesToUpdate.Contains(lastReadItem.symbolName))
                            {
                                symbolNamesToUpdate.Remove(symbolName);

                                if (symbolNamesToUpdate.Count > 0)
                                {
                                    var s = string.Join(", ", symbolNamesToUpdate);
                                    LogConnection.WriteLine($"Symbols remaining to connect: {s}");
                                }
                                else
                                {
                                    LogConnection.WriteLine("All symbols connected.");
                                    MainObj.OutputCompletedInitialisationTime();
                                }
                            }

                            var elapsedSecs = lastReadItem.timestamp.Subtract(lastReadItemTime).TotalSeconds;
                            lastReadItemTime = lastReadItem.timestamp;
                            MainObj.SetCurrentSeconds(elapsedSecs);
                        }
                    }
                    else
                    {
                        log += "Could not find next item at " + DateTime.UtcNow.ToString();
                    }

                }
                catch (Exception ex)
                {
                    log += ex.ToString();
                }

                //it is returning every one or 2 seconds, the frequency of the data feed

                if (log != string.Empty) LogConnection.WriteLine(log);
                return ret;
            }
        }


        private DateTime GetAsMinute(dynamic timestamp)
        {
            DateTime dt = timestamp;
            //must remove the seconds and milliseconds
            return new DateTime(
                dt.Year,
                dt.Month,
                dt.Day,
                dt.Hour,
                dt.Minute,
                0
                );
        }
    }
}
