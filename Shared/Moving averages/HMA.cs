using AlgoTrader.Shared.Enums;
using System.Linq;

namespace AlgoTrader.Shared.MovingAverages
{
    //Hull moving average
    public class HMA : MovingAverage
    {
        WMA fast_ma;
        WMA slow_ma;
        WMA final_ma;

        public HMA(CandlePosition candlePosition, Chart chart, int length) : base(candlePosition, chart, length) 
        { 
            fast_ma = new WMA(candlePosition, chart, length / 2);
            slow_ma = new WMA(candlePosition, chart, length);
            final_ma = new WMA(candlePosition, chart, (int) System.Math.Sqrt(length));
        }

        public override void Init(int timeInterval)
        {
            base.Init(timeInterval);
            fast_ma.Init(timeInterval);
            slow_ma.Init(timeInterval);
            final_ma.Init(timeInterval);
        }


        public override double CalculateMovingAverage(_SymbolDataItem item, int timeFrame, bool isAggregating)
        {
            var fastResult = fast_ma.ItemAdded(item, timeFrame);
            var slowResult = slow_ma.ItemAdded(item, timeFrame);

            var combinedResult = 2 * fastResult - slowResult;

            //not entirely efficient, since the symbol data item is not needed - just the value
            //this is the same for all the moving averages
            //TODO - SORT THIS
            var v = new _SymbolDataItem
            {
                Open = combinedResult, Close = combinedResult, High = combinedResult, Low = combinedResult
            };
            return final_ma.ItemAdded(v, timeFrame);
        }
    }
}
