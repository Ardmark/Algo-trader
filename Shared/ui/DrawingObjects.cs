using AlgoTrader.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Shared.Drawings
{
    public class DrawingObjects
    {
        public const double MARKER_OPACITY = 0.67;
        const double size = 30;


        #region Trade markers
        /*
           * Trade marker
           * This just encapsulates the canvas with an update method to move it, for the trailing stop 
           * */
        public class TradeMarkerCanvas
        {
            public Canvas canvas;
            double offset;
            public Line line = null;

            public TradeMarkerCanvas(Canvas canvas, double offset = 0)
            {
                this.canvas = canvas;
                this.offset = offset;
            }


            public TradeMarkerCanvas(Canvas canvas, double left, double top, double offset = 0)
            {
                this.canvas = canvas;
                this.offset = offset;
                Canvas.SetLeft(canvas, left);
                Canvas.SetTop(canvas, top + offset);

            }
            public void Update(double top)
            {
                Canvas.SetTop(canvas, top + offset);
                if (line != null)
                {
                    line.Y1 = line.Y2 = top;
                }
            }
            public void Update(double left, double top)
            {
                Update(top);
                Canvas.SetLeft(canvas, left);
            }
        }

        //various static methods to return drawing objects. These should obviously be replaced with classes which could be instantiated, etc.

        public static Shape TriangleUp(double size, double left, double top, SolidColorBrush fill, SolidColorBrush stroke)
        {
            double halfSize = size / 2;
            Polygon triangle = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(halfSize, 0),
                    new Point(size, size),
                    new Point(0, size)
                },
                Fill = fill,
                Stroke = stroke
            };

            Canvas.SetLeft(triangle, left);
            Canvas.SetTop(triangle, top);
            return triangle;
        }
        
        public static Shape TriangleDown(double size, double left, double top, SolidColorBrush fill, SolidColorBrush stroke)
        {
            double halfSize = size / 2;
            Polygon triangle = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(0, 0),
                    new Point(size, 0),
                    new Point(halfSize, size)
                },
                Fill = fill,
                Stroke = stroke
            };

            Canvas.SetLeft(triangle, left);
            Canvas.SetTop(triangle, top);
            return triangle;
        }
        
        public static Shape Square(double size, double left, double top, SolidColorBrush fill, SolidColorBrush stroke)
        {
            Rectangle rect = new Rectangle
            {
                Width = size,
                Height = size,
                Fill = fill,
                Stroke = stroke
            };

            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, top);
            return rect;
        }
        
        public static Shape Circle(double size, double left, double top, SolidColorBrush fill, SolidColorBrush stroke)
        {
            Ellipse circle = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = fill,
                Stroke = stroke
            };

            Canvas.SetLeft(circle, left);
            Canvas.SetTop(circle, top);
            return circle;
        }
        
        public static TradeMarkerCanvas CurrentPriceMarker(double left, double top, SolidColorBrush fill, SolidColorBrush stroke)
        {
            double halfSize = size / 4;
            double xOffset = 2;
            Polygon triangle1 = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(xOffset + 0, 0),
                    new Point(xOffset + halfSize * 2, -halfSize / 2 - 1),
                    new Point(xOffset + halfSize * 2, 0),
                    new Point(xOffset + halfSize * 4, 0),
                    new Point(xOffset + halfSize * 4, 0),
                    new Point(xOffset + halfSize * 2, 0),
                    new Point(xOffset + halfSize * 2, halfSize / 2)
                },
                //Fill = fill,
                Stroke = stroke
            };


            Canvas canvas = new Canvas();
            canvas.Children.Add(triangle1);

            return new TradeMarkerCanvas(canvas, left, top, 1); //just to line up vertically, have a one px offset
        }
        
        public static TradeMarkerCanvas TradeOrderMarker(TradePosition position, double left, double top, SolidColorBrush fill, SolidColorBrush stroke, SolidColorBrush textColor, string extraText)
        {
            double halfSize = size / 2;
            double arrowSize = size / 3;

            Canvas canvas = new Canvas { Opacity = MARKER_OPACITY };

            Polygon arrow = position == TradePosition.SHORT
                ? new Polygon
            {
                Points = new PointCollection
                {
                    new Point(0, 0),
                    new Point(arrowSize / 2-1, 0),
                    new Point(arrowSize / 2-1, -arrowSize),
                    new Point(arrowSize / 2+1, -arrowSize),
                    new Point(arrowSize / 2+1, 0),
                    new Point(arrowSize, 0),
                    new Point(arrowSize / 2, arrowSize)
                },
                Fill = fill,
                Stroke = stroke
            }
            : new Polygon
            {
                Points = new PointCollection
                {
                    new Point(0, 0),
                    new Point(arrowSize / 2-1, 0),
                    new Point(arrowSize / 2-1, arrowSize),
                    new Point(arrowSize / 2+1, arrowSize),
                    new Point(arrowSize / 2+1, 0),
                    new Point(arrowSize, 0),
                    new Point(arrowSize / 2, -arrowSize)
                },
                Fill = fill,
                Stroke = stroke
            };
            canvas.Children.Add(arrow);
            Canvas.SetLeft(arrow, -halfSize);
            Canvas.SetTop(arrow, halfSize);


            Ellipse circle = new Ellipse
            {
                Width = halfSize,
                Height = halfSize,
                Fill = fill,
                Stroke = stroke,

            };


            canvas.Children.Add(circle);
            Canvas.SetTop(circle, halfSize / 2);

            if (extraText != null)
            {
                TextBlock textBlock = new TextBlock
                {
                    Text = extraText,
                    Foreground = textColor,
                    Background = Brushes.White
                };

                canvas.Children.Add(textBlock);
                Canvas.SetLeft(textBlock, 20);
                Canvas.SetTop(textBlock, size / 2 + 2);
            }
               
            return new TradeMarkerCanvas(canvas, left - halfSize / 2, top, -halfSize);
  
        }
        
        public static TradeMarkerCanvas TradeEntryMarker(double left, double top, SolidColorBrush fill, SolidColorBrush stroke, SolidColorBrush textColor, string extraText)
        {

            Canvas canvas = new Canvas { Opacity = MARKER_OPACITY };

            double halfSize = size / 4;
            Polygon triangle1 = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(0, -halfSize * 1.5),
                    new Point(halfSize * 1.5, 0),
                    new Point(0, halfSize * 1.5)
                },
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = 2,
            };
            canvas.Children.Add(triangle1);


            if (extraText != null)
            {
                TextBlock textBlock = new TextBlock
                {
                    Text = extraText,
                    Foreground = textColor,
                    Background = Brushes.White
                };

                canvas.Children.Add(textBlock);
                Canvas.SetLeft(textBlock, 6);
                Canvas.SetTop(textBlock, size);
            }
            return new TradeMarkerCanvas(canvas, left - halfSize * 1.5, top, 1); //just to line up vertically, have a one px offset
        }

        static void DrawWarningMarker(Canvas canvas, SolidColorBrush fill, SolidColorBrush stroke, double halfSize, double leftOffset, double topOffset)
        {

            Polygon block = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(-halfSize * .5 + leftOffset, -halfSize * 2 - topOffset),
                    new Point(halfSize * .5 + leftOffset, -halfSize * 2 - topOffset),
                    new Point(halfSize * 1.255 + leftOffset, -size * 1.5 - halfSize  - topOffset),
                    new Point(-halfSize * 1.255 + leftOffset, -size * 1.5 - halfSize  - topOffset)
                },
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = 2,
            };
            canvas.Children.Add(block);

            Ellipse circle = new Ellipse
            {
                Width = size / 2,
                Height = size / 2,
                Fill = fill,
                Stroke = stroke,

            };
            Canvas.SetTop(circle, -halfSize * 1.2 - topOffset);
            Canvas.SetLeft(circle, -halfSize + leftOffset);

            canvas.Children.Add(circle);

        }
        public static TradeMarkerCanvas TradeEntryMarkerClosedOnExchange(double left, double top, SolidColorBrush fill, SolidColorBrush stroke, SolidColorBrush textColor, string extraText)
        {

            Canvas canvas = new Canvas { Opacity = MARKER_OPACITY };

            double halfSize = size / 4;
           
            DrawWarningMarker(canvas, fill, stroke, halfSize, 0, size);

            Polygon triangle1 = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(0, -halfSize * 1.5),
                    new Point(halfSize * 1.5, 0),
                    new Point(0, halfSize * 1.5)
                },
                Fill = fill,
                Stroke = stroke
            };
            canvas.Children.Add(triangle1);

            if (extraText != null)
            {
                TextBlock textBlock = new TextBlock
                {
                    Text = extraText,
                    Foreground = textColor,
                    Background = Brushes.White
                };

                canvas.Children.Add(textBlock);
                Canvas.SetLeft(textBlock, 6);
                Canvas.SetTop(textBlock, size);
            }
            return new TradeMarkerCanvas(canvas, left - halfSize * 1.5, top, 1); //just to line up vertically, have a one px offset
        }

        public static TradeMarkerCanvas TradeStopMarker(double left, double top, SolidColorBrush fill, SolidColorBrush stroke, SolidColorBrush textColor, string text, bool flipOrder, bool showWarning)
        {
            double halfSize = size / 2;
            Canvas canvas = new Canvas { Opacity = MARKER_OPACITY };

            if (showWarning)
            {
                DrawWarningMarker(canvas, fill, stroke, size / 4, halfSize / 2, halfSize / 4);
            }

            Polygon triangle = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(halfSize / 2, 0),
                    new Point(halfSize, halfSize),
                    new Point(0, halfSize)
                },
                Fill = fill,
                Stroke = stroke
            };

            canvas.Children.Add(triangle);
            Canvas.SetTop(triangle, halfSize);

            if (flipOrder)
            {
                Polygon triangle1 = new Polygon
                {
                    Points = new PointCollection
                    {
                        new Point(0, 0),
                        new Point(halfSize, 0),
                        new Point(halfSize / 2, halfSize)
                    },
                    Fill = fill,
                    Stroke = stroke
                };

                canvas.Children.Add(triangle1);
                Canvas.SetTop(triangle1, 0);
            }

            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = textColor,
                Background = Brushes.White
            };

            canvas.Children.Add(textBlock);
            Canvas.SetLeft(textBlock, 20);
            Canvas.SetTop(textBlock, size / 2 + 2);

            return new TradeMarkerCanvas(canvas, left - halfSize / 2, top, -halfSize);
        }

        public static TradeMarkerCanvas TradeTakeProfitMarker(double left, double top, SolidColorBrush fill, SolidColorBrush stroke, SolidColorBrush textColor, TradePosition tradePosition, string text, bool flipOrder)
        {
            double halfSize = size / 2;
            Polygon triangle1 = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(0, 0),
                    new Point(halfSize, 0),
                    new Point(halfSize / 2, halfSize)
                },
                Fill = fill,
                Stroke = stroke
            };

            Canvas canvas = new Canvas { Opacity = MARKER_OPACITY };
            canvas.Children.Add(triangle1);

            if (flipOrder)
            {
                Ellipse circle = new Ellipse
                {
                    Width = halfSize,
                    Height = halfSize,
                    Fill = fill,
                    Stroke = stroke,
                };

                canvas.Children.Add(circle);
                Canvas.SetTop(circle, -halfSize - 2);
            }
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = textColor,
                Background = Brushes.White
            };

            canvas.Children.Add(textBlock);
            Canvas.SetLeft(textBlock, 20);
            Canvas.SetTop(textBlock, -4);
            return new TradeMarkerCanvas(canvas, left - halfSize / 2, top, -halfSize);
            /*
            double halfSize = size / 2;
            Polygon triangle1 = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(-halfSize, -halfSize / 2),
                    new Point(-halfSize, halfSize / 2),
                    new Point(0, 0)
                },
                Fill = fill,
                Stroke = stroke
            };
            Polygon triangle2 = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(0, 0),
                    new Point(halfSize, halfSize / 2),
                    new Point(halfSize, -halfSize / 2)
                },
                Fill = fill,
                Stroke = stroke
            };
            Canvas canvas = new Canvas { Opacity = MARKER_OPACITY };
            canvas.Children.Add(triangle1);
            //canvas.Children.Add(triangle2);
          

            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = textColor,
                Background = Brushes.White
            };

            canvas.Children.Add(textBlock);
            Canvas.SetLeft(textBlock, 6);
            Canvas.SetTop(textBlock, size - 48);
            return new TradeMarkerCanvas(canvas, left, top, 0);// -halfSize);

            */
        }
        
        public static TradeMarkerCanvas TradeFloorMarker(double left, double top, SolidColorBrush fill, SolidColorBrush stroke, SolidColorBrush textColor, TradePosition tradePosition, string text)
        {
            double halfSize = size / 2;
            Ellipse circle = new Ellipse
            {
                 Width = halfSize,
                 Height = halfSize,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = 2,
                 
            };
            
            Canvas canvas = new Canvas { Opacity = MARKER_OPACITY };
            
            canvas.Children.Add(circle);

            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = textColor,
                Background = Brushes.White,
            };

            canvas.Children.Add(textBlock);
            Canvas.SetLeft(textBlock, 0);
            Canvas.SetTop(textBlock, 12 - size);

            return new TradeMarkerCanvas(canvas, left - halfSize / 2, top, -halfSize / 2);


        }

        public static TradeMarkerCanvas TradeBreakEvenMarker(double left, double top, SolidColorBrush fill, SolidColorBrush stroke, SolidColorBrush textColor, TradePosition tradePosition, string text)
        {
            double halfSize = size / 2;

            Rectangle square = new Rectangle
            {
                Width = halfSize,
                Height = halfSize,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = 2,
            };

            Canvas canvas = new Canvas { Opacity = MARKER_OPACITY };

            canvas.Children.Add(square);

            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = textColor,
                Background = Brushes.White,
            };

            canvas.Children.Add(textBlock);
            Canvas.SetLeft(textBlock, 0);
            Canvas.SetTop(textBlock, 12 - size);

            return new TradeMarkerCanvas(canvas, left - halfSize / 2, top, -halfSize / 2);
        }

        public static TradeMarkerCanvas TradeExitMarker(double left, double top, SolidColorBrush fill, SolidColorBrush stroke, SolidColorBrush textColor, string text)
        {
            double halfSize = size / 4;
            Polygon triangle2 = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(0, 0),
                    new Point(halfSize * 1.5, halfSize * 1.5),
                    new Point(halfSize * 1.5, -halfSize * 1.5)
                },
                Fill = fill,
                Stroke = stroke, 
                StrokeThickness = 2,
                Opacity = MARKER_OPACITY
            };
            Canvas canvas = new Canvas { Opacity = 1 };
            canvas.Children.Add(triangle2);
            Canvas.SetLeft(canvas, left);
            //Canvas.SetTop(canvas, top - halfSize);


            TextBlock tb = new TextBlock
            {
                Text = text,
                Opacity = 1,
                Padding = new Thickness(4, 0, 4, 0)
            };

            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            tb.Arrange(new Rect(tb.DesiredSize));

            Rectangle rect = new Rectangle
            {
                Width = tb.ActualWidth,
                Height = halfSize * 3,
                Fill = Brushes.White,
                Stroke = textColor,
                Opacity = 1
            };
            Canvas.SetLeft(tb, halfSize*1.5);
            Canvas.SetLeft(rect, halfSize * 1.5);
            Canvas.SetTop(rect, -halfSize * 1.5);
            Canvas.SetTop(tb, -8);
            canvas.Children.Add(rect);
            canvas.Children.Add(tb);
            return new TradeMarkerCanvas(canvas, left, top + halfSize, -halfSize);
        }

        #endregion
    }
}
