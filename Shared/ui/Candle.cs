using AlgoTrader.Shared.Enums;
using Shared.Drawings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Data.Entity;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using static Shared.Drawings.DrawingObjects;
using System.Diagnostics;
using System.Windows.Forms;

namespace AlgoTrader.Shared
{
    public class Candle
    {


        public static SolidColorBrush CANDLE_COLOR_UP_HA = new SolidColorBrush(Color.FromRgb(38, 198, 218));
        public static SolidColorBrush CANDLE_COLOR_DOWN_HA = new SolidColorBrush(Color.FromRgb(239, 83, 80));
        public static SolidColorBrush CANDLE_COLOR_UP = new SolidColorBrush(Color.FromRgb(38, 166, 154));
        public static SolidColorBrush CANDLE_COLOR_DOWN = new SolidColorBrush(Color.FromRgb(239, 83, 80));
        static readonly SolidColorBrush CANDLE_COLOR_EMPTY = Brushes.White;
        public static SolidColorBrush CANDLE_COLOR_INTERRUPTION = Brushes.LightGray;

        static double canvasHeight, canvasWidth, marginTop, marginBottom, marginRight, marginLeft, Scale, High;

        public DrawingGroup drawingGroup;
        Pen linePen;
        Point _wickStart = new Point();
        Point _wickEnd = new Point();
        Rect _candleBody = new Rect();
        DisplayPanel panelBase;
        public int lastXPosition;
        _SymbolDataItem currentDataItem;
        internal int xOffset, yOffset;
        bool interruption;

        static Candle()
        {
            if (CANDLE_COLOR_UP_HA.CanFreeze)
            {
                CANDLE_COLOR_UP_HA.Freeze();
            }
            if (CANDLE_COLOR_DOWN_HA.CanFreeze)
            {
                CANDLE_COLOR_DOWN_HA.Freeze();
            }
            if (CANDLE_COLOR_UP.CanFreeze)
            {
                CANDLE_COLOR_UP.Freeze();
            }
            if (CANDLE_COLOR_DOWN.CanFreeze)
            {
                CANDLE_COLOR_DOWN.Freeze();
            }
            if (CANDLE_COLOR_INTERRUPTION.CanFreeze)
            {
                CANDLE_COLOR_INTERRUPTION.Freeze();
            }
            if (CANDLE_COLOR_EMPTY.CanFreeze)
            {
                CANDLE_COLOR_EMPTY.Freeze();
            }
        }

        public Candle(DisplayPanel panelBase)// : base(canvas)
        {

            this.panelBase = panelBase;

            drawingGroup = null;
            linePen = new Pen
            {
                Thickness = 1
            };
        }

        public void Reset(List<_SymbolDataItem> items)
        {
            if (items == null || !items.Any())
            {
                return;
            }
            currentDataItem = new _SymbolDataItem { Open = items.First().Close, High = items.Max(x => x.Close), Low = items.Min(x => x.Close), Close = items.Last().Close };
        }
        public void Reset(_SymbolDataItem item, int yOffset, DisplayPanel panelBase)
        {
            currentDataItem = new _SymbolDataItem { Open = item.Close, High = item.Close, Low = item.Close, Close = item.Close };

            //this.interruption = false;
            //this.xOffset = -1;
            //this.yOffset = yOffset;
            //this.panelBase = panelBase;
        }

