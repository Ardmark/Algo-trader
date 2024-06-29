using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Windows;
using Shared.Drawings;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AlgoTrader.Shared
{
    public partial class _ChartLine : UIDrawingBase //as opposed to line, which is a WPF object
    {
        #region Properties

        //the data properties for this class are in a separate file in DataObjects, to keep all DB elements together

        /*
        //base class properties
        public string Symbol { get; set; }
        public Enums.ExchangeId ExchangeId { get; set; } = Enums.ExchangeId.BINANCE;

        //drawing can be associated with a trade
        public int TradeId { get; set; }

        public bool IsVisible { get; set; }
        public double Opacity { get; set; }

        //color
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
        public byte Alpha { get; set; }

        //bounds
        public DateTime StartTimeStamp { get; set; }
        public DateTime EndTimeStamp { get; set; }
        public double StartPrice { get; set; }
        public double EndPrice { get; set; }
        public int StartFrame { get; set; } //to save getting frame from timestamp each time
        public int EndFrame { get; set; }

        //line-specific properties
        public LineType Type { get; set; }
        public LineStyle Style { get; set; }
        public double Thickness { get; set; }

        */
        #endregion

        #region Fields
        Line line = null;
        public static _ChartLine movingLine = null;
        bool isDraggingStart, isDraggingEnd;

        #endregion

        #region Init

        public static Line CurrentLine { get; set; }
        public void InitLine(bool active)
        {
            if (line == null)
            {
                line = new Line();

                line.MouseEnter += (object sender, MouseEventArgs e) =>
                {
                    line.Cursor = Cursors.Pen;
                    e.Handled = true;
                    CurrentLine = line;
                };
                line.MouseLeave += (object sender, MouseEventArgs e) =>
                {
                    CurrentLine = null;
                    e.Handled = true;
                };
                line.MouseLeftButtonDown += (object sender, MouseButtonEventArgs e) =>
                {
                    movingLine = this;
                    MainObj.visibleChart.currentLine = this;

                    Trade = MainObj.visibleTradeEngine.UI_Trades.FirstOrDefault(x => x.Id == TradeId);
                    e.Handled = true; //prevents main view model from getting mouse down, so it does not set mouseDown to true which is passed to Chart with mouseMove, so chart does not drag, only line
                };
                line.MouseRightButtonDown += (object sender, MouseButtonEventArgs e) =>
                {
                    e.Handled = true; //prevents main view model from getting mouse down, so it does not set mouseDown to true which is passed to Chart with mouseMove, so chart does not drag, only line
                };
                //this is not needed as Chart handles this event
                //line.MouseUp += (object sender, MouseButtonEventArgs e) =>
                //{
                //    MouseUp_UI_synchronised();
                //    e.Handled = true; //does not block event from reaching Chart as event is added to chart in MainObj
                //};
                line.MouseRightButtonUp += (object sender, System.Windows.Input.MouseButtonEventArgs e) =>
                {
                    //using the main view model rather than putting code here,
                    //as there are a couple of methods there for show / hide of window, and need to access that object anyway
                    // to set ownership of the popup and popup position
                    MainObj.mainViewModel.PopupMenuForLine(sender, e);
                    movingLine = null;
                };
                line.Name = "_" + Id.ToString(); //cannot start with a number
            }

            void SetColor(int r, int g, int b, int a = 255)
            {
                Red = (byte)r;
                Green = (byte)g;
                Blue = (byte)b;
                Alpha = (byte)a;
            }
            void SetColorFromSolidColorBrush(SolidColorBrush color)
            {
                Red = color.Color.R;
                Green = color.Color.G;
                Blue = color.Color.B;
                Alpha = color.Color.A;
            }
            //var canvas = chart.canvas;

            switch (Type)
            {
                case LineType.NONE:
                    Style = LineStyle.SOLID;
                    SetColor(0, 0,0, (int) (DrawingObjects.MARKER_OPACITY * 255));
                    Thickness = 2;
                    break;
                case LineType.BREAK:
                    Style = LineStyle.SOLID;
                    if (active)
                    {
                        SetColor(0, 0, 255, (int)(DrawingObjects.MARKER_OPACITY * 255));
                    }
                    else
                    {
                        SetColor(0, 0, 0, (int)(DrawingObjects.MARKER_OPACITY * 255));
                    }
                    Thickness = 2;
                    break;
                case LineType.SUPPORT:
                    Style = LineStyle.SOLID;
                    SetColor(255, 0, 0, (int)(DrawingObjects.MARKER_OPACITY * 255));
                    Thickness = 2;
                    break;
                case LineType.RESISTANCE:
                    Style = LineStyle.SOLID;
                    SetColor(0, 255, 0, (int)(DrawingObjects.MARKER_OPACITY * 255));
                    Thickness = 2;
                    break;
                case LineType.CLOSED_ENTERED:
                    Style = LineStyle.SOLID;
                    SetColorFromSolidColorBrush(Chart.STOPPED_COLOR);
                    Thickness = 2;
                    break;
                case LineType.CLOSED_UNENTERED:
                    Style = LineStyle.SOLID;
                    SetColorFromSolidColorBrush(Chart.CANCELLED_COLOR);
                    Thickness = 2;
                    break;
            }

            DoubleCollection dashes = null;
            switch (Style)
            {
                case Enums.LineStyle.DASHED:
                    dashes = new DoubleCollection
                    {
                        10, 1
                    };
                    break;
                case Enums.LineStyle.DOTTED:
                    dashes = new DoubleCollection
                    {
                        2,
                        2
                    };
                    break;
            }

            line.StrokeThickness = Thickness;
            line.Stroke = new SolidColorBrush(Color.FromArgb(Alpha, Red, Green, Blue));
            line.Stroke.Freeze();
            line.StrokeDashArray = dashes;
          
        }

        #endregion

        #region Utility methods

        //unused, since it does not project line along vector, which would give false impression of trade duration anyway
        internal void SetMinimumLength(int timeFrame)
        {
            var diff = EndTimeStamp - StartTimeStamp;
            //need to show the line as a minimum of one frame length, on the current timeframe
            if (diff.TotalMinutes < timeFrame * 4)
            {
                EndTimeStamp = StartTimeStamp.AddMinutes(timeFrame * 4); //add a few bars, or will not be able to 'grab' end time so will always reverse line when extending
            }
        }

        internal void Extend(DateTime timeStamp, int timeFrame)
        {
            //if start is after timeStamp, set it to timeStamp
            //if end is before timeStamp, set it to timeStamp
            //but keep the correct line gradient
            if (timeStamp < StartTimeStamp)
            {
                var newTimeStamp = timeStamp.AddMinutes(-1);
                (StartPrice, _) = GetPriceAtTime(newTimeStamp, true);
                StartTimeStamp = newTimeStamp;
            }
            if (timeStamp > EndTimeStamp)
            {
                var newTimeStamp = timeStamp.AddMinutes(1);
                (EndPrice, _) = GetPriceAtTime(newTimeStamp, true);
                EndTimeStamp = newTimeStamp;
            }
        }

        internal (double price, bool withinLine) GetPriceAtTime(DateTime timestamp, bool projectForward)
        {
            var totSecs = EndTimeStamp.Subtract(StartTimeStamp).TotalSeconds;
            var elapsedSecs = timestamp.Subtract(StartTimeStamp).TotalSeconds;
            var priceDiff = EndPrice - StartPrice;
            var ret = priceDiff * elapsedSecs / totSecs + StartPrice;

            if (timestamp >= StartTimeStamp && timestamp <= EndTimeStamp)
            {
                return (ret, true);
            }
            else if (timestamp < StartTimeStamp)
            {
                //return (StartPrice, false);
                return (projectForward ? ret : StartPrice, false); //outside of line bounds but returns price as if still on line if projectForward == true (for stop continuation)
            }
            else
            {
                return (projectForward ? ret : EndPrice, false); //outside of line bounds but returns price as if still on line if projectForward == true (for stop continuation)
            }
        }

        internal bool TimeWithinLinePeriod(DateTime time)
        {
            return time >= StartTimeStamp && time <= EndTimeStamp;
        }


        internal void TruncateLineToTime()
        {
            if (StartTimeStamp < DateTime.UtcNow && EndTimeStamp > DateTime.UtcNow)
            {
                var newEndDate = DateTime.UtcNow;
                (var price, var _) = GetPriceAtTime(newEndDate, true);
                EndTimeStamp = newEndDate;
                EndPrice = price;
                //assume db.SaveChanges() called later
            }
        }

    #endregion
    }
}
