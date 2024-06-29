using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.MovingAverages;
using System.Windows.Media;

namespace AlgoTrader.Shared.TradeIndicators
{
    //this is the display class for the basic SMA
    public class SMA_Indicator : DisplayPanel
    {
        readonly int lineThickness;
        readonly SolidColorBrush color;
        readonly MovingAverage indicator;
        readonly double space;

        public SMA_Indicator(int timeInterval, TradeEngine tradeEngine, Chart chart, int length, CandlePosition candlePosition, int lineThickness, SolidColorBrush color, double space = 0) : base(null, timeInterval, tradeEngine, chart)
        {
            this.lineThickness = lineThickness;
            this.color = color;
            this.space = space;
            movingAverages.Add(indicator = new SMA(candlePosition, chart, length));
        }

        //ItemAdded - called when an item is added to the DB, this can be used for example with a moving average
        //public override void ItemAdded(_SymbolDataItem item, bool canComputeTradeEntry, bool liveItem)//, bool redrawOnly)
        //{
        //    base.ItemAdded(item, canComputeTradeEntry, liveItem);

        //    //foreach (var timeFrame in MainObj.timeFrames)
        //    //{
        //    //    indicator?.ItemAdded(item, timeFrame);
        //    //}
        //}

        public override void Draw() => DrawLineOverlayChart(indicator, lineThickness, color, space);

        /*     public override void Draw(int startFrame, int numFrames, DataContext db)
             {
                 canvas.Children.Clear();

                 List<double> items_smaD = indicator.GetResults(startFrame, numFrames);

                 Low = double.MaxValue;
                 High = double.MinValue;
                 SetScaling(new[] { items_smaD });

                 DrawLineSegments(items_smaD, 1, Brushes.Blue); //blue

             }
     */
    }
}
