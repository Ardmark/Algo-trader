using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.MovingAverages;
using Shared.Drawings;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using static System.Windows.Forms.AxHost;

namespace AlgoTrader.Shared
{

    /*
     * Base class for all panels
     * */
    public abstract class DisplayPanel 
    {

        const int MAX_DRAWING_OBJECTS = 4096;

        #region Fields

        //cannot mix different data contexts within the same database session, or one won't get immeditatly updated with the results of saving changes to the other
        public Canvas canvas;
        public double Scale, Low, High;
        protected bool timeIntervalFixed;
        bool hasInitialisedFixedTimeInterval;
        List<Line> lines = new List<Line>();
        protected ExchangeId exchangeId;
        protected TimeFrameAggregator timeFrameAggregator;
        public List<MovingAverage> movingAverages = new List<MovingAverage>();
        public string symbolName;

        #endregion

        #region Properties

        public int TimeFrame { get; set; }

        internal Chart chart;
        internal Chart Chart
        {
            get
            {
                //if no chart set, this is just a display-only object so use the current visible chart
                return chart ?? MainObj.visibleChart;
            }
            set
            {
                chart = value;
            }
        }

        public virtual double YOffset
        {
            get
            {
                return Chart.yOffset;
            }
        }

        internal TradeEngine tradeEngine;
        internal TradeEngine TradeEngine
        {
            get
            {
                //if no trade engine set, this is just a display-only object so use the current visible trade engine
                return tradeEngine ?? MainObj.visibleTradeEngine;
            }
            set
            {
                tradeEngine = value;
            }
        }

        public List<_SymbolDataItem> Results
        {
            get
            {
                return TradeEngine.results;
            }
        }

        
        int drawObjectListOffset;
        public int DrawObjectListOffset
        {
            get
            {
                return drawObjectListOffset;
            }
            set
            {
                drawObjectListOffset = value >= MAX_DRAWING_OBJECTS ? MAX_DRAWING_OBJECTS : value;
            }
        }
        

        public double CandleWidth
        {
            get
            {
                return (canvas.ActualWidth - canvas.Margin.Right) / Chart.ScaleBars;
            }
        }


        #endregion

        #region Init

        public DisplayPanel(string symbolName, int timeFrame, TradeEngine tradeEngine, Chart chart)
        {
            this.symbolName = symbolName;
            this.canvas = MainObj.chart_Canvas;
            this.chart = chart;
            this.tradeEngine = tradeEngine;
            TimeFrame = timeFrame;

            //fixed time interval is set by specifying timeInterval, if it is zero then it will take time interval from mainViewModel
            timeIntervalFixed = timeFrame > 0;
            exchangeId = TradeEngine.EXCHANGE;
        }

        //initialiser called during setup
        public virtual void Init(int timeFrame = 0)
        {
            //InitIndicators(timeFrame);        
        }

        //initialiser called when switching timeframe
        //this won't be called more than once per timeframe
        public virtual void InitTimeFrame(int timeFrame)
        {
            try
            {
                if (hasInitialisedFixedTimeInterval)
                {
                    return;
                }

                InitIndicators(timeFrame);

                if (tradeEngine != null)
                {
                    foreach (var indicator in movingAverages)
                    {
                        for (var i=0; i<tradeEngine.Results.Count; i++)
                        {
                            indicator.ItemAdded(tradeEngine.Results[i], timeFrame);
                        }
                    }
                }
            }
            catch { }
        }

        public virtual void InitIndicators(int timeFrame)
        {
            //if (!timeIntervalFixed)
            //{
            //    TimeFrame = timeFrame;
            //}
            //else
            //{
            //    //if (hasInitialisedFixedTimeInterval)
            //    //{
            //    //    return; //on setup, will initialise for all timeframes that symbols are trading on. Fixed time interval would attempt to add same key twice or more in the indicator init.
            //    //}
            //    //hasInitialisedFixedTimeInterval = true;
            //}

            TimeFrame = timeFrame;
            timeFrameAggregator = new TimeFrameAggregator(timeFrame);

            foreach (var indicator in movingAverages)
            {
                indicator.Init(timeFrame);
            }

        }

        public virtual void ReinitIndicators(int timeFrame)
        {
            if (!timeIntervalFixed)
            {
                TimeFrame = timeFrame;
            }
            timeFrameAggregator = new TimeFrameAggregator(timeFrame);


            //foreach (var indicator in movingAverages)
            //{
            //    indicator.ReInit(TimeFrame);
            //}
        }

