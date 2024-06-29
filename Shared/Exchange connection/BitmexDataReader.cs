using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Bitmex_trader
{
    public struct FeedDataItem
    {
        internal DateTime timestamp;
        internal float price;
    }

    internal class BitmexDataReader
    {
        #region Fields
        BitmexDataItem currentItem = null; //Current item - this is constantly updated and can be used to run live trades against
        List<BitmexDataItem> itemsToAdd = new List<BitmexDataItem>();
        FeedDataItem lastReadItem, lastSavedItem;
        readonly DateTime lastBackupTime;
        bool first = true;
        DataContext db; //might this help prevent concurrency issues by this class having its own data context?
        #endregion

        #region Properties
        DateTime lastReadDateTime;
        public string LastReadTime
        {
            get
            {
                return lastReadDateTime.ToString();
            }
        }
        #endregion

        internal BitmexDataReader()
        {
            db = new DataContext();
            lastReadDateTime = DateTime.Now;
        }

        //for debugging...
        public int __iterationCount;
        public int __numItemsSaved;
        public int __numItems;
        public int __i;

        //update the currentItem field whenever new data is available, and write data out in  compleded 1-minute chunks
        internal BitmexDataItem ProcessData(dynamic dyn)
        {
            __iterationCount = 0;
            __numItems = 0;

            BitmexDataItem ret = new BitmexDataItem();
            string log = string.Empty;
            try
            {
                var symbol = dyn.symbol;
                var table = dyn.table;

                int i = dyn.data.Count;
                __numItems = i;
                if (i == 0 || dyn.data[0] == null || dyn.data[i-1] == null)
                {
                    LogConnection.WriteLine("Data is null or count is zero at " + DateTime.Now.ToString());
                    return null;
                }

                lastReadDateTime = DateTime.Now;

                DateTime ts = dyn.data[i - 1].timestamp;

                //if just launched, initialise with the very first data item, as if this was the previous item saved to the DB, so it will be in the previous minute
                if (first)
                {
                    DateTime ds = dyn.data[0].timestamp;
                    lastReadItem = new FeedDataItem
                    {
                        timestamp = ds,
                        price = dyn.data[0].bidPrice
                    };
                    lastSavedItem = new FeedDataItem
                    {
                        timestamp = ds,
                        price = dyn.data[0].bidPrice
                    };
                    first = false;
                    currentItem = new BitmexDataItem(lastReadItem);
                }

                App.CurrentSeconds(60 - ts.Second);
                itemsToAdd.Clear();

                //find the next entry in the data which is later than the last saved entry in the FeedDataItems list
                //testing shows that it reads around 400 elements from the end of the data list each time, so stick with 1000 as the number downloaded
                if (FindNextItem(ref lastReadItem, dyn.data, ref i))
                {
                    __iterationCount++;
                    __i = i;
                    DateTime nextMinute = GetAsMinute(lastSavedItem.timestamp).AddMinutes(1);

                    bool hasUpdated = false;
                    //read forward from this position until rolling over to next minute, then save out
                    for (; i < dyn.data.Count; i++)
                    {
                        dynamic obj = dyn.data[i];
                        DateTime timestamp = GetAsMinute(obj.timestamp);

                        lastReadItem.price = obj.bidPrice;
                        lastReadItem.timestamp = obj.timestamp;

                        if (DateTime.Compare(nextMinute, timestamp) > 0)
                        {
                            /*
                             * edge case: runs to end of data, last element goes to next minute so item is saved.
                             * next data set adds just one element, or no elements (i.e. same data set sent):
                             *  - FindNextItem returns the last element, so there is just one iteration, currentItem is set to new BitmexDataItem so timestamp is 
                             *    never changed. This could happen repeatedly so it will result in a long period of 'dropout' of captured data, apparently
                             *    So must always set timestamp for returned object (which is done in currentItem.Update(...))
                             * */

                            hasUpdated = true;
                            currentItem.CopyTo(ret); //just assignment and timestamp will be reset in ret when currentItem loses its seconds in SetTimestamp()
                            currentItem.Update(lastReadItem);
                        }
                        else
                        {
                            /*
                            * edge case: runs to end of data, last element goes to next minute so item is saved.
                            * next data set adds just one element, or no elements (i.e. same data set sent):
                            *  - currentItem is null and ret is returned as a new BitmexDataItem with the timestamp of 0001,01,01
                            *    so need to set the timestamp to the current item timestamp at this point
                            *    */

                            hasUpdated = false;
                            currentItem.CopyTo(ret);
                            currentItem.SetTimestamp(); //will remove seconds to correctly timestamp as start of minute

                            //must clone currentItem as it will be changed next loop so that could change data somewhere else with unexpected results
                            BitmexDataItem item = currentItem.Clone();
                            App.ItemAdded(item);
                            itemsToAdd.Add(item);
                            lastSavedItem.price = lastReadItem.price;
                            lastSavedItem.timestamp = lastReadItem.timestamp;
                            nextMinute = timestamp.AddMinutes(1);
                            currentItem = new BitmexDataItem(lastReadItem);
                        }
                    }

                    if (hasUpdated)
                    {
                        //must clone currentItem as it will be changed next loop so that could change data somewhere else with unexpected results
                        App.ItemUpdated(currentItem.Clone()); //don't want to do this dozens of times each update, just for the final aggregated value
                    }

                    __numItemsSaved = itemsToAdd.Count;

                    #region Write to DB
                    if (App.WriteToDB == true && itemsToAdd.Count() > 0)
                    {
                        db.BitmexDataItems.AddRange(itemsToAdd);
                        db.SaveChanges();
                    }
                    #endregion
                }
                else
                {
                    log += "Could not find next item at " + DateTime.Now.ToString();
                }
            }
            catch (Exception ex)
            {
                log += ex.ToString();
            }
  
            //it is returning every one or 2 seconds, the frequency of the data feed

            if (log != string.Empty) LogConnection.WriteLine(log);
            return ret;

            //????? is the actual price the bid or ask price here
        }
       
        //find the first item which is imediately after the FeedDataItem param, by doing a binary partition search (or whatever it's called, quicksort type algorithm?)
        private bool FindNextItem(ref FeedDataItem lastReadItem, dynamic data, ref int index)
        {
            DateTime timestamp = lastReadItem.timestamp;
            int end = data.Count-1;
            int start = 0;

            while (end - start > 0)
            {
                if (data[start] == null)
                {
                    return false;
                }

                if (end - start < 2)
                {
                    //can only move one step foward now anyway, so this is the termination point
                    //but if start was set before end in the back step, start and end will be zero on the next back step
                    //this would result in an endless loop otherwise
                    start = end; //start was before timestamp, so end is the return value, or both are the same anyway (zero index)
                    break;
                }

                DateTime dt = data[start].timestamp;
                if (DateTime.Compare(dt,timestamp) <= 0)
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

            if (start < data.Count)
            {
                lastReadItem.timestamp = data[start].timestamp;
                lastReadItem.price = data[start].bidPrice;
                index = start;
                return true;
            }
            return false;
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

        #region Unused
        private bool FindNextItemSlow(ref FeedDataItem lastReadItem, dynamic data, ref int index)
        {
            int i = data.Count - 1;
            for (; i > -1; i--)
            {
                if (data[i] == null) continue;

                DateTime dt = data[i].timestamp;
                if (DateTime.Compare(dt, lastReadItem.timestamp) <= 0)
                {
                    //this is first element that is before lastReadItem
                    if (i < data.Count - 1)
                    {
                        i++;
                    }
                    break;
                }
            }
            if (i < 0)
            {
                i = 0;
            }
            if (data[i] == null)
            {
                LogConnection.WriteLine("Returning false from FindNextItemSlow()");
                return false;
            }
            lastReadItem.timestamp = data[i].timestamp;
            lastReadItem.price = data[i].bidPrice;
            index = i;
            return true;
        }
        #endregion
    }
}
