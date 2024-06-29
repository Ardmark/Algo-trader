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
    public class Chart : DisplayPanel
    {
   
        #region Constants


        const int NUM_BARS_TO_DISPLAY_INITIALLY = 200;
        const int SCROLL_TO_END_OFFSET = 20; //space between last bar and edge of screen when ScrollToEnd, symbol change etc. happens
        const int HORIZONTAL_GRID_LINES = 3;
        #endregion

        #region Static fields
        public static Line currentTimeMarkerLine;
        public static Canvas alertBox, timeBarChildCanvas;
        public static bool canDraw = true;

        public static bool hideTrades = false;
        public static bool showClosedTrades = true;
        public static bool showWaitingTrades = true;

        public static readonly SolidColorBrush STOPPED_COLOR = Brushes.Black;
        public static readonly SolidColorBrush CANCELLED_COLOR = Brushes.Gray;
        static readonly SolidColorBrush RUNNING_COLOR = Brushes.BlueViolet;
        static readonly SolidColorBrush WAITING_COLOR_LONG = Brushes.CornflowerBlue;
        static readonly SolidColorBrush WAITING_COLOR_SHORT = Brushes.Orange;
        static readonly SolidColorBrush STOP_COLOR_ACTIVE = Brushes.Red;
        static readonly SolidColorBrush BREAKEVEN_COLOR_ACTIVE = Brushes.Green;
        static readonly SolidColorBrush TAKE_PROFIT_COLOR_ACTIVE = Brushes.DarkGreen;
        static readonly SolidColorBrush BREAK_EVEN_COLOR = Brushes.Lime; //this is just #00ff00
        static readonly SolidColorBrush EXCHANGE_NOT_SYNCHED_COLOR = Brushes.Yellow;
        static readonly SolidColorBrush OUTLINE_COLOR = Brushes.Black;
        static readonly SolidColorBrush TEXT_COLOR = Brushes.White;
        static readonly SolidColorBrush DASHEDLINE_COLOR = Brushes.CornflowerBlue;
        static readonly SolidColorBrush WHITE = Brushes.White;
        static readonly SolidColorBrush GREYBOX_COLOR = Brushes.Gray;
        static readonly SolidColorBrush LOSS_COLOR = Brushes.Red;
        static readonly SolidColorBrush PROFIT_COLOR = Brushes.Blue;

        #endregion
        
        #region Static properties

        public static _Trade CurrentTrade { get; set; }
        internal static bool showHeikinAshi;
        public static bool ShowHeikinAshi
        {
            set
            {
                items_chart = null;
                showHeikinAshi = value;
            }
            get
            {
                return showHeikinAshi;
            }
        } 
        #endregion

        #region Fields

        public static List<DateTime> visibleTimes = new List<DateTime>();
        static List<Candle> candles = new List<Candle>();
        static List<_SymbolDataItem> items_chart;      
        Point boxStartPoint = new Point();
        double lastPrice;
        //SymbolDataItem lastItemAdded = null;
        Line priceMarkerLine = null;
        Grid panelsContainer;
        TextBlock currentPrice = null;
        TextBlock symbolText = null;
        Candle currentCandle = null;
        public _ChartLine currentLine = null;
        public double mouseX, mouseY;
        TextBlock tradePercentage = null;
        Rectangle greyBox = null;
        TradeMarkerCanvas currentPriceMarker = null;
        internal int yOffset = 0;
        public TradeMarkerCanvas liveTrailMarker;
        TextBlock overlay = null;
        List<ScreenLine> gridLines = new List<ScreenLine>();
        Image drawingImage, currentCandleImage;

        internal struct ScreenLine 
        {
            internal Point start;
            internal Point end;
            internal double Length
            {
                get
                {
                    return Math.Sqrt((end.Y - start.Y) * (end.Y - start.Y) + (end.X - start.X) * end.X - start.X);
                }
            }
        }


        #endregion

        #region Properties
        public static int ScaleBars { get; set; } = NUM_BARS_TO_DISPLAY_INITIALLY;

        private int scrollBars;
        public int ScrollBars
        {
            get {
                return scrollBars; 
            }
            set
            {
                scrollBars = value;
                if (scrollBars < 0)
                {
                    scrollBars = 0;
                }              
                //scroll limit to 80% of screen is in MouseMove, if it goes in Draw it gets juddery during mouse drag past limit
            }
        }

        public DateTime LastBarTime(List<_SymbolDataItem> items_chart)
        {

            /*
             * must look ahead because a lower timeframe marker can be within the current 'live' frame
             * e.g. if we set a marker right on the last 1-minute interval, it might be at 19:52 with the last bar at 19:51 (live bar will be at 19:52)
             * but on the 5-minute interval, the live bar will be at 19:50 and the last bar at 19:45
             * and on the 15-minute interval, the live bar will be at 19:45 and the last bar at 19:30
             * and on the 60-minute interval, the live bar will be at 19:00 and the last bar at 18:00
             * - we need to add 2 * TimeInterval to the last bar time, as follows:
             * so to display the 1-minute marker on the 5-minute interval, we need to add 2 * 5 minutes to get the range limit of 19:55
             * and to display the 1-minute marker on the 15-minute interval, we need to add 2 * 15 minutes to get the range limit 20:00
             * and to display the 1-minute marker on the 60-minute interval, we need to add 2 * 60 minutes to get the range limit 20:00
             * */
            if (items_chart.Count > 0)
            {
                return items_chart.First().Timestamp.AddMinutes(TimeFrame * (ScaleBars + 2));
            }

            //this is probably never going to be hit...
            return DateTime.UtcNow;
        }

        public DateTime LastVisibleTime { get; set; }

        public DateTime FirstVisibleTime { get; set; }

        public int EndFrame
        {
            get
            {
                return TradeEngine.results.Count;
            }
        }

        public bool showBox = false;
        public bool ShowBox
        {
            get
            {
                return showBox;
            }
            set
            {
                showBox = value;
                if (greyBox != null)
                {
                    greyBox.Visibility = showBox ? Visibility.Visible : Visibility.Hidden;
                    if (!showBox)
                    {
                        greyBox.Width = greyBox.Height = 0;
                    }
                }
            }
        }
        #endregion

        #region Init
        
        static Chart()
        {
            STOPPED_COLOR.Freeze();
            CANCELLED_COLOR.Freeze();
            RUNNING_COLOR.Freeze();
            WAITING_COLOR_LONG.Freeze();
            WAITING_COLOR_SHORT.Freeze();
            STOP_COLOR_ACTIVE.Freeze();
            TAKE_PROFIT_COLOR_ACTIVE.Freeze();
            BREAK_EVEN_COLOR.Freeze();
            EXCHANGE_NOT_SYNCHED_COLOR.Freeze();
            OUTLINE_COLOR.Freeze();
            TEXT_COLOR.Freeze();
            DASHEDLINE_COLOR.Freeze();
            WHITE.Freeze();
            GREYBOX_COLOR.Freeze();
            LOSS_COLOR.Freeze();
            PROFIT_COLOR.Freeze();
        }

        public Chart(string symbolName, int timeInterval, TradeEngine tradeEngine, Grid panelsContainer) : base(symbolName, timeInterval, tradeEngine, null)
        {
            this.panelsContainer = panelsContainer;

            //price marker is added to this canvas, so that is done in Draw()
            var lineCol = Brushes.DarkGray;
            lineCol.Freeze();
            priceMarkerLine = new Line
            {
                Stroke = lineCol,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection   {  3,  3  }
            };
            priceMarkerLine.MouseDown += (sender, e) => e.Handled = false;
            priceMarkerLine.IsEnabled = false;

            var greyBoxCol = new SolidColorBrush
            {
                Color = Color.FromRgb(128, 128, 128),
                Opacity = 0.5
            };
            greyBoxCol.Freeze();
            var greyBoxLine = Brushes.Black;
            greyBoxLine.Freeze();
            greyBox = new Rectangle
            {
                Fill = greyBoxCol,
                Stroke = greyBoxLine
            };
            greyBox.MouseDown += (sender, e) => e.Handled = false;
            //timeMarkerLine.PreviewMouseDown += (object sender, System.Windows.Input.MouseButtonEventArgs e) => e.Handled = true; -no help

            alertBox = new Canvas();
            var priceCol = Brushes.White;
            priceCol.Freeze();
            currentPrice = new TextBlock
            {
                Background = priceCol
            };
            symbolText = new TextBlock
            {
                Background = priceCol
            };
            var markerCol = Brushes.Gray;
            var markerCol1 = Brushes.White;
            markerCol.Freeze();
            markerCol1.Freeze();
            currentPriceMarker = DrawingObjects.CurrentPriceMarker(0, 0, markerCol1, markerCol);

            // overlay = new TextBlock
            // {
            //     FontSize = 100,
            //     Opacity = 0.3,
            // };


            for (int i=0; i<HORIZONTAL_GRID_LINES; i++)
            {
                //var line = new Line
                //{
                //    Opacity = 0.8,
                //    Stroke = Brushes.CornflowerBlue,
                //    StrokeDashArray = new DoubleCollection
                //    {
                //        1,
                //        2
                //    }
                //};
                //gridLines.Add(line);
                //canvas.Children.Add(line);

                gridLines.Add(new ScreenLine());
                currentCandleImage = new Image();
            }

            currentCandle = new Candle(this);

            drawingImage = new Image();
            drawingImage.MouseRightButtonUp += (object sender, System.Windows.Input.MouseButtonEventArgs e) =>
            {
                e.Handled = false;
                MouseUp();
            };
        }

        private void GreyBox_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            throw new NotImplementedException();
        }

        public override void Init(int timeFrame)
        {
            TimeFrame = timeFrame;

        }

        public static void OnBeforeSymbolHandlerChange()
        {
            if (items_chart != null)
            {
                items_chart.Clear();
            }

            scaleMult = 1;
        }

        #endregion

        #region Item added / updated

        //adds all the items to the data list. This will start at a midnight boundary
        //this does no aggregation or interpolation, the data is aggregated when required for display
        public override void ItemAdded(_SymbolDataItem item, bool canComputeTradeEntry, bool liveItem)//, bool redrawOnly)
        {
            lastPrice = item.Close;

            if (!(this == MainObj.visibleChart))
            {
                //block drawing of any chart except the visible one
                return;
            }

            if (items_chart != null)
            {
                items_chart.Clear(); //invalidate for drawing
            }

            if (TimeFrame != 0 && TradeEngine.results.Count % TimeFrame == 0)
            {
                ScrollBars++;
            }

            //reset current candle to item close value
            if (liveItem)
            {
                if (currentCandle != null)
                {
                    currentCandle.Reset(item, yOffset, this);
                }
                //UpdateCurrentPriceMarker(); - no, items_chart is null
            }
        }

        public override void ItemUpdated(_SymbolDataItem item, bool itemAdded = false)
        {
            if (currentCandle == null || items_chart == null || items_chart.Count == 0) return;

            if (symbolName == MainObj.CurrentSymbol)
            {
                if (MainObj.visibleChart == this)
                {
                        currentCandle.Update(item, false, yOffset, this, currentCandleImage);

                        //todo - move to drawing context, then use GetY_ForPriceNoOffset() instead
                        System.Windows.Application.Current.Dispatcher.Invoke(() => //must do this as this gets called by threads
                        {
                            //this won't work because the dc draws to a drawing image, which is the entire canvas
                            // - fine for whole canvas redraw
                            // - but this needs to be done as a separate drawing object, added to canvas once then updated live
                            //DrawingGroup drawingGroup = new DrawingGroup();
                            //using (DrawingContext dc = drawingGroup.Open())
                            //{
                            //    currentCandle.Draw(dc); //needs a separate drawing object for the single candle, to update it in real time
                            //}

                            UpdateCurrentPriceMarker();
                        });

                }
            }
        }

        #endregion

        #region Draw

        public override void InitDraw()
        {
      
        }
        //Draw method, this also makes available X, Y and Bar Width for other methods to draw directly to this canvas
        //no need to call this separately as it is always called before other Panels are drawn

        List<_Trade> visibleOrActiveOrders = new List<_Trade>(); 

        public override void Draw()
        {
            if (!canDraw)
            {
                return;
            }       

            if (_ChartLine.movingLine != null)
            {
                return;
            }  
            
            try
            {
                if (!(this == MainObj.visibleChart))
                {
                    //block drawing of any chart except the visible one
                    return;
                }

                List<_SymbolDataItem> currentCandleData = null;
                if (items_chart == null || items_chart.Count == 0)
                {
                    (items_chart, currentCandleData) = showHeikinAshi
                                ? TradeEngine.GetResultsHA_UI(0, Results.Count, TimeFrame)
                                : TradeEngine.GetResults_UI(0, Results.Count, TimeFrame);

                    currentCandle.Reset(currentCandleData);
                }

                canvas.Children.Clear();

                if (items_chart == null || items_chart.Count == 0)
                {
                    return;
                }

                FirstVisibleTime = Chart.ScrollBars >= items_chart.Count ? items_chart[items_chart.Count -1].Timestamp : items_chart[Chart.ScrollBars].Timestamp;
                var lastFrame = Chart.ScrollBars + Chart.ScaleBars;
                if (lastFrame > items_chart.Count - 1)
                {
                    lastFrame = items_chart.Count - 1;
                }
                LastVisibleTime = items_chart[lastFrame].Timestamp;

                DrawingGroup drawingGroup = new DrawingGroup();

                using (DrawingContext dc = drawingGroup.Open())
                {
                    //avoid running queries on the UI lists whilst TE could be accessing them. The objects therein may change during draw, but that shouldn't
                    // present any issues as draw does not update the objects
                    //ToList() does make a shallow copy to a new list.
                    _Trade activeOrder = null;

                    var leftEdgeTimestamp = FirstVisibleTime;
                    var rightEdgeTimestamp = LastVisibleTime.AddMinutes(TimeFrame*2); //or will scroll one extra frame before correctly rescaling

                    var maxVisible = FirstVisibleTime.AddMinutes(Chart.ScaleBars * TimeFrame);
                    if (rightEdgeTimestamp < maxVisible)
                    {
                        rightEdgeTimestamp = maxVisible;
                    }

                    visibleOrActiveOrders.Clear();
                    List<_ChartLine> visibleLines = null;
                    currentPrice.Background = WHITE;
                    liveTrailMarker = null;

                    #region Get all objects to draw
                    tradeEngine.GetUILists(null, () =>
                    {
                        //so this is a callback, to ensure thread safety - cannot read any of the UI lists if they are being updated
                        // - it also does not create a shallow copy of the entire lists, only a copy from a query  

                        visibleLines = tradeEngine.UI_Lines.Where(x =>
                                                                        (x.StartTimeStamp <= rightEdgeTimestamp && x.EndTimeStamp >= leftEdgeTimestamp) ||
                                                                        (x.StartTimeStamp >= leftEdgeTimestamp && x.EndTimeStamp <= rightEdgeTimestamp)
                                                                    ).ToList();

                        foreach (var l in visibleLines)
                        {
                            l.InitLine(false);
                        }

                        //need the trade set associated with any visible order (i.e. the order line is visible)
                        List<_Trade> orders = null;
                        if (showClosedTrades && showWaitingTrades)
                        {
                            orders = tradeEngine.UI_Trades.Where(x => x.Action == TradeAction.ORDER).ToList();
                        }
                        else if (!showClosedTrades && showWaitingTrades)
                        {
                            orders = tradeEngine.UI_Trades.Where(x => x.Action == TradeAction.ORDER && 
                                                                    x.GlobalStatus == TradeGlobalStatus.ACTIVE)
                                                                .ToList();
                        }
                        else if (showClosedTrades && !showWaitingTrades)
                        {
                            orders = tradeEngine.UI_Trades.Where(x => x.Action == TradeAction.ORDER &&
                                                                    x.GlobalStatus != TradeGlobalStatus.ACTIVE ||
                                                                    x.ItemStatus == TradeItemStatus.CLOSED)
                                                                .ToList();
                        }
                        else
                        {
                            //don't show closed or waiting
                            orders = tradeEngine.UI_Trades.Where(x => x.Action == TradeAction.ORDER &&
                                                                    x.GlobalStatus == TradeGlobalStatus.ACTIVE &&
                                                                    x.ItemStatus == TradeItemStatus.CLOSED)
                                                                .ToList();
                        }

                        //include any orders where the order and current time / close are either visible or bracket the visible area
                        foreach (var trade in orders)
                        {
                            bool canShow = false;

                            //if order is visible, show it obviously
                            if (trade.Timestamp <= rightEdgeTimestamp && trade.Timestamp >= leftEdgeTimestamp)
                            {
                                canShow = true;
                            }
                            else
                            {
                                //show it if order period brackets the visible area
                                if (trade.GlobalStatus == TradeGlobalStatus.ACTIVE)
                                {
                                    if (trade.Timestamp < rightEdgeTimestamp && DateTime.Now > leftEdgeTimestamp)
                                    {
                                        canShow = true;
                                    }
                                }
                                else
                                {
                                    var close = tradeEngine.UI_Trades.Where(x => x.TradeReference == trade.TradeReference && x.Action == TradeAction.CLOSE).FirstOrDefault();
                                    if (close != null)
                                    {
                                        if (trade.Timestamp < rightEdgeTimestamp && close.Timestamp >= leftEdgeTimestamp)
                                        {
                                            canShow = true;
                                        }
                                    }
                                }
                            }

                            if (canShow)
                            {
                                visibleOrActiveOrders.AddRange(tradeEngine.UI_Trades.Where(x => x.TradeReference == trade.TradeReference));
                            }
                        }

                        activeOrder = orders.FirstOrDefault(x =>
                                                            x.GlobalStatus == TradeGlobalStatus.ACTIVE &&
                                                            x.ItemStatus == TradeItemStatus.CLOSED);
                    }); 
                    #endregion

                    //the image for the drawing context is placed so the first non-empty pixel is at the top left. Therefore,
                    //since the trades may be occupying empty screen space above the candles, this will shift the candles up.
                    //Easier just to draw a line than shift the image down.
                    dc.DrawLine(new Pen(Brushes.White, 1), new Point(0, 0), new Point(1, 0));

                    #region Set range

                    var lastVisibleCandle = rightEdgeTimestamp.AddMinutes(-TimeFrame * 3); //or will scale to the next non-visible one
                    var _range = items_chart.Where(x => x.Timestamp > leftEdgeTimestamp && x.Timestamp <= lastVisibleCandle).Select(x => new { x.Low, x.High });
                    if (_range.Any())
                    {
                        High = _range.Max(x => x.High);
                        Low = _range.Min(x => x.Low);
                    }

                    //adjust the bounding range accordingly for trades
                    var high = visibleOrActiveOrders.Any() ? visibleOrActiveOrders.Max(x => x.Price) : double.MinValue;
                    var low = visibleOrActiveOrders.Any() ? visibleOrActiveOrders.Min(x => x.Price) : double.MaxValue;

                    High = Math.Max(High, high);
                    Low = Math.Min(Low, low);

                    //adjust range for lines. The only range of interest is where a line intersects the screen edges, or the line limits
                    foreach (var line in visibleLines)
                    {
                        var start = line.GetPriceAtTime(leftEdgeTimestamp, false);
                        var end = line.GetPriceAtTime(lastVisibleCandle, false);

                        High = Math.Max(High, start.price);
                        High = Math.Max(High, end.price);
                        Low = Math.Min(Low, start.price);
                        Low = Math.Min(Low, end.price);
                    }

                    SetScaling(); //this is the DisplayPanel method and just sets scaling to fit everything within screen height
                    

                    #endregion

                    #region Draw candles

                    Candle.SetCanvasDims(Scale, High, canvas.ActualWidth, canvas.ActualHeight, canvas.Margin.Top, canvas.Margin.Bottom, canvas.Margin.Right, canvas.Margin.Left);

                    //ha candles are calculated from the first visible raw candle on, and that candle is effectively discarded
                    // - so the ha list is one shorter than the raw list, which is adjusted by duplicating the first candl

                    //add candles, i.e. for zooming out
                    while (candles.Count < ScaleBars)
                    {
                        candles.Add(new Candle(this));
                    }

                    //remove unneeded candles, i.e. after zooming in
                    if (candles.Count > ScaleBars)
                    {
                        candles.RemoveRange(ScaleBars, candles.Count - ScaleBars);
                    }

                    int i = 0;
                    for (; i < ScaleBars; i++)
                    {
                        var index = i + ScrollBars;
                        if (index >= items_chart.Count)
                        {
                            break;
                        }

                        candles[i].Update(items_chart[index], items_chart[index].Interrupted, i, 0, this, dc);
                        candles[i].Draw(dc);
                    }


                    currentCandle.lastXPosition = i;
                    currentCandle.Update(tradeEngine.currentDataItem, false, yOffset, this, currentCandleImage);

                    #endregion

                    #region Add drawing image (before trades, these need to be on top)
                    //bug - scaling issue, see setting low and high re. take profit, above
                    drawingImage.Source = new DrawingImage(drawingGroup);
                    canvas.Background = TradeEngine.USE_SIMULATED_EXCHANGE
                                            ? MainObj.invertChart ? Brushes.PaleGoldenrod : Brushes.AliceBlue
                                            : MainObj.invertChart ? Brushes.PaleGoldenrod : Brushes.White;
                    canvas.Background.Freeze();
                    Canvas.SetTop(drawingImage, MainObj.invertChart ? -YOffset : YOffset);
                    canvas.Children.Add(drawingImage);
                    canvas.Children.Add(currentCandleImage);
                    #endregion

                    double runningPriceDiff = 0;
                    #region Draw trades and lines

                    //but not in inverted chart mode as that is confusing and potentially disasterous, so orders also cannot be placed in that mode
                    if (!MainObj.invertChart && items_chart.Any())
                    {
                        SolidColorBrush outline = OUTLINE_COLOR;
                        SolidColorBrush textColor = OUTLINE_COLOR;
                        SolidColorBrush color = STOPPED_COLOR;

                        Canvas canvas1 = null;

                        //not showing entries as markers, only lines
                        //get lists of active elements, for drawing of break lines etc
                        var orderGroups = visibleOrActiveOrders.GroupBy(x => x.TradeReference);//get a list of all visible trades now, rather than running a new query against the db for all visible times

                        foreach (var tradeGroup in orderGroups)
                        {
                            var order = tradeGroup.FirstOrDefault(x => x.Action == TradeAction.ORDER);
                            var entry = tradeGroup.FirstOrDefault(x => x.Action == TradeAction.ENTRY);
                            var stop = tradeGroup.FirstOrDefault(x => x.Action == TradeAction.STOP);
                            var takeProfit = tradeGroup.FirstOrDefault(x => x.Action == TradeAction.TAKE_PROFIT);
                            var ceiling = tradeGroup.FirstOrDefault(x => x.Action == TradeAction.CEILING);
                            var floor = tradeGroup.FirstOrDefault(x => x.Action == TradeAction.FLOOR);
                            var close = tradeGroup.FirstOrDefault(x => x.Action == TradeAction.CLOSE);
                            var breakEven = tradeGroup.FirstOrDefault(x => x.Action == TradeAction.BREAK_EVEN);

                            bool exchangeSynched = entry == null || entry.ExchangeMethodScript == null;

                            if (order != null)
                            {
                                #region Get colors
                                SolidColorBrush _stoppedColor = order == null
                                                                ? STOPPED_COLOR
                                                                : order.Result < 0
                                                                    ? LOSS_COLOR :
                                                                    order.Result > 0
                                                                        ? PROFIT_COLOR
                                                                        : STOPPED_COLOR;
                                var stoppedColor = exchangeSynched
                                    ? _stoppedColor
                                    : EXCHANGE_NOT_SYNCHED_COLOR;

                                var waitingColor = exchangeSynched
                                    ? order.Position == TradePosition.LONG ? WAITING_COLOR_LONG : WAITING_COLOR_SHORT
                                    : EXCHANGE_NOT_SYNCHED_COLOR;

                                bool orderEntered = order.ItemStatus == TradeItemStatus.CLOSED && order.GlobalStatus == TradeGlobalStatus.ACTIVE;

                                var breakEvenColor = exchangeSynched ? BREAK_EVEN_COLOR : EXCHANGE_NOT_SYNCHED_COLOR;
                                var runningColor = exchangeSynched ? Brushes.Blue : EXCHANGE_NOT_SYNCHED_COLOR;
                                var stopColorActive = exchangeSynched ? STOP_COLOR_ACTIVE : EXCHANGE_NOT_SYNCHED_COLOR;
                                var tpColorActive = exchangeSynched ? TAKE_PROFIT_COLOR_ACTIVE : EXCHANGE_NOT_SYNCHED_COLOR;

                                color = order.GlobalStatus == TradeGlobalStatus.STOPPED
                                    //? order.ExchangeStatus == TradeExchangeStatus.NEVER_ENTERED
                                    //    ? CANCELLED_COLOR
                                    //    : stoppedColor
                                    ? stoppedColor
                                    : order.ItemStatus == TradeItemStatus.CLOSED
                                        ? runningColor
                                        : waitingColor;

                                outline = OUTLINE_COLOR;
                                textColor = OUTLINE_COLOR;

                                #endregion

                                #region Get the order price at the current time, for orders which are not entered yet
                                var orderPriceNow = order.Price;
                                if (!orderEntered)
                                {
                                    var orderLine = visibleLines.FirstOrDefault(x => x.TradeId == order.Id);
                                    if (orderLine != null)
                                    {
                                        (orderPriceNow, _) = orderLine.GetPriceAtTime(DateTime.UtcNow, true);
                                    }
                                }
                                else
                                {
                                    orderPriceNow = entry.Price;
                                    if (tradeEngine.currentDataItem != null)
                                    {
                                        runningPriceDiff = -MainObj.GetPriceDiff(orderPriceNow, tradeEngine.currentDataItem.Close, order.Position) * 100;
                                    }
                                }

                                #endregion

                                #region Draw trade markers

                                //only draw floor and ceiling markers if trade is running
                                //stops will need to be drawn regardless because of the line association
                                //marker positions are at Order if unentered, at current time if running (stop line is extended in TradeEngine.RunTrade_Synchronised), Close if closed

                                foreach (var trade in tradeGroup)
                                {
                                    canvas1 = null;

                                    //will always draw trades lined up, although the stops will actually track along the support lines
                                    double left = 0;
                                    double top = 0;

                                    //must round timestamp down to time interval
                                    var timeStamp = entry == null ? order.Timestamp : trade.Timestamp;

                                    (left, top) = GetDrawPosition1(timeStamp, trade.Price);



                                    switch (trade.Action)
                                    {
                                        //when it is runninng (Global Status OPEN) will be inactive until Item Status is CLOSED, i.e. trade has entered, this will be for a backtesting trade
                                        case TradeAction.ORDER:

                                            //(left, top) = GetDrawPosition1(timeStamp, trade.Price);

                                            //for clarity, don't show the order if the trade has entered and closed, just show the entry and close markers
                                            //but if the trade has no close marker, then must show it or will never see it again
                                            //so trade is either pending, active, or closed with entry, otherwise don't show
                                            if (trade.ItemStatus == TradeItemStatus.OPEN || trade.GlobalStatus == TradeGlobalStatus.ACTIVE || entry == null)
                                            {
                                                string extraText = string.Empty;

                                                //this is a locally placed order if it is running with a zero action value. Otherwise it was placed on exchange.
                                                var p = trade.Position == TradePosition.LONG ? "LONG" : "SHORT";
                                                var t = trade.ItemStatus == TradeItemStatus.CLOSED && trade.ActionValue == 0 ? $"LOCALLY PLACED {p} ORDER" : $"{p} ORDER";
                                                switch (trade.Type)
                                                {
                                                    case TradeType.HMA_BREAK_ORDER:
                                                        extraText = trade.GlobalStatus == TradeGlobalStatus.ACTIVE ? $"{t}: HMA break" : " stopped";
                                                        break;
                                                    case TradeType.LINE_BREAK_ORDER:
                                                        extraText = trade.GlobalStatus == TradeGlobalStatus.ACTIVE ? $"{t}: line break" : " stopped";
                                                        break;
                                                    case TradeType.PRICE_BREAK_ORDER:
                                                        //trade may be closed before or after it has entered, so ItemStatus can be open or closed when trade stopped
                                                        extraText = trade.GlobalStatus == TradeGlobalStatus.ACTIVE ? $"{t}: price break" : " stopped";
                                                        break;
                                                    case TradeType.MARKET_ORDER:
                                                        //trade may be closed before or after it has entered, so ItemStatus can be open or closed when trade stopped
                                                        extraText = trade.GlobalStatus == TradeGlobalStatus.ACTIVE ? $"{t}: market" : " stopped";
                                                        break;
                                                    case TradeType.AUTO_ORDER:
                                                        extraText = trade.GlobalStatus == TradeGlobalStatus.ACTIVE ? $"{t}: auto" : " stopped";
                                                        break;
                                                }
                                                var s = trade.TimeFrame > 1 ? "s" : "";
                                                extraText += $" ({trade.TimeFrame}min{s})";
                                                canvas1 = TradeOrderMarker(trade.Position, left, top, color, outline, textColor, extraText).canvas;
                                            }
                                            break;

                                        case TradeAction.ENTRY:

                                            //(left, top) = GetDrawPosition1(timeStamp, trade.Price);

                                            //Entry and Close will kill the order locally if they timeout
                                            //so if trade is closed but still has script name, can consider this to be an interrupted trade
                                            string __text = exchangeSynched
                                                ? string.Empty
                                                : trade.GlobalStatus == TradeGlobalStatus.ACTIVE
                                                        ? string.Empty
                                                        : "INTERRUPTED";
                                            canvas1 = TradeEntryMarker(left, top, color, outline, Brushes.Black, __text).canvas;
                                            break;

                                        case TradeAction.STOP:

                                            textColor = outline;

                                            string text = string.Empty;
                                            if (trade.GlobalStatus == TradeGlobalStatus.ACTIVE)
                                            {
                                                text = "stop ";

                                                var stLine = visibleLines.FirstOrDefault(x => x.TradeId == trade.Id);
                                                if (stLine != null && tradeEngine.currentDataItem != null)
                                                {
                                                    (var currLinePrice, _) = stLine.GetPriceAtTime(DateTime.UtcNow, true);

                                                    var stopPricePC = -MainObj.GetPriceDiff(orderPriceNow, currLinePrice, order.Position) * 100;

                                                    if (stopPricePC > 0)
                                                    {
                                                        text += "+";
                                                    }
                                                    text += $"{stopPricePC.ToString("0.0")}%";
                                                    text += orderEntered ? " from entry" : " from order";
                                                }

                                                switch (trade.Stop)
                                                {
                                                    case TradeStop.STOP_TRAIL:
                                                        text += "trail";
                                                        break;
                                                    case TradeStop.STOP_TRAIL_SPIKE:
                                                        text += "spike";
                                                        break;
                                                    case TradeStop.STOP_ON_LINE_BREAK:
                                                        break;
                                                    default:
                                                        text += "unspecified";
                                                        break;
                                                }
                                            }

                                            liveTrailMarker = TradeStopMarker(left, top, color, outline, textColor, text);
                                            canvas1 = liveTrailMarker.canvas;
                                            break;

                                        case TradeAction.TAKE_PROFIT:

                                            text = "take profit ";

                                            var tpLine = visibleLines.FirstOrDefault(x => x.TradeId == trade.Id);
                                            if (tpLine != null && tradeEngine.currentDataItem != null)
                                            {
                                                double tpPricePC = 0;
                                                (var currLinePrice, _) = tpLine.GetPriceAtTime(DateTime.UtcNow, true);
                                                tpPricePC = -MainObj.GetPriceDiff(orderPriceNow, currLinePrice, order.Position) * 100;

                                                if (tpPricePC > 0)
                                                {
                                                    text += "+";
                                                }

                                                text += $"{tpPricePC.ToString("0.0")}%";
                                                text += orderEntered ? " from entry" : " from order";
                                            }

                                            canvas1 = TradeTakeProfitMarker(left, top, color, outline, textColor, trade.Position, trade.GlobalStatus == TradeGlobalStatus.ACTIVE ? text : " stopped").canvas;
                                            break;

                                        case TradeAction.CEILING:

                                            text = "ceiling ";

                                            if (tradeEngine.currentDataItem != null)
                                            {
                                                var ceilingPricePC = -MainObj.GetPriceDiff(orderPriceNow, trade.Price, order.Position) * 100;

                                                if (ceilingPricePC > 0)
                                                {
                                                    text += "+";
                                                }
                                                text += $"{ceilingPricePC.ToString("0.0")}%";
                                                text += orderEntered ? " from entry" : " from order";
                                            }

                                            canvas1 = TradeTakeProfitMarker(left, top, color, outline, textColor, trade.Position, trade.GlobalStatus == TradeGlobalStatus.ACTIVE ? text : " stopped").canvas;
                                            break;

                                        case TradeAction.BREAK_EVEN:

                                            text = "break even ";

                                            if (tradeEngine.currentDataItem != null)
                                            {
                                                var breakEvenPrice = -MainObj.GetPriceDiff(orderPriceNow, trade.Price, order.Position) * 100;

                                                if (breakEvenPrice > 0)
                                                {
                                                    text += "+";
                                                }
                                                text += $"{breakEvenPrice.ToString("0.0")}%";
                                                text += orderEntered ? " from entry" : " from order";
                                            }

                                            canvas1 = TradeBreakEvenMarker(left, top, color, outline, textColor, trade.Position, trade.GlobalStatus == TradeGlobalStatus.ACTIVE ? text : " stopped").canvas;
                                            break;

                                        case TradeAction.FLOOR:

                                            text = "floor ";

                                            if (tradeEngine.currentDataItem != null)
                                            {
                                                var floorPricePC = -MainObj.GetPriceDiff(orderPriceNow, trade.Price, order.Position) * 100;

                                                if (floorPricePC > 0)
                                                {
                                                    text += "+";
                                                }
                                                text += $"{floorPricePC.ToString("0.0")}%";
                                                text += orderEntered ? " from entry" : " from order";
                                            }
                                            //if (order.Id == 5045)
                                            //{
                                            canvas1 = TradeStopMarker(left, top, color, outline, textColor, trade.GlobalStatus == TradeGlobalStatus.ACTIVE ? text : " stopped").canvas;
                                            //}
                                            break;

                                        case TradeAction.CLOSE:
                                            canvas1 = TradeExitMarker(left, top, color, stoppedColor, outline, trade.Result.ToString("0.0000") + "%").canvas;
                                            break;
                                    }

                                    if (canvas1 != null)
                                    {
                                        canvas1.Name = "_" + trade.Id.ToString(); //cannot start with a number


                                        if (trade.Action == TradeAction.ORDER || trade.Action == TradeAction.ENTRY)
                                        {
                                            canvas1.MouseRightButtonUp += (object sender, System.Windows.Input.MouseButtonEventArgs e) =>
                                            {
                                                MainObj.mainViewModel.PopupMenuForOrder(sender, e);
                                                e.Handled = true;
                                            };
                                        }
                                        else
                                        {
                                            canvas1.MouseRightButtonUp += (object sender, System.Windows.Input.MouseButtonEventArgs e) =>
                                            {
                                                MainObj.mainViewModel.PopupMenuForTrade(sender, e);
                                                e.Handled = true;
                                            };
                                        }

                                        canvas1.MouseDown += (object sender, System.Windows.Input.MouseButtonEventArgs e) =>
                                        {
                                            tradeEngine.EditTradeSet_UI(trade);
                                            e.Handled = true;
                                        };

                                        canvas1.MouseEnter += (object sender, System.Windows.Input.MouseEventArgs e) =>
                                        {
                                            if (order.Result == 0 || trade.Action == TradeAction.STOP || trade.Action == TradeAction.FLOOR || trade.Action == TradeAction.CEILING || trade.Action == TradeAction.BREAK_EVEN)
                                            {
                                                CurrentTrade = trade;
                                                var _canvas = sender as Canvas;
                                                if (_canvas != null)
                                                {
                                                    _canvas.Cursor = System.Windows.Input.Cursors.Pen;
                                                }
                                                e.Handled = true;
                                            }
                                        };
                                        canvas1.MouseLeave += (object sender, System.Windows.Input.MouseEventArgs e) =>
                                        {
                                            CurrentTrade = null;
                                            e.Handled = true;
                                        };

                                        //this does not fix the sometimes unresponsive popup menu issue, something is getting keydown focus first
                                        //canvas1.KeyDown += (object _sender, System.Windows.Input.KeyEventArgs _e) => { MainObj.mainViewModel.KeyDown(_e.Key); };
                                        //canvas1.KeyUp += (object _sender, System.Windows.Input.KeyEventArgs _e) => { MainObj.mainViewModel.KeyUp(_e.Key); };
                                        canvas.Children.Add(canvas1);
                                    }
                                }


                                #endregion
                            }

                            #region Draw active entry, take profit and stop lines

                            double right = canvas.ActualWidth;

                            //get the furthest extent of the order line
                            if (order != null)
                            {
                                var line = visibleLines.FirstOrDefault(x => x.TradeId == order.Id);
                                if (line != null)
                                {
                                    (right, _) = GetDrawPosition1(line.EndTimeStamp, line.EndPrice);
                                    line.InitLine(order.ItemStatus == TradeItemStatus.CLOSED && order.GlobalStatus == TradeGlobalStatus.ACTIVE);
                                }
                            }

                            _ChartLine stopLine = stop == null ? null : visibleLines.FirstOrDefault(x => x.TradeId == stop.Id);

                            //this will draw all trades at the Order timestamp, not the trade timestamp - 
                            // so stops, which will track with time, will always show lined up with the other orders
                            void DrawStopLines(_Trade _trade, SolidColorBrush _color, double _right, int _thickness = 1, bool dotted = false)
                            {
                                if (_trade == null || !canDraw)
                                {
                                    return;
                                }

                                (double left, double top) = GetDrawPosition1(order.Timestamp, _trade.Price, false);
                                //will cause screen image to shrink if drawing starts before left edge, which offsets candles
                                //if (top > 0 && top < canvas.ActualHeight)
                                {
                                    left = left < 0 ? 0 : left;
                                    right = right >= canvas.ActualWidth ? canvas.ActualWidth : right;
                                    var pen = new Pen(_color, _thickness);
                                    pen.DashStyle = dotted ? DashStyles.Dot : DashStyles.Solid;
                                    dc.DrawLine(pen, new Point(left, top), new Point(_right, top));
                                }
                            }

                            //get the furthest extent to draw horizontal lines - either the end of the order line if not entered, or the current item position
                            if (entry != null && entry.GlobalStatus == TradeGlobalStatus.ACTIVE && tradeEngine.currentDataItem != null)
                            {
                                (right, _) = GetDrawPosition1(DateTime.UtcNow, tradeEngine.currentDataItem.Close);
                            }
                            if (close != null)
                            {
                                (right, _) = GetDrawPosition1(close.Timestamp, close.Price);
                            }
                            //draw horizontal lines
                            if (entry?.GlobalStatus == TradeGlobalStatus.ACTIVE)
                            {
                                DrawStopLines(entry, RUNNING_COLOR, right);
                            }
                            if (close == null)
                            {
                                //DrawStopLines(stop, STOP_COLOR_ACTIVE, right);
                                //if (!hideTrades) //this has beaten me - it just offsets the chart when not fully clipping to the screen
                                {
                                    DrawStopLines(ceiling, TAKE_PROFIT_COLOR_ACTIVE, right);
                                    DrawStopLines(floor, STOP_COLOR_ACTIVE, right);
                                    DrawStopLines(breakEven, BREAKEVEN_COLOR_ACTIVE, right, 1, true);
                                }

                                //draw connecting vertical line
                                var topPrice = tradeGroup.Max(x => x.Price);
                                var botPrice = tradeGroup.Min(x => x.Price);

                                if (stopLine != null)
                                {
                                    (var price, _) = stopLine.GetPriceAtTime(order.Timestamp, true);
                                    if (order.Position == TradePosition.LONG)
                                    {
                                        botPrice = Math.Min(botPrice, price);
                                    }
                                    else
                                    {
                                        topPrice = Math.Max(topPrice, price);
                                    }
                                }

                                (double left1, double top1) = GetDrawPosition1(order.Timestamp, topPrice, false);

                                if (left1 >= 0 && left1 < canvas.ActualWidth)
                                {
                                    (double _, double bottom) = GetDrawPosition1(order.Timestamp, botPrice, false);

                                    top1 = top1 < 0 ? 0 : top1;
                                    bottom = bottom > canvas.ActualHeight ? canvas.ActualHeight : bottom;

                                    Pen p = new Pen(Brushes.Black, 4);
                                    p.DashStyle = new DashStyle(new DoubleCollection { 1, 2 }, 0);
                                    if (order.ItemStatus == TradeItemStatus.CLOSED && order.GlobalStatus == TradeGlobalStatus.ACTIVE)
                                    {
                                        p.Brush = Brushes.Orange;// tradeEngine.currentTradeSetReference == null || tradeEngine.currentTradeSetReference == order.TradeReference 
                                                                 // ? Brushes.Orange : Brushes.Blue;
                                    }
                                    else
                                    {
                                        p.Brush = tradeEngine.currentTradeSetReference == null || tradeEngine.currentTradeSetReference == order.TradeReference
                                            ? Brushes.Black : Brushes.LightGray;
                                    }

                                    dc.DrawLine(p, new Point(left1, top1), new Point(left1, bottom));
                                }

                            }
                            #endregion

                        }
                    }

                    foreach (var line in visibleLines)
                    {
                        line.Draw_UI(this);
                    }

                    #endregion

                    //technically, updating the current price marker should update the active stop, tp, floor and ceiling percentages
                    //however, this will happen on the minute anyway
                    UpdateCurrentPriceMarker();

                    #region Draw overlay text
                    void SetOverlay(string text)
                    {
                        //var textTop = 0;// (canvas.ActualHeight - overlay.ActualHeight) / 2;

                        //FormattedText ft = new FormattedText(
                        //    text,
                        //    CultureInfo.GetCultureInfo("en-us"),
                        //    FlowDirection.LeftToRight,
                        //    new Typeface("Verdana"),
                        //    20,
                        //    Brushes.Black);

                        //ft.MaxTextWidth = 800;
                        //ft.MaxTextHeight = 200;
                        //var textLeft = (int)(canvas.ActualWidth - ft.Width - 20);
                        //dc.DrawText(ft, new Point(textLeft, textTop));

                        symbolText.Text = text;
                        symbolText.FontSize = 20;
                        symbolText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        symbolText.Arrange(new Rect(currentPrice.DesiredSize));
                        var textLeft = (int)(canvas.ActualWidth - symbolText.ActualWidth - 20);
                        var textTop = 0;
                        Canvas.SetLeft(symbolText, textLeft);
                        Canvas.SetTop(symbolText, textTop);
                    }

                    var hideWaiting = Chart.showWaitingTrades == false ? "\n(waiting trades hidden)" : string.Empty;
                    var hideClosed = Chart.showClosedTrades == false ? "\n(closed trades hidden)" : string.Empty;
                    var sn = symbolName + (MainObj.invertChart ? " INVERTED" : "");
                    if (activeOrder != null)
                    {
                        if (activeOrder.ItemStatus == TradeItemStatus.CLOSED)
                        {
                            var system = TradeEngine.USE_SIMULATED_EXCHANGE == false ? "On exchange" : "Exchange simulated";
                            var tradeType = activeOrder.ExchangeStatus == TradeExchangeStatus.ENTERED_ON_EXCHANGE ? "LIVE TRADE" : "test trade";

                            string pc = $"{runningPriceDiff.ToString("0.0")}%";
                            if (runningPriceDiff > 0)
                            {
                                pc = "+" + pc;
                            }

                            SetOverlay($"{pc} {sn} {system} {tradeType}{hideWaiting}{hideClosed}");
                        }
                        else
                        {
                            SetOverlay($"{sn}                        (trade waiting){hideWaiting}{hideClosed}");
                        }
                    }
                    else
                    {
                        SetOverlay($"{sn}                        {hideWaiting}{hideClosed}");
                    }

                    #endregion
                 
                    #region Add canvases
                    canvas.Children.Add(currentPriceMarker.canvas);
                    //canvas.Children.Add(currentCandleImage);
                    canvas.Children.Add(alertBox);
                    canvas.Children.Add(priceMarkerLine);
                    canvas.Children.Add(currentPrice);
                    canvas.Children.Add(symbolText);
                    canvas.Children.Add(greyBox);
                    #endregion
                }
            }
#pragma warning disable CS0168
            catch (Exception e)
            {

            }

        }

        void UpdateCurrentPriceMarker()
        {
            //todo - move to drawing context, then use GetY_ForPriceNoOffset() instead
            if (tradeEngine.currentDataItem != null)
            {
                currentPriceMarker.Update(GetX(currentCandle.lastXPosition + 1), (int)GetY_ForPrice(tradeEngine.currentDataItem.Close));
            }         
        }

        public void RemoveTradePercentage()
        {
            //remove trade percentage text, doing it this way to keep the drawing code separate from the execution code
            if (tradePercentage != null && canvas.Children.Contains(tradePercentage))
            {
                canvas.Children.Remove(tradePercentage);
                tradePercentage = null;
            }
        }

