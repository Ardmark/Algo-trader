using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.MovingAverages;
using System.Collections.Generic;
using System.Windows.Media;

namespace AlgoTrader.Shared.TradeIndicators
{
    public class Stochastic_Indicator : DisplayPanel
    {
        #region Fields
        const int smoothK_length = 3;
        const int smoothD_length = 3;
        readonly int lineThicknessK;
        readonly int lineThicknessD;
        readonly SolidColorBrush colorK;
        readonly SolidColorBrush colorD;
        readonly Stochastic stochastic;
        private readonly SMA smaK;
        private readonly SMA smaD;


        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mainViewModel">Main view model    </param>
        /// <param name="canvas">Canvas - either Chart (to overlay main display) or separate canvas    </param>
        /// <param name="length">Moving average length          </param>
        /// <param name="candlePosition">Open, Close, High or Low   </param>
        /// <param name="lineThicknessK">Line thickness for stochastic   </param>
        /// <param name="colorK">Line color for stochastic   </param>
        /// <param name="lineThicknessD">Line thickness for SMA   </param>
        /// <param name="colorD">color thickness for SMA   </param>

        public Stochastic_Indicator(int timeInterval, TradeEngine tradeEngine, Chart chart, int length, CandlePosition candlePosition, int lineThicknessK, SolidColorBrush colorK, int lineThicknessD, SolidColorBrush colorD) : base(null, timeInterval, tradeEngine, chart)
        {
            this.lineThicknessK = lineThicknessK;
            this.colorK = colorK;

            this.lineThicknessD = lineThicknessD;
            this.colorD = colorD;

            movingAverages.Add(stochastic = new Stochastic(candlePosition, chart, length));
            movingAverages.Add(smaK = new SMA(candlePosition, chart, 3));
            movingAverages.Add(smaD = new SMA(candlePosition, chart, 3));
        }

        public override void ItemAdded(_SymbolDataItem item, bool canComputeTradeEntry, bool liveItem)//, bool redrawOnly)
        {
            base.ItemAdded(item, canComputeTradeEntry, liveItem);

            foreach (var timeFrame in MainObj.timeFrames)
            {
                smaD.AddElementNoAggregation(smaK.AddElementNoAggregation(stochastic.ItemAdded(item, timeFrame), timeFrame), timeFrame);
            }
        }

        public override void Draw()
        {
            int startFrame = Chart.ScrollBars;
            int numFrames = Chart.ScaleBars;

            if (MainObj.IsBacktesting)
            {
                return;
            }
            InitDraw();
            canvas.Children.Clear();
            List<double> items_smaK = stochastic.GetResults(startFrame, numFrames, Chart.TimeFrame);
            List<double> items_smaD = smaD.GetResults(startFrame, numFrames, Chart.TimeFrame);

            #region set the scaling to exactly fill the panel
            Low = double.MaxValue;
            High = double.MinValue;
            SetScaling(new[] { items_smaK, items_smaD });
            #endregion

            #region set scaling to 0-100 range
       //     Low = 0;
       //     High = 100;
       //     SetScaling();
            #endregion

            DrawLineSegments(items_smaK, lineThicknessK, colorK); //blue
            DrawLineSegments(items_smaD, lineThicknessD, colorD);  //red
            DrawText(High.ToString("0"), 0, canvas.ActualHeight - 20);
            DrawText(Low.ToString("0"), 0, 0);

     /*       for (int i=0; i < items_smaK.Count; i++)
            {
                if (MovingIndicatorBase.Crossover(smaK, smaD, startFrame + i)) //the stored results are latest first, but the iterator returns earliest first so must reverse frame order
                {
                    DrawMarker(MarkerStyle.TRIANGLEDOWN, new System.Windows.Point(i, 0), colorD, 10);
                }
                if (MovingIndicatorBase.Crossover(smaD, smaK, startFrame + i))
                {
                   DrawMarker(MarkerStyle.TRIANGLEUP, new System.Windows.Point(i, canvas.ActualHeight - 10), colorK, 10);
                }
            }
            */
        }
    }
}
