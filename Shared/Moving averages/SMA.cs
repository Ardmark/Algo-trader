using AlgoTrader.Shared.Enums;

namespace AlgoTrader.Shared.MovingAverages
{
    //Simple moving average calculator

    public class SMATimeFrame : MovingAverageTimeframe
    {
        public double sum; //this is unused as rolling calculation cannot work with interpolated values

        public SMATimeFrame(int timeFrame) : base(timeFrame)
        {
            sum = 0;
        }
    }

    public class SMA : MovingAverage
    {
        //double lastItem;

        public SMA(CandlePosition candlePosition, Chart chart, int length) : base(candlePosition, chart, length) { }
        //public SMA(MovingIndicator ma, int length) : base(ma, length) { }

        //double GetSum(int timeFrame)
        //{
        //    var SMAtimeFrame = (SMATimeFrame)timeFrameDictionary[timeFrame];
        //    return SMAtimeFrame.sum;
        //}
        //void SetSum(double sum, int timeFrame)
        //{
        //    var SMAtimeFrame = (SMATimeFrame)timeFrameDictionary[timeFrame];
        //    SMAtimeFrame.sum = sum;
        //}

        public override void Reset(int timeFrame)
        {
            
            //double sum = 0;
            //var values = GetValues(timeFrame);
            //if (values.Count == 0)
            //{
            //    return;
            //}

            //foreach (var v in values)
            //{
            //    sum += v.Close;
            //}
            //sum /= values.Count;
            
            //SetSum(sum, timeFrame);
            //lastItem = values[0].Close;
            
        }

        public override double CalculateMovingAverage(_SymbolDataItem item, int timeFrame, bool isAggregating)
        {
            /////////////////////////////////////////////////////////////////////////////////
            double sum = 0;
            var values = GetValues(timeFrame);
            const int sampleSize = 1000; //100 seems OK also
            var startpos = values.Count > sampleSize ? values.Count - sampleSize : 0;
            for (int i = startpos; i < values.Count; i++)
            {
                sum += values[i].Close;
            }
            sum /= values.Count;
            return sum;
            /*
            //return values.Count != 0 ? values.Select(x => x.Close).Sum() / values.Count() : 0;

            //rolling calculation cannot work with interpolated values
            ////////////////////////////////////////////////////////////////////////////////////////

            sum = sum - (lastItem - item.Close) / MA_Length;
            lastItem = values[0].Close; //save for next time
            return sum;
            */
        }
    }
}
