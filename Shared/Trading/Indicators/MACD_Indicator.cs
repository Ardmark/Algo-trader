using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.MovingAverages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace AlgoTrader.Shared.TradeIndicators
{
    public class MACD_Indicator : DisplayPanel
    {
        readonly MACD macd;
        readonly EMA signal;
        public readonly MovingAverage histogram;

        const int NUM_BARS_RISING_FOR_ENTRY = 6;

        //const int timeFrame_major = 1440;
        const int timeFrame_fixed = 180;

        List<int> timeFrames = new List<int>();
        readonly EMA baseEMA;

        public override double YOffset
        {
            get
            {
                return 0;
            }
        }
        public MACD_Indicator(TradeEngine tradeEngine,  Chart chart) : base(null, timeFrame_fixed, tradeEngine, chart)
        {
            canvas = MainObj.indicator1_Canvas;    
            
            movingAverages.Add(macd = new MACD(chart, 65, 200));

            //this does not actually initialise the EMA with any values, but it catches up OK after a while so is usable
            movingAverages.Add(signal = new EMA(CandlePosition.CLOSE, chart, 9));
            movingAverages.Add(histogram = new MovingAverage(CandlePosition.CLOSE, chart, 0));
            movingAverages.Add(baseEMA = new EMA(CandlePosition.CLOSE, chart, 100));

            base.InitIndicators(timeFrame_fixed);
            //base.InitIndicators(timeFrame_major);   
        }

        public override void InitIndicators(int timeFrame)
        {


        }
        //ItemAdded - called when an item is added to the DB, this can be used for example with a moving average
        public override void ItemAdded(_SymbolDataItem item, bool canComputeTradeEntry, bool liveItem)//, bool redrawOnly)
        {
            //foreach (var timeFrame in MainObj.timeFrames)
            //{
            //    var _macd = macd.ItemAdded(item, timeFrame);
            //    var _signal = signal.AddElementNoAggregation(_macd, timeFrame);
            //    histogram.AddInterpolatedResults(_macd - _signal, timeFrame); //just using the results list in this moving average
            //    var _ema = baseEMA.ItemAdded(item, timeFrame);
            //}



                var _macd = macd.ItemAdded(item, timeFrame_fixed);
                var _signal = signal.AddElementNoAggregation(_macd, timeFrame_fixed);
                histogram.AddInterpolatedResults(_macd - _signal, timeFrame_fixed); //just using the results list in this moving average
                var _ema = baseEMA.ItemAdded(item, timeFrame_fixed);

                //_macd = macd.ItemAdded(item, timeFrame_major);
                //_signal = signal.AddElementNoAggregation(_macd, timeFrame_major);
                //histogram.AddInterpolatedResults(_macd - _signal, timeFrame_major); //just using the results list in this moving average
                //_ema = baseEMA.ItemAdded(item, timeFrame_major);

            /*
                macd = fast_ma - slow_ma
                signal = sma_signal ? sma(macd, signal_length) : ema(macd, signal_length)
                hist = macd - signal
            */
        }

        //test for trade entry, based on the fixed timeframe - this is for live trading
        public override bool CanEnterTrade(int timeFrame, double currPrice, double prevPrice, int currentFrame)
        {
            #region Histogram cross up on zero

            //just return true when histogram hits zero, this would be for trade entry test
            /*
             *  var results = histogram.GetResultsFromDictionary(timeIntervalFixed ? TimeFrame : visibleTimeFrame);
            if (frame == -1)
            {
                frame = results.Count - 1;
            }

            //get starting frame, this will be based on the visible time frame

            //only using timeframe boundary results
            frame = ((int)(frame / visibleTimeFrame)) * visibleTimeFrame;
            frame -= visibleTimeFrame;
            if (frame <= 0 || frame + visibleTimeFrame >= results.Count)
            {
                return false;
            }
            if (!timeIntervalFixed || visibleTimeFrame == TimeFrame)
            {
                return results[frame] < 0 && results[frame + visibleTimeFrame] >= 0;
            }
            else
            {
                //loop across a set of frames, this would be for a different display timeframe to the results timeframe
                for (int i = 0; i < visibleTimeFrame / TimeFrame; i++)
                {
                    var _frame = frame + i * TimeFrame;
                    if (_frame + TimeFrame >= results.Count)
                    {
                        break;
                    }
                    if (results[_frame] < 0 && results[_frame + TimeFrame] >= 0)
                    {
                        return true;
                    }
                }
            } 
            */
            #endregion

            //var resultsMACD_fixed = macd.GetResultsFromDictionary(timeFrame_fixed);
            //var resultsEMA_fixed = baseEMA.GetResultsFromDictionary(timeFrame_fixed);
            var resultsHist_fixed = histogram.GetResultsFromDictionary(timeFrame_fixed);
            var frame = resultsHist_fixed.Count - 1;

            //here's the problem: the macd only achieves correct values after about 400 bars. On the hour, this is 2 - 3 weeks. On the day, this is about 14 months.
            //so it is not going to be possible to use the day as a signal generator.

            int startFrame = ((int)(frame / timeFrame_fixed)) * timeFrame_fixed;
            startFrame -= timeFrame_fixed;

            if (startFrame < timeFrame_fixed)
            {
                return false;
            }

            if (resultsHist_fixed[startFrame - timeFrame_fixed] <= 0 && resultsHist_fixed[startFrame] > 0)
            {
                return true;
            }

            return false;
        }

        //as above, but will loop over each hour to see if an entry has been indicated
        private bool CanEnterTradeDraw(int frame = -1)
        {
            return false;

            //best to keep this, it looks for cross up over the day, for drawing on the day
            //var resultsMACD_major = macd.GetResultsFromDictionary(timeFrame_major);
            //var resultsEMA_major = baseEMA.GetResultsFromDictionary(timeFrame_major);

            //var resultsMACD_minor = macd.GetResultsFromDictionary(timeFrame_fixed);
            //var resultsEMA_minor = baseEMA.GetResultsFromDictionary(timeFrame_fixed);
            //if (frame == -1)
            //{
            //    frame = resultsEMA_minor.Count - 1;
            //}


            //var histResultsMinor = histogram.GetResultsFromDictionary(timeFrame_fixed);

            ////here's the problem: the macd only achieves correct values after about 400 bars. On the hour, this is 2 - 3 weeks. On the day, this is about 14 months.
            ////so it is not going to be possible to use the day as a signal generator.

            //int startFrame = ((int)(frame / timeFrame_major)) * timeFrame_major;
            //startFrame -= timeFrame_major;

            //if (startFrame < timeFrame_fixed)
            //{
            //    return false;
            //}


            //for (int i = 0; i < timeFrame_major / timeFrame_fixed; i++)
            //{
            //    var _frame = startFrame + i * timeFrame_fixed;
            //    if (_frame + timeFrame_fixed >= resultsEMA_minor.Count)
            //    {
            //        break;
            //    }

            //    if (histResultsMinor[_frame - timeFrame_fixed] <= 0 && histResultsMinor[startFrame] > 0)
            //    {
            //        return true;
            //    }

            //}

            //return false;
        }


        public override void Draw()
        {
            //return;
            int startFrame = Chart.ScrollBars;
            int numFrames = Chart.ScaleBars;

            try
            {
                //this will draw on a fixed timeframe of 24 hours, and look for entries on the shorter timeframe
                Low = double.MaxValue;
                High = double.MinValue;
                var timeFrame = timeFrame_fixed;
                var visibleTimeFrame = MainObj.visibleChart.TimeFrame;
                if (visibleTimeFrame != timeFrame_fixed)
                {
                    canvas.Children.Clear();
                    return;
                }
                List<double> items_macd = macd.GetResults(startFrame, numFrames, timeFrame, timeFrame);
                List<double> items_signal = signal.GetResults(startFrame, numFrames, timeFrame, timeFrame);
                List<double> items_hist = histogram.GetResults(startFrame - NUM_BARS_RISING_FOR_ENTRY, numFrames + NUM_BARS_RISING_FOR_ENTRY, timeFrame, timeFrame);
                var min = items_hist.Min();
                var max = items_hist.Max();
                var scale = 100 / (max - min);
                List<double> items_hist_scaled = new List<double>();
                foreach (var c in items_hist)
                {
                    items_hist_scaled.Add(c * scale);
                }

                var macdColor = new SolidColorBrush(Color.FromRgb(0, 148, 255));
                var signalColor = new SolidColorBrush(Color.FromRgb(255, 106, 0));
                if (items_macd != null)
                {
                    //set the scaling
                    //SetScaling(new[] { items_macd, items_signal, items_hist });
                    SetScaling(new[] { items_hist_scaled });

                    InitDraw();
                    canvas.Children.Clear();
                    //DrawLineSegments(items_macd, 1, macdColor);
                    //DrawLineSegments(items_signal, 1, signalColor);

                    double rectWidth = CandleWidth;

                    for (int i=1; i< items_hist.Count-1; i++)
                    {
                        var curr = items_hist[i];

                        Point start = new Point(i - 1, 0);// - NUM_BARS_RISING_FOR_ENTRY, 0);
                        Point end = new Point(i, 0);// - NUM_BARS_RISING_FOR_ENTRY, 0);

                        var col = curr <= 0 ? Candle.CANDLE_COLOR_DOWN_HA : Candle.CANDLE_COLOR_UP_HA; ;
                        
                        DrawLineSegment(start, end, curr * scale, col, 1);

                        const int majorTimeFrame = 1440;
                        var _startFrame = (startFrame + i) * majorTimeFrame;
                        var _endFrame = _startFrame + numFrames * majorTimeFrame;

                        if (CanEnterTradeDraw(_startFrame))
                        {
                            DrawMarker(MarkerStyle.TRIANGLEUP, new System.Windows.Point(i - 1, /*canvas.ActualHeight -*/ 10), Brushes.DarkGray, 10);
                        }

                    }
                }
            }
            catch
            {

            }
            /*        for (int i=0; i < items_macd.Count; i++)
                    {

                        if (MovingIndicatorBase.Crossover(macd, signal, startFrame + i)) //the stored results are latest first, but the iterator returns earliest first so must reverse frame order
                        {
                            DrawMarker(MarkerStyle.CIRCLE, new System.Windows.Point(i, canvas.ActualHeight - 10), Brushes.Red, 10);
                        }
                        if (MovingIndicatorBase.Crossover(signal, macd, startFrame + i))
                        {
                            DrawMarker(MarkerStyle.CIRCLE, new System.Windows.Point(i, canvas.ActualHeight - 10), Brushes.Blue, 10);
                        }
                    }
                    */
        }

        internal bool TestForStopExit(int timeFrame, int currentFrame, DateTime timestamp1, DateTime timestamp2)
        {
            throw new NotImplementedException();
        }
    }
}
