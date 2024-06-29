using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Interfaces;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AlgoTrader.Shared.MovingAverages
{
    /*
     * THIS IS NOT GOOD DESIGN
     * A better approach would be for each indicator to return a series (class), which would be used as an input to another indicator if needed. The db would also be a series.
     * This could be done as a heirarchy where the root object owns the db series, and that would inform all immediate child (moving indicator) objects when an update was available.
     * Each series would then take the input from its parent output, update its own series, and inform its children that an update is available.
     * Drawing would be done from the results, not in the iteration process.
     * */

    public class MovingAverageTimeframe
    {
        //the values for all the 1-minute frames within this moving average
        //this is used as EMA requires re-calculating each time from the data, so for consistency all indicators are done like this
        //[0] == later, [n] == earlier, so time moves FORWARD from [n] to [0]
        public List<_SymbolDataItem> symbolDataItems;
        //results for all the frames
        public TimeFrameAggregator timeFrameAggregator;
        public List<double> results;

        public MovingAverageTimeframe(int timeFrame)
        {
            symbolDataItems = new List<_SymbolDataItem>();
            results = new List<double>();
            timeFrameAggregator = new TimeFrameAggregator(timeFrame);
        }
    }
    public class MovingAverage
    {
        #region Fields
        public int movingAverageLength;
        //public int timeFrame;

        protected Chart chart;

        //protected int startFrame; //the first available frame of the set - this will be calculated based on the time interval
        protected CandlePosition candlePosition;
        //int initialStartFrame; //frame used until the initial values have been saved
        //protected bool hasInitialised = false;
        //public Dictionary<int, List<SymbolDataItem>> valuesDictionary = new Dictionary<int, List<SymbolDataItem>>(4);
        //public Dictionary<int, TimeFrameAggregator> timeFrameAggregatorsDictionary = new Dictionary<int, TimeFrameAggregator>(4);
        //public TimeFrameAggregator timeFrameAggregator;       
        //public List<double> results = new List<double>();
        //public Dictionary<int, List<double>> resultsDictionary = new Dictionary<int, List<double>>(4);

        public Dictionary<int, MovingAverageTimeframe> timeFrameDictionary = new Dictionary<int, MovingAverageTimeframe>();
        #endregion

        #region Delegates (unused)
        /*
        /// <summary>
        /// Callback to provide the moving average with data for each frame (Open, Close or some other data)
        /// </summary>
        /// <param name="dataItem">The data item for the current frame</param>
        /// <param name="frame">The number of the current frame.</param>
        public delegate object ValueDelegateMethod(DataItem dataItem, int frame);
        protected ValueDelegateMethod valueCallback; //returns the data item value for this moving average to process (Close, Open etc.) or the result of some other function

        /// <summary>
        /// Callback for the moving average to process the data set for each frame
        /// </summary>
        /// <param name="dataItem">The data item for the current frame</param>
        /// <param name="frame">The number of the current frame.</param>
        public delegate double ResultDelegateMethod(in List<DataItem> values);
        protected ResultDelegateMethod resultCallback;
        */
        #endregion

        #region virtual unimplemented
        public virtual double CalculateMovingAverage(_SymbolDataItem item, int timeFrame, bool isAggregating = false) { return 0;  }

        //virtual initialiser so object-specific code can be run during both init calls
        public virtual void Reset(int timeFrame)  {}

        public virtual void ItemUpdated(_SymbolDataItem item) { } //only using minute close to generate alerts, so live item updates not needed here
        #endregion

        #region Init
        public MovingAverage(CandlePosition candlePosition, Chart chart, int length)
        {
            //chart = this.chart;
            this.candlePosition = candlePosition;
            movingAverageLength = length;
        }

        public virtual void Init(int timeFrame)
        {
            if (!timeFrameDictionary.ContainsKey(timeFrame))
            {
                timeFrameDictionary.Add(timeFrame, new MovingAverageTimeframe(timeFrame));
            }
        }

        public TimeFrameAggregator GetAggregator(int timeFrame)
        {
            return timeFrameDictionary[timeFrame].timeFrameAggregator;
        }
        public List<_SymbolDataItem> GetValues(int timeFrame)
        {
            return timeFrameDictionary[timeFrame].symbolDataItems;
        }
        public (List<_SymbolDataItem> values, TimeFrameAggregator aggregator) GetValuesAndAggregator(int timeFrame)
        {
            return (GetValues(timeFrame), GetAggregator(timeFrame));
        }
        public List<double> GetResultsFromDictionary(int timeFrame)
        {
            //if (timeFrame == 0) return null;
            if (!timeFrameDictionary.ContainsKey(timeFrame))
            {
                return null;
            }
            return timeFrameDictionary[timeFrame].results;
        }
        public bool HasTimeFrame(int timeFrame)
        {
            return timeFrameDictionary.ContainsKey(timeFrame);
        }
        #endregion

        #region Add item
        public double ItemAdded(_SymbolDataItem item, int timeFrame)
        {
            var aggregator = GetAggregator(timeFrame);
            return AddElement(aggregator.AggregateEveryFrame(item), timeFrame, aggregator);
        }


        internal virtual double AddElement(_SymbolDataItem item, int timeFrame, TimeFrameAggregator timeFrameAggregator)
        {
            //only remove when interpolationCount count is zero, otherwise update last value
           // (var values, var timeFrameAggregator) = GetValuesAndAggregator(timeFrame);
           var values = GetValues(timeFrame);

            if (movingAverageLength > 0)
            {
                if (values.Count == movingAverageLength)
                {
                    if (timeFrameAggregator.AggregationCount == 0)
                    {
                        if (!timeFrameAggregator.hasInitialisedValues)
                        {
                            Reset(timeFrame);
                            timeFrameAggregator.hasInitialisedValues = true;
                        }
                        values.RemoveAt(0);
                    }
                    else
                    {
                        //need to replace the last value with the updated one
                        values.RemoveAt(values.Count - 1);
                    }
                }

                values.Add(item);
            }

            return AddInterpolatedResults(CalculateMovingAverage(item, timeFrame, timeFrameAggregator.AggregationCount > 0), timeFrame);

        }

        public virtual double AddElementNoAggregation(double value, int timeFrame)
        {
            (var values, var timeFrameAggregator) = GetValuesAndAggregator(timeFrame);

            //only remove when interpolationCount count is zero, otherwise update last value
            timeFrameAggregator.AggregateEveryFrameCountOnly(); //for aggregation and interpolation count, do not remove!!

            if (values.Count == movingAverageLength)
            {
                if (timeFrameAggregator.AggregationCount == 0)
                {
                    if (!timeFrameAggregator.hasInitialisedValues)
                    {
                        Reset(timeFrame);
                        timeFrameAggregator.hasInitialisedValues = true;
                    }
                    values.RemoveAt(0);
                }
                else
                {
                    //need to replace the last value with the updated one
                    values.RemoveAt(values.Count - 1);
                }
            }

            var item = new _SymbolDataItem { Close = value };
            values.Add(item);

            //still need the interpolated result, just no need to aggregate input
            return AddInterpolatedResults(CalculateMovingAverage(item, timeFrame, timeFrameAggregator.AggregationCount > 0), timeFrame);
        }


        public double AddInterpolatedResults(double value, int timeFrame)
        {
            //problem - this will set the correct set of interpolated values for the chosen timeframe, but those will not be the same as if they were created live,
            //as they are constantly overwritten as new data is presented.
            //So just add values as they come in.
            GetResultsFromDictionary(timeFrame).Add(value);
            return value;

/*
            results.Add(value);


            if (timeFrameAggregator.AggregationCount == 0)
            {
                //remove all results back to start of interpolationCount count
                if (results.Count > 0)
                {
                    results.RemoveRange(results.Count - timeFrameAggregator.InterpolationCount, timeFrameAggregator.InterpolationCount);
                }

                var previous = results.Count > 0 ? results[results.Count - 1] : 0;

                //add in interpolated results for however many frames there are, up to the timeFrame when it will reset and start again
                foreach (var res in Interpolate(previous, value))
                {
                    results.Add(res);
                }
            }

            return value;
            */
        }


        //simple linear interpolation
        //this will return all values up to and including the current param
        // so for a 5-interval, this will return 5 values, for a 1-interval it will just return one value which will be the same as the current param
    /*    protected IEnumerable<double> Interpolate(double previous, double current)
        {
            var timeFrameAggregator = GetAggregator(timeFrame);
            //use interpolationCount count instead of timeScele
            for (int i = 0; i < timeFrameAggregator.InterpolationCount+1; i++) 
            {
                yield return (current - previous) / timeFrame * (i + 1) + previous; //divide by timeframe, always the same regardless of how many interpolated frames
            }
        }
    */


        #endregion

        #region Return Value and Result

        //Get results methods only called for Draw, so can use current timeframe
        public IEnumerable<double> GetResult(int firstRequestedFrame, int numFrames, int timeFrame, int visibleTimeFrame = 0)
        {
            if (visibleTimeFrame == 0)
            {
                visibleTimeFrame = timeFrame;
            }
            int increment = timeFrame;
            //the first requested frame, say frame zero, is wrong for the first result if time interval is say, 5 min. It should be frame 4, which corresponds
            // to the 5th frame result, the last frame in the aggregation 
            GetFrameCount(ref firstRequestedFrame, ref numFrames, timeFrame, visibleTimeFrame);

            var results = GetResultsFromDictionary(timeFrame);
            if (results != null)
            {
                for (int i = firstRequestedFrame; i < numFrames && i < results.Count; i += increment)
                {
                    yield return results[i];
                }
            }
        }

        public List<double> GetResults(int firstRequestedFrame, int numFrames, int timeFrame, int visibleTimeFrame = 0)
        {
            if (visibleTimeFrame == 0)
            {
                visibleTimeFrame = timeFrame;
            }
            if (firstRequestedFrame < 0)
            {
                firstRequestedFrame = 0;
            }
            List<double> framesetResults = new List<double>();

            int increment =  visibleTimeFrame;
            GetFrameCount(ref firstRequestedFrame, ref numFrames, timeFrame, visibleTimeFrame);
            var results = GetResultsFromDictionary(timeFrame); //timeframe may be fixed, in which case will only have a single dictionary entry
            if (results == null)
            {
                return null;
            }
            for (int i = firstRequestedFrame; i < numFrames && i < results.Count; i += increment)
            {
                framesetResults.Add(results[i]);
            }
            return framesetResults;
        }

        public double GetResult(int requestedFrame, int timeFrame)
        {
            var results = GetResultsFromDictionary(timeFrame);
            if (results == null)
            {
                return 0;
            }
            return results.Count <= requestedFrame ? results.Last() : results[requestedFrame];
        }
        //return the actual frame count from a starting frame
        public void GetFrameCount(ref int firstRequestedFrame, ref int numFrames, int timeFrame, int visibleTimeFrame)
        {
            //the first requested frame, say frame zero, is wrong for the first result if time interval is say, 5 min. It should be frame 4, which corresponds
            // to the 5th frame result, the last frame in the aggregation 
            firstRequestedFrame++;
            firstRequestedFrame *= visibleTimeFrame;
            firstRequestedFrame--; //and this is returned as the reference param
            int ret = firstRequestedFrame + numFrames * visibleTimeFrame;
            var results = GetResultsFromDictionary(timeFrame);
             if (results == null)
            {
                return;
            }           
            
            if (ret > results.Count)
            {
                ret = results.Count;
            }
            numFrames = ret;
        }

        //return the visible frame count from a starting frame, this will allow for scrolling beyond the end of the visible frames (so can't just use numFrames)

        /*        public int GetVisibleFrameCount(int firstRequestedFrame, int numFrames)
                {
                    int totalFrames = GetFrameCount(firstRequestedFrame, numFrames);
                    if (totalFrames > results.Count)
                    {
                        totalFrames = results.Count;
                    }
                    return (totalFrames - firstRequestedFrame) / mainViewModel.TimeFrame;
                }
           */

        #endregion

        #region Utility methods
        //public static bool Crossover(MovingAverage m1, MovingAverage m2, int frame)
        //{
        //    return Crossover(m1.GetResultsFromDictionary(), m2.GetResultsFromDictionary(), frame);
        //}
        public static bool Crossover(List<double> m1, List<double> m2, int frame)
        {
            try
            {
                return frame > 0 && m1.Count > frame && m2.Count > frame
                    ? m1[frame - 1] < m2[frame - 1] && m1[frame] >= m2[frame]
                    : false;
            }
            catch
            {
                return false;
            }
        }

      //  const double CROSSOVER_PC = 0.5;
        //return true if one moving indicator is above another
        public static bool IsAbove(MovingAverage m1, MovingAverage m2, int frame, int timeFrame)
        {
            if (m1 == null || m2 == null)
            {
                return false;
            }
            try
            {
                var m1Results = m1.GetResultsFromDictionary(timeFrame);
                var m2Results = m2.GetResultsFromDictionary(timeFrame);
                if (frame > 0 && frame < m1Results.Count && frame < m2Results.Count && m1Results[frame] != 0 && m2Results[frame] != 0)
                {
                    return m1Results[frame] >= m2Results[frame];
                    //  var c_pc = CROSSOVER_PC / 100 + 1;
                    //   return m1.results[frame] / m2.results[frame] > c_pc;
                }
            }
            catch
            {

            }
            return false;
        }

        //return true if moving indicator is falling, based on 2 complete bars at this timescale
        public static bool IsFalling(MovingAverage m, int frame, int timeFrame)
        {
            var frame1 = frame - timeFrame * 2; //this is very sensitive to negative gradient, and will dip briefly in many places without some smoothing
            var frame2 = frame;
            var mResults = m.GetResultsFromDictionary(timeFrame);
            if (frame1 >=0 && frame2 >= 0 && frame1 < mResults.Count && frame2 < mResults.Count)
            {
                return mResults[frame1] != 0 && mResults[frame2] != 0 && mResults[frame1] >= mResults[frame2];
            }
            return false;
        }
        public static bool IsRising(MovingAverage m, int frame, int timeFrame)
        {
            return !IsFalling(m, frame, timeFrame);
        }

        public double GetCandlePositionForFrame(int frame, int timeFrame)
        {
            var values = GetValues(timeFrame);
            if (frame < values.Count)
            {
                switch (candlePosition)
                {
                    case CandlePosition.OPEN: return values[frame].Open;
                    case CandlePosition.CLOSE: return values[frame].Close;
                    case CandlePosition.HIGH: return values[frame].High;
                    case CandlePosition.LOW: return values[frame].Low;
                }
            }
            return 0;
        }

        public float Highest(CandlePosition candlePosition, List<_SymbolDataItem> items)
        {
            float ret = float.MinValue;
            foreach (var data in items)
            {
                switch (candlePosition)
                {
                    case CandlePosition.HIGH:
                        if (data.High > ret) ret = (float)data.High;
                        break;
                    case CandlePosition.LOW:
                        if (data.Low > ret) ret = (float)data.Low;
                        break;
                }
            }
            return ret;
        }
        public float Lowest(CandlePosition candlePosition, List<_SymbolDataItem> items)
        {
            float ret = float.MaxValue;
            foreach (var data in items)
            {
                switch (candlePosition)
                {
                    case CandlePosition.HIGH:
                        if (data.High < ret) ret = (float)data.High;
                        break;
                    case CandlePosition.LOW:
                        if (data.Low < ret) ret = (float)data.Low;
                        break;
                }
            }
            return ret;
        }
        #endregion

        #region Unused

        //Item added event (unused, keep for reference)

        /*
        public class ItemAddedEventArgs : EventArgs
        {
            public DataItem DataItem { get; set; }

        }

        //event handler (in the parent class)
        public event EventHandler<ItemAddedEventArgs> ItemAddedHandler;

        //method to be invoked by the event handler (in the parent class)
        void ItemAdded(object sender, ItemAddedEventArgs e)
        {
            AddElement(sender as BitmexDataItem);
        }

        //Method to be called by the child class whenever the parent event is to be raised
        protected virtual void OnItemAdded(ItemAddedEventArgs e)
        {
            ItemAddedHandler?.Invoke(this, e);
        }

        //raise the item added event with any child objects
           ItemAddedEventArgs args = new ItemAddedEventArgs
           {
               DataItem = item.GetData()
          };
          OnItemAdded(args);
        */
        #endregion

    }
}
