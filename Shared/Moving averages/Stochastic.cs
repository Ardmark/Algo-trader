using AlgoTrader.Shared.Enums;

namespace AlgoTrader.Shared.MovingAverages
{
    //Stochastic
    public class Stochastic : MovingAverage
    {
        public Stochastic(CandlePosition candlePosition, Chart chart, int length) : base(candlePosition, chart, length) { }

        public override double CalculateMovingAverage(_SymbolDataItem item, int timeFrame, bool isAggregating)
        {
            //this is the formula: sma(100 * (close - lowest(low, periodK)) / (highest(high, periodK) - lowest(low, periodK)), smoothK)
            var values = GetValues(timeFrame);
            var v1 = Highest(CandlePosition.HIGH, values) - Lowest(CandlePosition.LOW, values);

            if (v1==0) //this can happen if all input data are the same for a period, resulting in all values being the same
            {
                return 0; //because normally v1 would be > 0 so the return value would be zero if close == lowest
            }

            var v2 = values[values.Count - 1].Close - Lowest(CandlePosition.LOW, values);

            return 100.0 * v2 / v1;
        }

    }
}