#endregion

        #region Mouse
        public void MouseUp()
        {
            //finish line here
            if (currentLine != null)
            {
                currentLine.MouseUp_UI_synchronised();
            }

            ShowBox = false;
            currentLine = null;

            //hideTrades = false;

        }
        //because the crosshair lines will capture mouse events once they show, we need to route the mouse down event directly to the chart to show the overlay box
        public void MouseDown(bool shiftDown, bool ctrlDown)
        {
            MouseMove(mouseX, mouseY, true, shiftDown, ctrlDown);
        }


        public void MouseMove(double mouseX, double mouseY, bool mouseDown, bool shiftDown, bool ctrlDown)
        {
            if (items_chart == null || !items_chart.Any())
            {
                return;
            }
            Canvas.SetTop(currentPrice, mouseY);

            // - this caused text to imtermittently jump to top left corner, as if size was the whole screen??
            // - and did nothing anyway
            //currentPrice.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            //currentPrice.Arrange(new Rect(currentPrice.DesiredSize));

            var textLeft = (int)(canvas.ActualWidth - currentPrice.ActualWidth - 10);
            Canvas.SetLeft(currentPrice, textLeft);

            bool drawRequired = false;

            //handle move of a line
            if (_ChartLine.movingLine != null)
            {
                currentLine?.MoveLine_UI(this.mouseX, mouseX, this.mouseY, mouseY, shiftDown, ctrlDown, this);
            }
            else
            {
                //handle mouse down for drag of chart
                if (mouseDown)
                {
                    if (!shiftDown)
                    {
                        if (ctrlDown)
                        {
                            //draw line
                            if (currentLine == null)
                            {
                                currentLine = new _ChartLine
                                {
                                    SymbolName = symbolName
                                };
                                (currentLine.StartTimeStamp, currentLine.StartPrice) = GetTimeAndPriceFromLastMouse();
                            }

                            (currentLine.EndTimeStamp, currentLine.EndPrice) = GetTimeAndPriceFromLastMouse();
                            //currentLine.SetMinimumLength(TimeFrame);
                            currentLine.InitLine(false);
                            currentLine.Draw_UI(this); //if no chart scroll, must draw the line here
                        }
                        else
                        {
                            //drag chart
                            double diffX = this.mouseX - mouseX;
                            double diffY = this.mouseY - mouseY;
                            double amt = diffX / CandleWidth;

                            if (diffX != 0 || diffY != 0)
                            {
                                ShowBox = false;
                                oldScrollbars += amt;
                                if (Math.Abs(oldScrollbars) > 1)
                                {
                                    amt = (int)oldScrollbars;
                                    //if scrolling left, don't want to scroll all bars off screen (and continue to move them further to the left)
                                    if (amt < 0 || items_chart.Count > 2)
                                    {
                                        ScrollBars += (int)amt;
                                        if (ScrollBars < 0)
                                        {
                                            ScrollBars = 0;
                                            oldScrollbars = 0;
                                        }
                                    }
                                    oldScrollbars -= (int)oldScrollbars;
                                }

                                //limit scroll to always fill 80% of screen, pointless hiding everything
                                int w = (int)(canvas.ActualWidth / CandleWidth * 0.2);
                                if (ScrollBars > Results.Count - w)
                                {
                                    ScrollBars = Results.Count - w;
                                }
                                yOffset -= (int)diffY;
                                drawRequired = true;
                            }
                        }
                    }
                    else
                    {
                        if (!ShowBox)
                        {
                            //mouse down, set start point of overlay box since left shift is also down
                            boxStartPoint.X = mouseX;
                            boxStartPoint.Y = mouseY;
                            ShowBox = true;
                        }
                    }
                }
            }

            //offset the lines by 1 pixel to avoid capture of mouse events
            //setting Handled=false does not seem to help, in the mouse down event handler
            if (!showBox)
            {
                currentTimeMarkerLine.X1 = mouseX-1;
                currentTimeMarkerLine.X2 = mouseX-1;
                currentTimeMarkerLine.Y2 = panelsContainer.ActualHeight;

                try
                {
                    //allow for current candle when showing bar time
                    int barNum = (int) (mouseX / MainObj.chart_Canvas.ActualWidth * ScaleBars) + ScrollBars;
                    int frameNum = GetFrameNumFromMouseX(mouseX);
                    if (barNum > -1)
                    {
                        var numExtraMins = barNum - items_chart.Count + 1;
                        var dateTime = barNum < items_chart.Count ? items_chart[barNum].Timestamp : items_chart.Last().Timestamp.AddMinutes(TimeFrame * numExtraMins);

                        //line up with BST, by offsetting into the data by 1 hour when necessary
                        //see TradeEngine.START_HOUR property for full explanation
                        var offset = TimeZoneInfo.Local.GetAdjustmentRules()[0].DaylightDelta;
                        bool v = TimeZoneInfo.Local.IsDaylightSavingTime(dateTime);
                        var t = v ? dateTime.AddTicks(offset.Ticks) : dateTime;

                        SetBarTimestampText(mouseX, $"{frameNum}-{t:dd/MM/yy HH:mm}");
                    }
                }
                catch { }


                if (greyBox != null)
                {
                    greyBox.Visibility = Visibility.Hidden;
                }

                priceMarkerLine.X1 = 0;
                priceMarkerLine.Y1 = mouseY-1;
                priceMarkerLine.X2 = canvas.ActualWidth;
                priceMarkerLine.Y2 = mouseY-1;

                if (items_chart.Count > 0)
                {
                    currentPrice.Text = PriceFormatter.Format(GetTradePriceFromScreen(mouseY));
                }
            }
            else
            {
                try
                {
                    double width = mouseX - boxStartPoint.X;
                    double height = mouseY - boxStartPoint.Y;
                    double x = width > 0 ? boxStartPoint.X : mouseX;
                    double y = height > 0 ? boxStartPoint.Y : mouseY;
                    greyBox.Height = Math.Abs(height);
                    greyBox.Width = Math.Abs(width);
                    greyBox.Visibility = Visibility.Visible;
                    Canvas.SetLeft(greyBox, x);
                    Canvas.SetTop(greyBox, y);

                    double currPrice = GetTradePriceFromScreen(mouseY);
                    double oldPrice = GetTradePriceFromScreen(boxStartPoint.Y);
                    var pc = -MainObj.GetPriceDiff(oldPrice, currPrice, TradePosition.LONG);
                    currentPrice.Text = (pc * 100).ToString("0.00") + "%";
                    int bars = (int)Math.Abs((boxStartPoint.X - mouseX) / CandleWidth);
                    int hours = bars * TimeFrame / 60;
                    int mins = bars * TimeFrame - hours * 60;
                    if (hours == 0 && mins > 0)
                    {
                        SetBarTimestampText(mouseX, string.Format("{1} min{2}", hours, mins, mins > 1 ? "s" : "", hours > 1 ? "s" : ""));
                    }
                    else if (hours > 0 && mins == 0)
                    {
                        SetBarTimestampText(mouseX, string.Format("{0} hour{3}", hours, mins, mins > 1 ? "s" : "", hours > 1 ? "s" : ""));
                    }
                    else if (hours > 0 && mins > 0)
                    {
                        SetBarTimestampText(mouseX, string.Format("{0} hour{3} {1} min{2}", hours, mins, mins > 1 ? "s" : "", hours > 1 ? "s" : ""));
                    }
                }
                catch { }
            }

            this.mouseX = mouseX;
            this.mouseY = mouseY;

            if (drawRequired)
            {
                MainObj.DrawImmediate();
            }
        }

        internal List<_Trade> GetAllTrades()
        {
            List<_Trade> trades = null;
            tradeEngine.GetUILists(null, () =>
            {
                trades = tradeEngine.UI_Trades.ToList(); //get a shallow copy
            });
            return trades;
        }

        //called from the UI to move between trades
        internal void ScrollToNextVisibleTrade(bool forward)
        {
            var allTrades = GetAllTrades();

            var firstVisibleTime = LastVisibleTime.AddMinutes(-ScaleBars * TimeFrame);
            var visibleOrders = allTrades.Where(x =>
                                                            x.Action == TradeAction.ORDER &&
                                                            x.Timestamp > firstVisibleTime &&
                                                            x.Timestamp <= LastVisibleTime
                                                            ).OrderBy(x => x.TimeFrame)
                                                            ;

            var allOrders = allTrades.Where(x => x.Action == TradeAction.ORDER).OrderBy(x => x.TimeFrame);

            _Trade order = null;
            if (!visibleOrders.Any()) //nothing showing
            {
                order = forward
                    ? allOrders.Where(x => x.Timestamp >= LastVisibleTime).FirstOrDefault()
                    : allOrders.Where(x => x.Timestamp < LastVisibleTime).FirstOrDefault();
            }
            else
            {
                if (forward) //go one forward from the last one showing
                {
                    var firstOrder = visibleOrders.OrderBy(x => x.Timestamp).First();
                    order = allOrders.Where(x =>x.Timestamp >firstOrder.Timestamp).FirstOrDefault();
                }
                else //etc
                {
                    var lastOrder = visibleOrders.OrderBy(x => x.Timestamp).First();
                    order = allOrders.Where(x => x.Timestamp <lastOrder.Timestamp).FirstOrDefault();
                }
            }
            if (order != null)
            {
                var frame = GetFrameForTimestamp(order.Timestamp);
                ScrollBars = frame / MainObj.TimeFrame - 10; //just an arbitrary number of bars offset from edge
                yOffset = 0;
            }
        }

       
