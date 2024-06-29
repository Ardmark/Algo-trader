using AlgoTrader.Shared.Enums;
using System.Linq;

namespace AlgoTrader.Shared.MovingAverages
{
    //weighted moving average - used in Hull moving average
    public class WMA : MovingAverage
    {

        public WMA(CandlePosition candlePosition, Chart chart, int length) : base(candlePosition, chart, length) { }


        public override double CalculateMovingAverage(_SymbolDataItem item, int timeFrame, bool isAggregating)
        {
            var values = GetValues(timeFrame);
            if (values.Count < movingAverageLength)
            {
                return 0;
            }

            int startPos = values.Count - movingAverageLength;
            double sum = 0;
            for (int i = 0; i < movingAverageLength; i++)
            {
                sum += values[startPos + i].Close * (i + 1);
            }

            return sum / ((movingAverageLength * (movingAverageLength + 1)) / 2);
        }
    }
}
