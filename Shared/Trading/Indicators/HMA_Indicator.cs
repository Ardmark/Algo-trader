using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.MovingAverages;
using System;
using System.Linq;
using System.Windows.Media;


namespace AlgoTrader.Shared.TradeIndicators
{
    public class HMA_Indicator : DisplayPanel
    {
        readonly int lineThickness;
        readonly SolidColorBrush color;
        internal readonly MovingAverage indicator;

        public HMA_Indicator(int timeInterval, TradeEngine tradeEngine, Chart chart, int length, CandlePosition candlePosition, int lineThickness, SolidColorBrush color) : base(null, timeInterval, tradeEngine, chart)
        {
            this.lineThickness = lineThickness;
            this.color = color;
            movingAverages.Add(indicator = new HMA(candlePosition, chart, length));
        }

        public override void Draw() => DrawLineOverlayChart(indicator, lineThickness, color);

        public override bool CanEnterTrade(int timeFrame, double currPrice, double prevPrice, int currentFrame)
        {
            var results = indicator.GetResultsFromDictionary(timeFrame);
            if (currentFrame >= results.Count)
            {
                currentFrame = results.Count-1;
            }
            if (results == null) return false;
            var price = results[currentFrame];

            return currPrice > price && prevPrice <= price;
        }

        public override double GetLastPrice(int timeFrame, int frameNum = -1)
        {
            var results = indicator.GetResultsFromDictionary(timeFrame);
            if (results == null)
            {
                return -1;
            }
            return frameNum == -1 ? results[results.Count - 1] : results[frameNum];
        }
    }
}
