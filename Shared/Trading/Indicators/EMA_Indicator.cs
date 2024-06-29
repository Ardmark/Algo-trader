using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.MovingAverages;
using System.Windows.Media;


namespace AlgoTrader.Shared.TradeIndicators
{
    public class EMA_Indicator : DisplayPanel
    {
        readonly int lineThickness;
        readonly SolidColorBrush color;
        readonly MovingAverage indicator;

        public EMA_Indicator(int timeInterval, TradeEngine tradeEngine, Chart chart, int length, CandlePosition candlePosition, int lineThickness, SolidColorBrush color) : base(null, timeInterval, tradeEngine, chart)
        {
            this.lineThickness = lineThickness;
            this.color = color;
            movingAverages.Add(indicator = new EMA(candlePosition, chart, length));
        }

        //ItemAdded - called when an item is added to the DB, this can be used for example with a moving average
        //public override void ItemAdded(_SymbolDataItem item, bool canComputeTradeEntry, bool liveItem)//, bool redrawOnly)
        //{
        //    base.ItemAdded(item, canComputeTradeEntry, liveItem);

        //    ////if (redrawOnly) return;
        //    //foreach (var timeFrame in MainObj.timeFrames)
        //    //{
        //    //    indicator?.ItemAdded(item, timeFrame);
        //    //}
        //}

        public override void Draw() => DrawLineOverlayChart(indicator, lineThickness, color);

    }
}