#endregion

        #region Alert text
        /*
        public void SetAlertText()
        {
            if (!canDraw)
            {
                return;
            }
            string text = tradeEngine.activeOrder == null 
                ? string.Empty
                : tradeEngine.activeOrder.ItemStatus == TradeItemStatus.CLOSED 
                    ? "Live trade running" 
                    : "Trade waiting";

            if (MainObj.visibleChart == this)
            {
                SolidColorBrush s = new SolidColorBrush();

                TextBlock textBlock = new TextBlock()
                {
                    Text = text,
                    FontSize = 12,
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                };

                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                textBlock.Arrange(new Rect(textBlock.DesiredSize));

                alertBox.Children.Clear();
                alertBox.Children.Add(textBlock);
            }
        }

        void SetAlertPosition()
        {
            Canvas.SetTop(alertBox, 20);
            Canvas.SetLeft(alertBox, 20);
        }
        */
#endregion

        #region Bar timestamp

        public void SetBarTimestampText(double mouseMoveX, string text)
        {
            TextBlock tb = new TextBlock()
            {
                Text = text,
            };

            timeBarChildCanvas.Children.Clear();
            timeBarChildCanvas.Children.Add(tb);
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            tb.Arrange(new Rect(tb.DesiredSize));
            var w = tb.ActualWidth;
            double pos = mouseMoveX - w / 2;
            if (pos < 0)
            {
                pos = 0;
            }
            if (pos + w > canvas.ActualWidth)
            {
                pos = canvas.ActualWidth - w;
            }
            Canvas.SetLeft(timeBarChildCanvas, pos);
        }

