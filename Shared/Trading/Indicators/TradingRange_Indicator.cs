using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.MovingAverages;
using static Shared.Drawings.DrawingObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AlgoTrader.Shared.TradeIndicators
{
    public class TradingRange_Indicator : DisplayPanel
    {
        #region Fields

        readonly int emaLength = 5;
        readonly int rangeLength = 5; //how far back to go for range
        double maxVolatility = 2; //max volatility for entry, as a multiplier of range

        protected List<MovingAverage> indicators = new List<MovingAverage>();
        EMA highIndicator, lowIndicator;


        bool hasRange, entryFound;
        double low, high;

        //tuple for entries: frame index, price, long or short (long == true)
        List<(int, double, bool)> entries = new List<(int, double, bool)>();

        int lineThickness = 1;
        #endregion

        #region Init
        public TradingRange_Indicator(string symbolName, Canvas canvas, Chart chart, TradeEngine tradeEngine) : base(symbolName, 0, tradeEngine, chart)
        {
            rangeLength *= chart.TimeFrame;
            highIndicator = new EMA(CandlePosition.HIGH, chart, emaLength);
            lowIndicator = new EMA(CandlePosition.LOW, chart, emaLength);
            indicators.Add(highIndicator);
            indicators.Add(lowIndicator);

        }
        #endregion

        #region Item added
        //ItemAdded - called when an item is added to the DB, this can be used for example with a moving average
        public override void ItemAdded(_SymbolDataItem item, bool canComputeTradeEntry, bool liveItem)//, bool redrawOnly)
        {
            base.ItemAdded(item, canComputeTradeEntry, liveItem);

            //foreach (MovingAverage indicator in indicators)
            //{
            //    foreach (var timeFrame in MainObj.timeFrames)
            //    {
            //        indicator.ItemAdded(item, timeFrame);
            //    }
            //}

            var results = highIndicator.GetResultsFromDictionary(TimeFrame);
            if (results.Count > rangeLength)
            {
                //during init, will only have AddItem, so will need to test for entry at this point
                //as item is updated, will then only add entry if not already done here
                if (hasRange)
                {
                    AddEntryIfMaxVolatilityExceeded(item); //need to use the previous ema range so do this first
                }

                //set high low value after close of bar, these values will be used during the ItemUpdated method calls
                high = double.MinValue;
                low = double.MaxValue;

                var firstFrame = results.Count - rangeLength;
                foreach (var val in highIndicator.GetResult(firstFrame, rangeLength, TimeFrame)) //not very efficient
                {
                    if (val > high)
                    {
                        high = val;
                    }
                }
                foreach (var val in lowIndicator.GetResult(firstFrame, rangeLength, TimeFrame))
                {
                    if (val > 0 && val < low)
                    {
                        low = val;
                    }
                }

                hasRange = true;
            }


            entryFound = false;


        }

        #endregion

        #region Item updated
        public override void ItemUpdated(_SymbolDataItem item,  bool itemAdded = false)
        {
            base.ItemUpdated(item, itemAdded);

            bool entryWasFound = entryFound;
            AddEntryIfMaxVolatilityExceeded(item);

            if (!entryWasFound && entryFound)
            {
                //draw
                DrawEntryMarker(Chart.GetDrawPosition(item.Open));      
            }
        }

        #endregion

        void AddEntryIfMaxVolatilityExceeded(_SymbolDataItem item)
        {
            if (hasRange && !entryFound)
            {
                var v = (item.High - low) / (high - low);
                if (v > maxVolatility)
                {
                    entryFound = true;
                    entries.Add(new ValueTuple<int, double, bool>(highIndicator.GetResultsFromDictionary(TimeFrame).Count, item.High, true)); //regular Tuple does not have extension method Add for IList, so need to add that extension manually or use ValueTuple
                }
            }
        }
        #region Draw
        public override void Draw()
        {
            int startFrame = Chart.ScrollBars;
            int numFrames = Chart.ScaleBars;

            if (MainObj.IsBacktesting)
            {
                return;
            }
            MovingAverage indicator1 = indicators.First();
            MovingAverage indicator2 = indicators.Last();

            //overlays chart, so must use the same scaling
            Low = Chart.Low;
            High = Chart.High;
            Scale = Chart.Scale;

            #region Draw indicators

            InitDraw();

            foreach (MovingAverage indicator in indicators)
            {
                int count = 0;
                Point start = new Point();
                Point end = new Point();

                //need the actual start frame in the visible timeframe
                int frame = startFrame;
                int _numFrames = numFrames;
                indicator.GetFrameCount(ref frame, ref _numFrames, TimeFrame, TimeFrame);

                foreach (double val in indicator.GetResult(frame, _numFrames, TimeFrame))
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

                    DrawLineSegment(start, end, lineThickness, Brushes.Black);

                    count++;

                    //draw the entries
                    var entry = entries.Where(x => x.Item1 == frame);
                    if (entry.Any())
                    {
                        var price = entry.Select(x => x.Item2).First();
                        DrawEntryMarker(Chart.GetDrawPosition(frame, price));
                    }
                    
                    frame += MainObj.TimeFrame;
                }
            }

            #endregion
        }


        private void DrawEntryMarker((double left, double top) pos)
        {
            var tradeMarker = TradeOrderMarker(TradePosition.LONG, pos.left, pos.top, Brushes.Bisque, Brushes.Black, Brushes.Black, MainObj.TimeFrame.ToString());
            canvas.Children.Add(tradeMarker.canvas);
        }

        #endregion


    }
}
