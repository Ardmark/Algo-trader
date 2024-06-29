using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.MovingAverages;
using System.Windows;
using System.Windows.Media;

namespace AlgoTrader.Shared.TradeIndicators
{
    public class Ribbon_Indicator : DisplayPanel
    {
        #region Fields
        readonly int lineThickness;
        readonly SolidColorBrush colorUp, colorDown;
        readonly int shortestLength = 15;
        readonly int longestLength = 50;
        readonly int indicatorIncrement = 5;
        #endregion

        #region Init
        public Ribbon_Indicator(int timeInterval, TradeEngine tradeEngine, Chart chart, CandlePosition candlePosition, int lineThickness, SolidColorBrush colorUp, SolidColorBrush colorDown) : base(null, timeInterval, tradeEngine, chart)
        {
            this.lineThickness = lineThickness;
            this.colorUp = colorUp;
            this.colorDown = colorDown;
            for (int i = shortestLength; i <= longestLength; i += indicatorIncrement)
            {
                movingAverages.Add(new EMA(candlePosition, chart, i));
            }
        }


        #endregion

        #region Item added
        //ItemAdded - called when an item is added to the DB, this can be used for example with a moving average
        //public override void ItemAdded(_SymbolDataItem item, bool canComputeTradeEntry, bool liveItem)//, bool redrawOnly)
        //{
        //    base.ItemAdded(item, canComputeTradeEntry, liveItem);
        //}

        #endregion

        #region Draw
        public override void Draw()
        {
            int startFrame = Chart.ScrollBars;
            int numFrames = Chart.ScaleBars;

            if (MainObj.IsBacktesting)
            {
                return;
            }
            //overlays chart, so must use the same scaling
            Low = Chart.Low;
            High = Chart.High;
            Scale = Chart.Scale;

            #region Draw indicators
            int __i = 0;
            InitDraw();

            foreach (MovingAverage indicator in movingAverages)
            {

               // if (!(indicator == indicators.First()) && !(indicator == indicators.Last())) continue;

                int count = 0;
                Point start = new Point();
                Point end = new Point();

                //need the actual start frame in the visible timeframe, which is available from GetFrameCount()
                //int frame = startFrame;
                //int _numFrames = numFrames;
                //indicator.GetFrameCount(ref frame, ref _numFrames, TimeFrame);

                foreach (double val in indicator.GetResult(startFrame, numFrames, Chart.TimeFrame))
                {
                    if (count == 0)
                    {
                        start.X = count;
                        start.Y = val;
                    }
                    else
                    {
                        start.X = end.X;
                        start.Y = end.Y;
                    }
                    end.X = count;
                    end.Y = val;

                    //get a suitable color
                    //SolidColorBrush color = MovingIndicatorBase.AboveOrBelow(indicator1, indicator2, frame) ? colorDown : colorUp;
                    SolidColorBrush color = __i==0 ? colorDown : colorUp;

                    //this method basically replicates PanelBase.DrawLineOverlayChart, which is in visible time scale.
                    //frame += MainObj.TimeFrame;

                    DrawLineSegment(start, end, lineThickness, color);

                    count++;
                }
                __i++;
            }
            #endregion



        }
        #endregion


    }
}