#endregion

        #region Scroll and zoom

        internal void Magnify(bool v)
        {
            //the bar number here is whatever it would be under the mouse, regardless of whether there is a real bar there (or whether mouse is past last bar)
            int barNum = (int)(mouseX / MainObj.chart_Canvas.ActualWidth * ScaleBars + ScrollBars);
            var scale = mouseX / canvas.ActualWidth;
            if (v)
            {
                //zoom in
                ScaleBars = (int)(ScaleBars / 1.3);

            }
            else
            {
                //zoom out
                ScaleBars = (int)(ScaleBars * 1.2);

                //something like this could work to adjust timeframe when zooming... but this does not
                //if (ScaleBars > 1000)
                //{
                //    ScaleBars /= 2;
                //    var oldTimeframe = TimeFrame;
                //    TimeFrame *= 2;
                //    OnTimeframeChange(oldTimeframe, TimeFrame);
                //    items_chart.Clear();
                //}
            }

            ScrollBars = barNum - (int)(ScaleBars * scale);
            MainObj.DrawImmediate();
        }

        internal void Enlarge(bool v)
        {
            //this needs to be worked out. It affects trade drawing, which is from bottom of screen, and candles, which are from top, and order price when placing order
            //scaleMult *= v ? 1.1 : 0.9;
            //MainObj.DrawImmediate();
        }

        internal void ScrollToEnd()
        {
            if (MainObj.IsBacktesting == true)
            {
                return;
            }
            if (TimeFrame > 0)
            {
                ScrollBars = EndFrame / TimeFrame - ScaleBars + SCROLL_TO_END_OFFSET;
            }
            yOffset = 0;
        }

        internal void ScrollToStart()
        {
            if (MainObj.IsBacktesting == true)
            {
                return;
            }
            ScrollBars = 0;
            yOffset = 0;
        }

        internal void ScrollForward(bool page)
        {
            yOffset = 0;
            ScrollBy(page ? ScaleBars : 1, false);
        }

        internal void ScrollBack(bool page)
        {
            yOffset = 0;
            ScrollBy(page ? ScaleBars : 1, true);
        }

        //DispatcherTimer scrollTimer;
        internal void ScrollBy(double scrollAmount, bool forward)
        {
            //not animating: this needs re-doing, and in any case would probably be better just to slide the container rather than mess around with saving the data and shifting it
            yOffset = 0;
            ScrollBars += forward ? (int)scrollAmount : (int)-scrollAmount;
            return;
#region unused
            /*
            bool single = scrollAmount == 1;
            double fastScroll = 100 / (double)(ScaleBars);  //default is 100, if scale is larger need a faster scroll
            //scrollAmount = scrollAmount * fastScroll / 2; //increase or decrease the amount to be scrolled

            if (scrollTimer != null)
            {
                scrollTimer.Stop();
            }

            int numSteps = (int) (scrollAmount / fastScroll);
            if (numSteps <= 0)
            {
                numSteps = 1;
            }
            scrollAmount /= numSteps;

            if (scrollAmount < 1)
            {
                scrollAmount = 1;
            }

            if (!single)
            {
                //speed it up

                savedScrollOffset = (int)(4 * scrollAmount / fastScroll);
                numSteps = (int) ((double) numSteps / (4 / fastScroll));
            }

            int direction = forward ? (int)scrollAmount : (int)-scrollAmount;
            scrollTimer = new DispatcherTimer();
            scrollTimer.Tick += new EventHandler((source, eventArgs) =>
            {
                ScrollBars += direction;
                numSteps--;
                Draw(numSteps > 1);

                if (numSteps == 0)
                {
                    scrollTimer.Stop();
                }
            });
            scrollTimer.Interval = new TimeSpan(0, 0, 0, 0, 10);
            scrollTimer.Start();
            */
#endregion
        }

        double oldScrollbars;