        #endregion

        #region Virtual / abstract

        public virtual void Draw()
        {
            //derived class will then update all visible objects
        }

        public virtual bool CanEnterTrade(int timeFrame, double currPrice, double prevPrice, int currentFrame) { return false; }
        public virtual double GetLastPrice(int timeFrame, int frameNum = -1) { return -1; }

        public virtual void MouseMove(int mouseX, int mouseY) { }

        public virtual void ItemAdded(_SymbolDataItem item, bool canComputeTradeEntry, bool liveItem)       
        {
            if (movingAverages.Count == 0) return;

            foreach (MovingAverage indicator in movingAverages)
            {
                foreach (var timeFrame in MainObj.timeFrames)
                {
                    indicator.ItemAdded(item, timeFrame);
                }
            }
        }

        //ItemUpdated - must wait for ItemAdded before data is interpolated
        public virtual void ItemUpdated(_SymbolDataItem item, bool itemAdded = false)        {        }

        public void Draw(bool allowBacktestingDraw = false)
        {
            if (MainObj.IsBacktesting && !allowBacktestingDraw)
            {
                return;
            }
            Draw();
        }
        #endregion

        #region Scaling
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetX(double barNum)
        {
            return CandleWidth * barNum;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetY_ForPrice(double value) //absolute value from top of window
        {
            var v = High - value;
            return !MainObj.invertChart ? GetY_Relative(v) + YOffset : canvas.ActualHeight - GetY_Relative(v) - YOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetY_ForPriceNoOffset(double value) //as above, but no offset for vertical scroll, as that does not work with drawing context (must move entire image)
        {
            var v = High - value;
            return !MainObj.invertChart ? GetY_Relative(v) : canvas.ActualHeight - GetY_Relative(v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetY_ForPrice_dc(double value) //same, but no y offset for canvas margin or scroll, the drawing context will clip to top of screen regardless
        {
            var v = High - value;
            return !MainObj.invertChart ? v * Scale : canvas.ActualHeight - v * Scale;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetY_Relative(double value) //relative value, nothing to do with drawing orientation
        {
            //return (High - value) * Scale + canvas.Margin.Top;
            return value * Scale + canvas.Margin.Top - canvas.Margin.Bottom;
        }

        protected void SetScaling(params List<double>[] lists)
        {
            foreach (List<double> list in lists)
            {
                var min = list.Min();
                var max = list.Max();
                if (min < Low) Low = min;
                if (max > High) High = max;
            }
            double range = High - Low;

            //if only 'missing' elements on the whole page, range will be zero
            if (range == 0)
            {
                range = canvas.ActualHeight;
            }
            Scale = range != 0 ? canvas.ActualHeight / range : 0;
        }

        #endregion

        #region Draw line segments

        protected void DrawLineOverlayChart(MovingAverage indicator, int lineThickness, SolidColorBrush color, double space = 0, bool _continue = false)
        {
            if (!Chart.canDraw)
            {
                return;
            }
            int startFrame = Chart.ScrollBars;
            int numFrames = Chart.ScaleBars;

            //overlays chart, so must use the same scaling
            Low = Chart.Low;
            High = Chart.High;
            Scale = Chart.Scale;

            //returned values are latest at [0], earliest at end
            Point start = new Point();
            Point end = new Point();
            int count = 0;
            if (_continue == false)
            {
                InitDraw();
            }

            foreach (double val in indicator.GetResult(startFrame, numFrames, TimeFrame))
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

                DrawLineSegment(start, end, lineThickness, color, Chart.CandleWidth * space);

                count++;
            }
        }

        //must call this before drawing lines, as they are re-used to avoid garbage collection
        public virtual void InitDraw()
        {
            drawObjectListOffset = 0;
        }
        /// <summary>
        ///Draws a continuous line comprised of segments, each x-coordinate is the center of the corresponding candle
        ///points[0] is the earliest point in time
        /// </summary>
        /// <param name="yCoords">The list of y-coords, the point representing the earliest time is at index [0].</param>
        protected void DrawLineSegments(List<double> yCoords, int thickness, SolidColorBrush brushColor)
        {
            if (!Chart.canDraw)
            {
                return;
            }
            double rectWidth = CandleWidth;

            bool first = true;
            double prevX = 0;
            double prevY = 0;
            for (int i = 0; i < yCoords.Count; i++)
            {
                var item = yCoords[i];
                double lineX = GetX(i) + rectWidth / 2;
                double lineY = GetY_ForPrice(item);
                if (lines.Count <= DrawObjectListOffset)
                {
                    lines.Add(new Line());
                }

                if (first)
                {
                    first = false;
                }
                else
                {
                    if (DrawObjectListOffset < MAX_DRAWING_OBJECTS)
                    {
                        Line line = lines[DrawObjectListOffset];
                        line.X1 = prevX;
                        line.Y1 = prevY;
                        line.X2 = lineX;
                        line.Y2 = lineY;
                        line.StrokeThickness = thickness;
                        line.Stroke = brushColor;
                        canvas.Children.Add(line);
                    }

                }
                prevX = lineX;
                prevY = lineY;
                DrawObjectListOffset++;
            }
        }

        /// <summary>
        ///Draws a line segment between points, offset to the center of the candles
        /// </summary>
        /// <param name="start">Start point.</param>
        /// <param name="end">End point.</param>
        protected void DrawLineSegment(double startX, double endX, ref double startY, ref double endY, double thickness, SolidColorBrush brushColor, DrawingContext dc, double space = 0)
        {
            //return;
            if (!Chart.canDraw)
            {
                return;// new Point();
            }
            if (_ChartLine.movingLine != null)
            {
                return;// new Point();
            }

            startX = GetX(startX);// + space;
            startY =  GetY_ForPrice_dc(startY);
            endX = GetX(endX);// - space;
            endY =  GetY_ForPrice_dc(endY);

            Pen pen = new Pen(brushColor, thickness);
            dc.DrawLine(pen, new Point(startX + space, startY), new Point(endX + space, endY));

        }

        protected void DrawLineSegment(Point start, Point end, double thickness, SolidColorBrush brushColor, double space = 0)
        {
            //return;
            if (!Chart.canDraw)
            {
                return;
            }
            if (_ChartLine.movingLine != null)
            {
                return;
            }

            double startX = GetX(start.X);// + space;
            double startY = GetY_ForPrice(start.Y);
            double endX = GetX(end.X);// - space;
            double endY = GetY_ForPrice(end.Y);

            if (lines.Count <= DrawObjectListOffset)
            {
                lines.Add(new Line());
            }
            if (DrawObjectListOffset < MAX_DRAWING_OBJECTS)
            {
                Line line = lines[DrawObjectListOffset];
                line.X1 = startX + space;
                line.Y1 = startY;
                line.X2 = endX + space;
                line.Y2 = endY;
                line.StrokeThickness = thickness;
                line.Stroke = brushColor;
                canvas.Children.Add(line);
            }

            DrawObjectListOffset++;
        }


        #endregion

        #region Draw text
        protected void DrawText(string text, double left, double top)
        {
            TextBlock tb = new TextBlock()
            {
                Text = text,
                FontSize = 12
            };
            Canvas.SetLeft(tb, left);
            Canvas.SetTop(tb, top);
            canvas.Children.Add(tb);
        }

        #endregion

        #region Draw shapes

        protected void DrawMarker(MarkerStyle style,Point position, SolidColorBrush color, double size)
        {
            double candleWidth = CandleWidth;
            double offset = (candleWidth - size) / 2;
            double X = GetX(position.X) + offset; //marker will show on the next bar along
            double Y = position.Y;

            switch (style)
            {
                case MarkerStyle.TRIANGLEUP:
                    canvas.Children.Add(DrawingObjects.TriangleUp(size, X, Y, color, Brushes.Black));
                    break;
                case MarkerStyle.TRIANGLEDOWN:
                    canvas.Children.Add(DrawingObjects.TriangleDown(size, X, Y, color, Brushes.Black));
                    break;
                case MarkerStyle.SQUARE:
                    canvas.Children.Add(DrawingObjects.Square(size, X, Y, color, Brushes.Black));
                    break;
                case MarkerStyle.CIRCLE:
                    canvas.Children.Add(DrawingObjects.Circle(size, X, Y, color, Brushes.Black));
                    break;
            }
        }

        #endregion


        /*
        //this does not send the event after the individual canvases are resized following a maximise, but just before so it doesn't help
        canvas.SizeChanged += new SizeChangedEventHandler((obj,eventArgs) =>
        {
            if (numFrames != 0)
            {
                Draw(startFrame, numFrames);
            }
        }
        );
        */

    }
}
