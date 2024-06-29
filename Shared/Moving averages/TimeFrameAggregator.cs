using System.Collections.Generic;
using System.Linq;

namespace AlgoTrader.Shared
{
    public class TimeFrameAggregator //This is a finite state machine.
    {
        int timeFrame;
        public bool hasInitialisedValues = false;
        protected List<_SymbolDataItem> timeframeAggregatedItems;

        public int InterpolationCount { get; set; }
        public int AggregationCount { get; set; }

        public TimeFrameAggregator() : this(1) { }

        public TimeFrameAggregator(int timeFrame)
        {
            this.timeFrame = timeFrame;
            timeframeAggregatedItems = new List<_SymbolDataItem>();
        }

        public void Init(int timeFrame)
        {
            this.timeFrame = timeFrame;
            timeframeAggregatedItems.Clear();
        }

        //aggregate data and return result only when aggregation is complete
        //this is for use in initialising the values array
        public _SymbolDataItem AggregateSingleFrame(_SymbolDataItem item)
        {
            InterpolationCount = timeframeAggregatedItems.Count; //since aggregation is called before interpolation, need the previous Count value for interpolation
            timeframeAggregatedItems.Add(item);

            if (timeframeAggregatedItems.Count > 0)
            {
                //if (timeframeAggregatedItems.Last().Timestamp.Subtract(timeframeAggregatedItems.First().Timestamp).TotalMinutes >= timeFrame - 1)
                if (timeframeAggregatedItems.Count == timeFrame)
                {
                    _SymbolDataItem ret = Combine(timeframeAggregatedItems);
                    timeframeAggregatedItems.Clear();
                    return ret;
                }
            }
            return null;
        }

        //aggregate data and return the result every frame
        //this is for use with interpolation, especially with live data, so every 1-min frame updates the results list
        public _SymbolDataItem AggregateEveryFrame(_SymbolDataItem item)
        {

            InterpolationCount = AggregationCount; //since aggregation is called before interpolation, need the previous Count value for interpolation
            timeframeAggregatedItems.Add(item);
            _SymbolDataItem ret = Combine(timeframeAggregatedItems);
            if (timeframeAggregatedItems.Count == timeFrame)
            {
                timeframeAggregatedItems.Clear();
            }
            AggregationCount = timeframeAggregatedItems.Count;
            return ret;
        }
        public _SymbolDataItem AggregateEveryFrame(List<_SymbolDataItem> items)
        {
            foreach (var item in items)
            {
                InterpolationCount = AggregationCount; //since aggregation is called before interpolation, need the previous Count value for interpolation
                timeframeAggregatedItems.Add(item);
                Combine(timeframeAggregatedItems);
                if (timeframeAggregatedItems.Count == timeFrame)
                {
                    timeframeAggregatedItems.Clear();
                }
                AggregationCount = timeframeAggregatedItems.Count;
            }
            return null;
        }

        //as above, but without the actual aggregation
        internal void AggregateEveryFrameCountOnly()
        {
            InterpolationCount = AggregationCount;
            AggregationCount++;
            if (AggregationCount == timeFrame)
            {
                AggregationCount = 0;
            }
        }
        internal _SymbolDataItem Combine(List<_SymbolDataItem> items)
        {
            var firstItem = items.First();
            var lastItem = items.Last();
            if (firstItem == null || lastItem == null)
            {
                return null;
            }
            var open = firstItem.Open;
            var close = lastItem.Close;
            var high = double.MinValue;
            var low = double.MaxValue;
            //var symbolName = firstItem.SymbolName;
            //var exchangeId = firstItem.ExchangeId;
            var timeStamp = firstItem.Timestamp; //the timestamp needs to be the open time
            var interrupted = firstItem.Interrupted;

            foreach (_SymbolDataItem item in items)
            {
                if (high < item.High)
                {
                    high = item.High;
                }
                if (low > item.Low)
                {
                    low = item.Low;
                }
            }

            return new _SymbolDataItem
            {
                Open = open,
                Close = close,
                High = high,
                Low = low,
                Timestamp = timeStamp,
                //SymbolName = symbolName,
                //ExchangeId = exchangeId,
                Interrupted = interrupted
            };
        }

    }

}
