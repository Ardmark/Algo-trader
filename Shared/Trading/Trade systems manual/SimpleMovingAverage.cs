using Bitmex_trader.DataObjects;
using Bitmex_trader.Indicators;
using Bitmex_trader.UI;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Bitmex_trader.Panels
{
    //this is the display class for the basic SMA
    public class SMA_Panel : PanelBase
    {
        SMA sma;

        public SMA_Panel(MainViewModel mainViewModel, Canvas canvas) : base(mainViewModel, canvas)
        {
            sma = new SMA(mainViewModel);
        }

        public override void ItemAdded(BitmexDataItem item)
        {
            sma.ItemAdded(item);
        }

        /// <summary>
        /// Initialise this MovingIndicator
        /// </summary>
        /// <param name="startFrame">The first frame (counted back from the last one added to the database), this would be the first frame of the Chart, for example.</param>
        /// <param name="numFrames">The number of 1-minute frames this MovingIndicator will cover. This would be the Chart length, for example. Use -1 for all the frames.</param>
        /// <param name="length">The length of the moving average (time interval agnostic).</param>
        /// <param name="timeInterval">The time interval (1, 5, 15 etc).</param>
        /// <param name="syncToChart">This will set the time boundary to match the current time interval, to sync with the Chart which always starts on a boundary</param>
        public virtual void Init(int startFrame, int numFrames, int length, int timeInterval, bool syncToChart = false)
        {
            sma.Init(startFrame, numFrames, length, timeInterval, syncToChart);
        }

        public override void Draw()
        {

            List<double> items = new List<double>();
            foreach (var item in sma.GetValue())
            {
                items.Add(item);
            }

            double rectWidth = mainViewModel.Chart.GetCandleWidth();

            bool first = true;
            double prevX = 0;
            double prevY = 0;
            for (int i = items.Count -1; i > -1;  i--)
            {
                var item = items[i];
                double lineX = mainViewModel.Chart.GetX(i) - rectWidth / 2;
                double lineY = mainViewModel.Chart.GetY(item);
                if (first)
                {
                    first = false;
                }
                else
                {
                    Line line = new Line
                    {
                        X1 = prevX,
                        Y1 = prevY,
                        X2 = lineX,
                        Y2 = lineY,
                        StrokeThickness = 1,
                        Stroke = Brushes.Gray
                    };
                    canvas.Children.Add(line);
                }
                prevX = lineX;
                prevY = lineY;
            }
        }

        

    }
}
