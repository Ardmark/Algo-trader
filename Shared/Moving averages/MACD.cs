using AlgoTrader.Shared.Enums;
using System.Linq;

namespace AlgoTrader.Shared.MovingAverages
{
    public class MACD : MovingAverage
    {
        EMA fast_ma;
        EMA slow_ma;


        public MACD(Chart chart, int fastLength, int slowLength) : base(CandlePosition.CLOSE, chart, 0)
        {
            fast_ma = new EMA(CandlePosition.CLOSE, chart, fastLength);
            slow_ma = new EMA(CandlePosition.CLOSE, chart, slowLength);

        }

        public override void Init(int timeInterval)
        {
            base.Init(timeInterval);
            fast_ma.Init(timeInterval);
            slow_ma.Init(timeInterval);
        }


        public override double CalculateMovingAverage(_SymbolDataItem item, int timeFrame, bool isAggregating)
        {
            return fast_ma.ItemAdded(item, timeFrame) - slow_ma.ItemAdded(item.Clone(), timeFrame);
        }
    }
}