        //update for live candle
        public void Update(_SymbolDataItem item, bool interruption, int yOffset, DisplayPanel panelBase, Image currentCandleImage)
        {
            if (currentDataItem == null || item == null)
            {
                return;
            }
            currentDataItem.Close = item.Close;
            currentDataItem.High = Math.Max(currentDataItem.High, item.Close);
            currentDataItem.Low = Math.Min(currentDataItem.Low, item.Close);

            this.interruption = interruption;
            this.xOffset = lastXPosition;
            this.yOffset = yOffset;
            this.panelBase = panelBase;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var _currentCandleDrawingGroup = new DrawingGroup();
                currentCandleImage.Source = new DrawingImage(_currentCandleDrawingGroup);

                using (DrawingContext dc = _currentCandleDrawingGroup.Open())
                {
                    (double left, double top) = DrawCurrent(dc);

                    //canvas is just to fit candle, so need to offset
                    Canvas.SetTop(currentCandleImage, top + yOffset);
                    Canvas.SetLeft(currentCandleImage, left);
                }
            });
        }

        //update for static candles, prior to drawing
        public void Update(_SymbolDataItem item, bool interruption, int xOffset, int yOffset, DisplayPanel panelBase, DrawingContext dc)
        {
            currentDataItem = item;

            this.interruption = interruption;
            this.xOffset = xOffset;
            this.yOffset = yOffset;
            this.panelBase = panelBase;
        }

        public static void SetCanvasDims(double scale, double high, double width, double height, double marginTop, double marginBottom, double marginRight, double marginLeft)
        {
            canvasWidth = width;
            canvasHeight = height;
            Candle.marginTop = marginTop;
            Candle.marginBottom = marginBottom;
            Candle.marginRight = marginRight;
            Candle.marginLeft = marginLeft;
            Scale = scale;
            High = high;
        }

        public static double CandleWidth
        {
            get
            {
                return (canvasWidth - marginRight) / Chart.ScaleBars;
            }
        }
        public static double GetX(double barNum)
        {
            return CandleWidth * barNum;
        }

        public static double GetY_ForPriceNoOffset(double value) //as above, but no offset for vertical scroll, as that does not work with drawing context (must move entire image)
        {
            var v = High - value;
            return MainObj.invertChart ? canvasHeight - GetY_Relative(v) : GetY_Relative(v);
        }

        public static double GetY_Relative(double value) //relative value, nothing to do with drawing orientation
        {
            //return (High - value) * Scale + canvas.Margin.Top;
            return value * Scale + marginTop - marginBottom;
        }

        //candle is updated during ItemUpdated loop, but will only need drawing if visible.
        //if not visible (chart), still need update so when chart switches, the current candle has correct data
        public static int minTop = int.MaxValue;
        public void Draw(DrawingContext dc)
        {
            try
            {
                if (currentDataItem == null)
                {
                    return;
                }

                //offset is the candle X-position when the candles are drawn sequentially
                //when updating a candle, need the last known X-position to correctly position it
                //could save the timestamp for the candle then calculate from that, like TradeEngine.GetFrameForTrade(), but this is just simpler as it will be reset whenever the screen is redrawn
                if (xOffset == -1)
                {
                    xOffset = lastXPosition;
                }
                else
                {
                    lastXPosition = xOffset;
                }

                var barTop = currentDataItem.Open > currentDataItem.Close ? currentDataItem.Open : currentDataItem.Close;
                var barBottom = currentDataItem.Open != 0 ? currentDataItem.Open > currentDataItem.Close ? currentDataItem.Close : currentDataItem.Open : currentDataItem.Close; //open will be zero initially
                var barMax = currentDataItem.High > currentDataItem.Low ? currentDataItem.High : currentDataItem.Low;
                var barMin = currentDataItem.Low != 0 ? currentDataItem.High > currentDataItem.Low ? currentDataItem.Low : currentDataItem.High : currentDataItem.High;

                double candleLeft = GetX(lastXPosition);
                double wickTop = MainObj.invertChart ? GetY_ForPriceNoOffset(barMin) : GetY_ForPriceNoOffset(barMax);// * scale + canvas.Margin.Top;
                double wickLength = GetY_Relative(barMax - barMin);
                double wickLeft = (CandleWidth - 2) / 2 + 1;
                wickLeft += candleLeft;

                if (minTop > wickTop) { minTop = (int) wickTop; }
                _wickStart.X = wickLeft;
                _wickStart.Y = wickTop;
                _wickEnd.X = wickLeft;
                _wickEnd.Y = wickTop + wickLength;

                double rectHeight = GetY_Relative(barTop - barBottom);
                rectHeight = rectHeight == 0 ? 1 : rectHeight;
                double rectTop = MainObj.invertChart ? GetY_Relative(barBottom - barMin) : GetY_Relative(barMax - barTop); //offset within container for entire candle
                rectTop += wickTop;
                var w = panelBase.CandleWidth - 2;
                var rectWidth = w > 1 ? w : 1;

                _candleBody.X = candleLeft;
                _candleBody.Y = rectTop;
                _candleBody.Width = rectWidth;
                _candleBody.Height = rectHeight;

                Brush col = interruption
                    ? CANDLE_COLOR_INTERRUPTION
                    : Chart.showHeikinAshi
                        ? currentDataItem.Open < currentDataItem.Close
                            ? CANDLE_COLOR_UP_HA
                            : CANDLE_COLOR_DOWN
                        : currentDataItem.Open < currentDataItem.Close
                            ? CANDLE_COLOR_UP
                            : CANDLE_COLOR_EMPTY;

                linePen.Brush = currentDataItem.Open < currentDataItem.Close
                        ? CANDLE_COLOR_UP
                        : CANDLE_COLOR_DOWN;

                dc.DrawLine(linePen, _wickStart, _wickEnd);
                dc.DrawRectangle(col, linePen, _candleBody);
           
            }
            catch
            {
            }

        }

        public (double left, double top) DrawCurrent(DrawingContext dc)
        {
            try
            {
                if (currentDataItem != null)
                {
                    //offset is the candle X-position when the candles are drawn sequentially
                    //when updating a candle, need the last known X-position to correctly position it
                    //could save the timestamp for the candle then calculate from that, like TradeEngine.GetFrameForTrade(), but this is just simpler as it will be reset whenever the screen is redrawn
                    if (xOffset == -1)
                    {
                        xOffset = lastXPosition;
                    }
                    else
                    {
                        lastXPosition = xOffset;
                    }

                    var barTop = currentDataItem.Open > currentDataItem.Close ? currentDataItem.Open : currentDataItem.Close;
                    var barBottom = currentDataItem.Open != 0 ? currentDataItem.Open > currentDataItem.Close ? currentDataItem.Close : currentDataItem.Open : currentDataItem.Close; //open will be zero initially
                    var barMax = currentDataItem.High > currentDataItem.Low ? currentDataItem.High : currentDataItem.Low;
                    var barMin = currentDataItem.Low != 0 ? currentDataItem.High > currentDataItem.Low ? currentDataItem.Low : currentDataItem.High : currentDataItem.High;

                    double candleLeft = GetX(lastXPosition);
                    double wickTop = MainObj.invertChart ? GetY_ForPriceNoOffset(barMin) : GetY_ForPriceNoOffset(barMax);// * scale + canvas.Margin.Top;
                    double wickLength = GetY_Relative(barMax - barMin);
                    double wickLeft = (CandleWidth - 2) / 2 + 1;
                    //wickLeft += candleLeft;

                    _wickStart.X = wickLeft;
                    _wickStart.Y = 0;
                    _wickEnd.X = wickLeft;
                    _wickEnd.Y = wickLength;

                    double rectHeight = GetY_Relative(barTop - barBottom);
                    rectHeight = rectHeight == 0 ? 1 : rectHeight;
                    double rectTop = MainObj.invertChart ? GetY_Relative(barBottom - barMin) : GetY_Relative(barMax - barTop); //offset within container for entire candle
                    var w = panelBase.CandleWidth - 2;
                    var rectWidth = w > 1 ? w : 1;

                    _candleBody.X = 0;
                    _candleBody.Y = rectTop;
                    _candleBody.Width = rectWidth;
                    _candleBody.Height = rectHeight;

                    Brush col = interruption
                        ? CANDLE_COLOR_INTERRUPTION
                        : Chart.showHeikinAshi
                            ? currentDataItem.Open < currentDataItem.Close
                                ? CANDLE_COLOR_UP_HA
                                : CANDLE_COLOR_DOWN
                            : currentDataItem.Open < currentDataItem.Close
                                ? CANDLE_COLOR_UP
                                : CANDLE_COLOR_EMPTY;

                    //clear
                    dc.DrawRectangle(MainObj.invertChart ? Brushes.AntiqueWhite : Brushes.White, new Pen(), new Rect(0, 0, rectWidth, wickLength));

                    linePen.Brush = currentDataItem.Open < currentDataItem.Close
                            ? CANDLE_COLOR_UP
                            : CANDLE_COLOR_DOWN;

                    dc.DrawLine(linePen, _wickStart, _wickEnd);
                    dc.DrawRectangle(col, linePen, _candleBody);
                    return (candleLeft, wickTop);
                }
            }
            catch
            {
            }
            return (0, 0);

        }
    }

}
