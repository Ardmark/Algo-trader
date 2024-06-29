using AlgoTrader.Shared.Enums;
using System.Linq;

namespace AlgoTrader.Shared.MovingAverages
{
    public class EMATimeFrame : MovingAverageTimeframe
    {
        public double ema, alpha;
        
        public EMATimeFrame(int timeFrame) : base (timeFrame)
        {
            ema = alpha = 0;
        }
    }
    public class EMA : MovingAverage
    {

        public EMA(CandlePosition candlePosition, Chart chart, int length) : base(candlePosition, chart, length) { }

        (double ema, double alpha) GetEmaAlpha(int timeFrame)
        {
            var EMAtimeFrame = (EMATimeFrame)timeFrameDictionary[timeFrame];
            return (EMAtimeFrame.ema, EMAtimeFrame.alpha);
        }
        void SetEmaAlpha(double ema, double alpha, int timeFrame)
        {
            var EMAtimeFrame = (EMATimeFrame)timeFrameDictionary[timeFrame];
            EMAtimeFrame.ema = ema;
            EMAtimeFrame.alpha = alpha;
        }
        public override void Init(int timeFrame)
        {
            if (!timeFrameDictionary.ContainsKey(timeFrame))
            {
                timeFrameDictionary.Add(timeFrame, new EMATimeFrame(timeFrame));
            }
        }
        public override void Reset(int timeFrame)
        {
            var values = GetValues(timeFrame);
            (var ema, var alpha) = GetEmaAlpha(timeFrame);
            //can just get the SMA to start with, but better to correctly calculate the ema for the initial series, although it seems to make no perceptible difference
            alpha = 2.0 / (values.Count + 1);

            //ema = values.Select(x => x.Close).Reverse().Aggregate((ema, nextQuote) => (float)alpha * nextQuote + (1 - (float)alpha) * ema);
            switch (candlePosition)
            {
                case CandlePosition.OPEN:
                    ema = values.Select(x => x.Open).Aggregate((_ema, _nextQuote) => (float)alpha * _nextQuote + (1 - (float)alpha) * ema);
                    break;
                case CandlePosition.CLOSE:
                    ema = values.Select(x => x.Close).Aggregate((_ema, nextQuote) => (float)alpha * nextQuote + (1 - (float)alpha) * ema);
                    break;
                case CandlePosition.HIGH:
                    ema = values.Select(x => x.High).Aggregate((_ema, nextQuote) => (float)alpha * nextQuote + (1 - (float)alpha) * ema);
                    break;
                case CandlePosition.LOW:
                    ema = values.Select(x => x.Low).Aggregate((_ema, nextQuote) => (float)alpha * nextQuote + (1 - (float)alpha) * ema);
                    break;
            }

            SetEmaAlpha(ema, alpha, timeFrame);

            #region Unused
            //this is the logic in non-linq syntax
            //it is probably faster, but would need profiling to be sure
            //if using full calculation per-frame, the order must be reversed from this - HOWEVER full re-calc per frame does not produce quite the desired result,
            // this is not how it is usually done and so it differes from trading view, etc.

            /*
            if (values.Count > 0)
            {
                ema = values[values.Count() - 1].Close;
                for (int i = values.Count - 2; i > -1; i--)
                {
                    ema = alpha * values[i].Close + (1 - alpha) * ema;
                }
            }
            */
            #endregion
        }

        public override double CalculateMovingAverage(_SymbolDataItem item, int timeFrame, bool isAggregating)
        {
            //very simple formula, does not need the calculation to be made over the entire series each time
            //but this won't work with interpolation
            //solution is to treat each aggregated frame as if it is the same as the final aggregated frame, i.e. for a 5-interval, it will only do a new calculation every 5 frames
            double price = 0;
            switch (candlePosition)
            {
                case CandlePosition.OPEN:
                    price = item.Open;
                    break;
                case CandlePosition.CLOSE:
                    price = item.Close;
                    break;
                case CandlePosition.HIGH:
                    price = item.High;
                    break;
                case CandlePosition.LOW:
                    price = item.Low;
                    break;
            }
            (var ema, var alpha) = GetEmaAlpha(timeFrame);
            var _ema = (price - ema) * alpha + ema;
            if (!isAggregating)
            {
                ema = _ema;
            }

            SetEmaAlpha(ema, alpha, timeFrame);

            return _ema;
        }

        //override for AddElement, just adds to values array prior to initialisation
        //after that, there is no use for the array so no need to add and remove values
        internal override double AddElement(_SymbolDataItem item, int timeFrame, TimeFrameAggregator timeFrameAggregator)
        {
            var values = GetValues(timeFrame);
            //(var values, var timeFrameAggregator) = GetValuesAndAggregator(timeFrame);

            if (!timeFrameAggregator.hasInitialisedValues)
            {
                if (values.Count == 0)
                {
                    SetEmaAlpha(item.Close, 0, timeFrame); //initial seed value, or it starts from zero and takes longer to reach a valid state
                }
                if (timeFrameAggregator.AggregationCount == 0)
                {
                    if (values.Count == movingAverageLength) //don't remove until all values in place
                    {
                        Reset(timeFrame);
                        timeFrameAggregator.hasInitialisedValues = true;
                    }
                }
                if (values.Count < movingAverageLength) //do not remove this!
                {
                    values.Add(item);
                }
            }
        
            return AddInterpolatedResults(CalculateMovingAverage(item, timeFrame, timeFrameAggregator.AggregationCount > 0), timeFrame);
        }

        //no aggregation version for efficiency
        public override double AddElementNoAggregation(double value, int timeFrame)
        {
            //only remove when interpolationCount count is zero, otherwise update last value
            var aggregator = GetAggregator(timeFrame);
            aggregator.AggregateEveryFrameCountOnly(); //for aggregation and interpolation count, do not remove!!
            return AddElement(new _SymbolDataItem { Close = value }, timeFrame, aggregator);
        }


    }
}