#endregion

        #region Timeframe
        public void OnTimeframeChange(int oldTimeFrame, int timeFrame)
        {
            if (items_chart != null)
            {
                items_chart.Clear(); //invalidate for drawing
            }

            double timeRescale = oldTimeFrame / timeFrame;
            ScrollBars = (int)(ScrollBars * timeRescale);
            if (ScrollBars - ScaleBars > EndFrame)
            {
                ScrollBars = EndFrame - ScaleBars;
            }
            ScrollBars++;
        }

        //go to a frame, time-adjusted (i.e. aggregated frame count)
        public void GoToFrame(int frame, int offsetFrames)
        {
            frame /= TimeFrame;
            frame += offsetFrames;
            yOffset = 0;

            ScrollBars = frame - ScaleBars;
            ScrollBars++;
        }

        #endregion

        #region Utility methods

        public int GetFrameForTimestamp(DateTime timestamp, bool isDrawingLine = false)
        {
            if (!Results.Any())
            {
                return 0;
            }

            var last = Results.Last().Timestamp;
            var diff = timestamp.Subtract(last).TotalMinutes;
            if (diff < 0)
            {
                //timestamp is before last item, so get the item
                //cannot guarantee that the Results values are rounded to the minute, can be 59 secs
                //also the timestamp parameter can have seconds

                //not testing for the index where timestamp is less than next frame, because there can be missing frames so next frame may be missing
                // - just test for greater than timestamp
                var ret = Results.FindIndex(x => x.Timestamp >= timestamp);
                if (ret == -1)
                {
                    return Results.Count - 1; //to be safe
                }
                return ret;
            }
            else
            {
                //timestamp is same as or after last item, so add frames to get the position it would be at if that many items existed
                //THIS HAS +1 FRAME OR LINE DRAWING IS ONE FRAME OUT
                return (int)(Results.Count + diff + (isDrawingLine ? 1 : 0));
            }
        }

        //inverse of the above

        internal DateTime GetTimestampFromFrameNum(int frameNum)
        {
            if (!Results.Any())
            {
                return DateTime.UtcNow;
            }
            if (frameNum < 0)
            {
                return Results.First().Timestamp;
            }
            if (frameNum > Results.Count - 1)
            {
                var numExtraFrames = frameNum - Results.Count;
                return Results.Last().Timestamp.AddMinutes(numExtraFrames);
            }
            return Results[frameNum].Timestamp;
        }

        //methods to get the on-screen position for a price and frame
        internal (double left, double top) GetDrawPosition(int frame, double price, bool addYOffset = true)
        {
            //this method has been changed and is untested
            var numFramesToPosition = frame - ScrollBars;

            var left = numFramesToPosition * CandleWidth + CandleWidth / 2;

            var _yOffset = addYOffset ? yOffset : 0;

            var range = (price - Low) / (High - Low);
            var top = MainObj.invertChart
                ? canvas.ActualHeight * range + _yOffset
                : canvas.ActualHeight * (1 - range) + _yOffset;

            return (left, top);
        }


        internal (double left, double top) GetDrawPosition1(DateTime timeStamp, double price, bool addYOffset = true)
        {
            if (items_chart == null || items_chart.Count == 0)
            {
                return (0, 0);
            }
            var frame = 0;

            var mins = timeStamp.Subtract(items_chart[0].Timestamp).TotalMinutes;
            mins %= TimeFrame;
            timeStamp = timeStamp.AddMinutes(-mins);

            if (timeStamp <= items_chart.Last().Timestamp)
            {
                frame = items_chart.FindIndex(x => x.Timestamp >= timeStamp);
            }
            else
            {
                var numExtraBars = (timeStamp - items_chart.Last().Timestamp).TotalMinutes / TimeFrame - 1;
                frame = items_chart.Count + (int)numExtraBars;
            }

            var numFramesToPosition = frame - ScrollBars;

            var left = numFramesToPosition * CandleWidth + CandleWidth / 2;

            var _yOffset = addYOffset ? yOffset : 0;
            var range = (price - Low) / (High - Low) * Scale;

            var top = MainObj.invertChart
                ? canvas.ActualHeight * range + _yOffset
                : canvas.ActualHeight * (1 - range) + _yOffset;

            return (left, top);
        }

        /*
        internal (double left, double top) GetDrawPosition1(DateTime timeStamp, double price, bool addYOffset = true)
        {
            if (items_chart == null)
            {
                return (0, 0);
            }
            var frame = 0;
            var mins = timeStamp.Subtract(refDateTime).TotalMinutes;
            mins %= TimeFrame;
            timeStamp = timeStamp.AddMinutes(-mins);

            if (timeStamp <= items_chart.Last().Timestamp)
            {
                frame = items_chart.FindIndex(x => x.Timestamp >= timeStamp);
            }
            else
            {
                var numExtraBars = (timeStamp - items_chart.Last().Timestamp).TotalMinutes / TimeFrame - 1;
                frame = items_chart.Count + (int)numExtraBars;
            }

            var numFramesToPosition = frame - ScrollBars;

            var left = numFramesToPosition * CandleWidth + CandleWidth / 2;
            var _yOffset = addYOffset ? yOffset : 0;
            var range = (price - Low) / (High - Low) * scaleMult;
            var top = MainObj.invertChart
                ? canvas.ActualHeight * range + _yOffset
                : canvas.ActualHeight * (1 - range) + _yOffset;

            return (left, top);
        }
        */

        //as above but for the last visible frame
        internal (double left, double top) GetDrawPosition(double price)
        {
            //return GetDrawPosition(items_chart.Count, price);
            return GetDrawPosition1(items_chart.Last().Timestamp, price);
        }
        internal (double left, double top) GetDrawPosition(_Trade trade, bool addYOffset = true)
        {
            if (showHeikinAshi)
            {
                //todo - find out why HA trades are displayed one timeframe too early. This is a temp. fix
                return GetDrawPosition1(trade.Timestamp.AddMinutes(TimeFrame), trade.Price, addYOffset);
            }
            else
            {
                return GetDrawPosition1(trade.Timestamp, trade.Price, addYOffset);
            }
        }

        internal (DateTime timeStamp, double price) GetTimeAndPriceFromLastMouse(bool isSettingTrade = false)
        {
            return GetTimeAndPrice(mouseX, mouseY, isSettingTrade);
        }

        
        //inverse of GetDrawPosition(), returns frame and price from absolute pixel coordinates
        /// <summary>
        /// 
        /// </summary>
        /// <param name="left"></param>
        /// <param name="top"></param>
        /// <param name="isSettingTrade">If setting a trade, will use current time on LIVE build (so trade is always placed now), frame time on BACKTEST build</param>
        /// <returns></returns>
        internal (DateTime timeStamp, double price) GetTimeAndPrice(double left, double top, bool isSettingTrade = false)
        {

            //this just solves the draw position for frames given the on-screen position, using the GetDrawPosition formula
            //var frame = TimeFrame * ((left - CandleWidth / 2) * ScaleBars / canvas.ActualWidth + ScrollBars + 0.5);
            var frame = GetFrameNumFromMouseX(left);
            var timeStamp = isSettingTrade
                ? !MainObj.BACKTEST ? DateTime.UtcNow : GetTimestampFromFrameNum((int)frame)
                : GetTimestampFromFrameNum((int)frame);
            var price = GetTradePriceFromScreen(top);
            return (timeStamp, price);
        }

        public int GetFrameNumFromMouseX(double pos)
        {
            //this just solves the draw position for frames given the on-screen position, using the GetDrawPosition formula
            var ret =  (int)(TimeFrame * ((pos - CandleWidth / 2) * ScaleBars / canvas.ActualWidth + ScrollBars + 0.5));
            return ret;
        }

        public int GetFrameNumFromLastMousePos()
        {
            return GetFrameNumFromMouseX(mouseX);
        }

        public int GetLastFrameNum()
        {
            return (ScrollBars + items_chart.Count) * TimeFrame;
        }

        public double GetTradePriceFromLastMouseY()
        {
            return GetTradePriceFromScreen(mouseY);
        }

        public double GetTradePriceFromScreen(double y)
        {
            //this just solves for price given the on-screen position, using the GetDrawPosition formula
            double range = High - Low;
            double pos = MainObj.invertChart ? (y + yOffset) / canvas.ActualHeight : (canvas.ActualHeight - y + yOffset) / canvas.ActualHeight;
            return Low + range * pos;
        }
        #endregion

        #region Unused
        //unused
        /*
        internal (DateTime timeStamp, double price) GetTimeAndChartPriceFromLastMouse() //always used to set a trade
        {
            // (DateTime timeStamp, double price) =  GetTimeAndPrice(mouseX, mouseY, isSettingTrade);

            //ignore the price at the cursor, use the bar price instead
            //this just solves the draw position for frames given the on-screen position, using the GetDrawPosition formula
            //var frame = TimeFrame * ((left - CandleWidth / 2) * ScaleBars / canvas.ActualWidth + ScrollBars + 0.5);
            if (MainObj.LIVE)
            {
                return (DateTime.UtcNow, tradeEngine.currentDataItem.Close);
            }

            //backtesting
            if (MainObj.BACKTEST)
            {
                var frame = GetFrameNumFromMouseX(mouseX);
                //may be clicking beyond candle range

                //due to display bug, cannot use last candle so go to previous one to avoid confusion
                // - problem is, last candle doesn't display unless chart scrolled (i.e. if whole chart does not fit on screen),
                // and when scrolled, last candle is messed up anyway
                if (frame > tradeEngine.Results.Count - 1)
                {
                    frame = tradeEngine.results.Count - 1;
                }

                var frameNum = (int)((double)frame / TimeFrame) * TimeFrame;

                //offset for start hour if on day
                if (TimeFrame == 1440)
                {
                    frameNum += 60 * TradeEngine.START_HOUR;
                }

                var timeStamp = GetTimestampFromFrameNum(frameNum);
                var price = tradeEngine.Results[frameNum].Open;
                return (timeStamp, price);
            }

            throw new Exception();


        }
        */

        #endregion
    }
}
