using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.MovingAverages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using static AlgoTrader.Shared.Chart;

namespace AlgoTrader.Shared.TradeIndicators
{
    public class HeikinAshi_Indicator : DisplayPanel
    {
        public override double YOffset
        {
            get
            {
                return 0;
            }
        }
        public HeikinAshi_Indicator(TradeEngine tradeEngine,  Chart chart) : base(null, 0, tradeEngine, chart)
        {

        }

        public override void InitIndicators(int timeFrame)
        {
            base.InitIndicators(timeFrame);
            canvas = MainObj.indicator1_Canvas;
        }
        //ItemAdded - called when an item is added to the DB, this can be used for example with a moving average
        public override void ItemAdded(_SymbolDataItem item, bool canComputeTradeEntry, bool liveItem)//, bool redrawOnly)
        {

        }

        const int numFramesToSample = 5;

        public bool RisingTrend(int timeFrame, int frame, List<_SymbolDataItem> results = null)
        {
            //may make more sense to hold a set of persistant results for all timeframes
            if (results == null)
            {
                (results, _) = tradeEngine.GetResultsHA_UI(frame - numFramesToSample * timeFrame, numFramesToSample * timeFrame, timeFrame, false);
                if (results == null || results.Count < numFramesToSample - 1)
                {
                    return false;
                }
            }
            var currItem = results[results.Count - 1];
            var prevItem = results[results.Count - 2];
            var currItemUp = currItem.Close >= currItem.Open;
            var prevItemUp = prevItem.Close >= prevItem.Open;

            //only interested in both bars blue
            return currItemUp && prevItemUp;

        }

        //similar to rising trend, but this is only where both bars are falling and current close is lower than previous close
        public bool FallingTrend(int timeFrame, int frame, List<_SymbolDataItem> results = null)
        {
            //may make more sense to hold a set of persistant results for all timeframes
            if (results == null)
            {
                (results, _) = tradeEngine.GetResultsHA_UI(frame - numFramesToSample * timeFrame, numFramesToSample * timeFrame, timeFrame, false);
                if (results == null || results.Count < numFramesToSample - 1)
                {
                    return false;
                }
            }
            var currItem = results[results.Count - 1];
            var prevItem = results[results.Count - 2];
            var currItemUp = currItem.Close >= currItem.Open;
            var prevItemUp = prevItem.Close >= prevItem.Open;

            if (!currItemUp && !prevItemUp) //both bars red
            {
                if (currItem.Close < prevItem.Close)
                {
                    return true;
                }
            }
            return false;

        }

        //this method is used in backtesting for consistency
        public int CanEnterTrade(int timeFrame, int frame = -1, int offset = -1, List<_SymbolDataItem> results = null)
        {

            //this is called using Chart ScrollBars and ScaleBars, so the frame relative to the Timeframe
            if (frame == -1 || frame > chart.Results.Count - 1)
            {
                frame = chart.Results.Count - 1;
            }

            if (frame <= 0)
            {
                return 0;
            }

            //may make more sense to hold a set of persistant results for all timeframes
            if (results == null)
            {
                (results, _) = tradeEngine.GetResultsHA_UI(frame - numFramesToSample * timeFrame, numFramesToSample * timeFrame, timeFrame, false);
                if (results == null || results.Count < numFramesToSample - 1)
                {
                    return 0;
                }
            }
            //offset is used with the pre-populated list of results, to speed up drawing
            if (offset == 0 || offset >= results.Count)
            {
                return 0;
            }
            var currItem = offset == -1 ? results[results.Count - 1] : results[offset];
            var prevItem = offset == -1 ? results[results.Count - 2] : results[offset - 1];
            var currItemUp = currItem.Close >= currItem.Open;
            var prevItemUp = prevItem.Close >= prevItem.Open;


            //enter on blue bar
            if (currItemUp && !prevItemUp)
            {
                return 1;
            }

            //if both red, enter if current close is higher than previous close
            if (!currItemUp && !prevItemUp)
            {
                if (currItem.Close > prevItem.Close)
                {
                    return 1;
                }
            }



            //doji
            //if (prevItemDown)
            //{
            //    //trend was falling
            //    var bodyHeight = Math.Abs(currItem.Open - currItem.Close);
            //    var topWickHeight = currItemUp
            //        ? currItem.High - currItem.Close
            //        : currItem.High - currItem.Open;
            //
            //    var bottomWickHeight = currItemUp
            //        ? currItem.Open - currItem.Low
            //        : currItem.Close - currItem.Low;
            //
            //    double dojiScale = 3;
            //
            //    if (topWickHeight >= dojiScale * bodyHeight && bottomWickHeight >= dojiScale * bodyHeight)
            //    {
            //        return 2;
            //    }
            //}

            return 0;
        }

        
        public bool CanExitTrade(int timeFrame, int frame = -1, int offset = -1, List<_SymbolDataItem> resultsHA = null, List<_SymbolDataItem> results = null, _Trade entryTrade = null)
        {

            //this is called using Chart ScrollBars and ScaleBars, so the frame relative to the Timeframe
            if (frame == -1)
            {
                frame = chart.Results.Count - 1;
            }

            if (frame <= 0 || frame >= tradeEngine.results.Count)
            {
                return false;
            }


            //may make more sense to hold a set of persistant results for all timeframes
            if (resultsHA == null)
            {
                (resultsHA, _) = tradeEngine.GetResultsHA_UI(frame - numFramesToSample * timeFrame, numFramesToSample * timeFrame, timeFrame, false);
                if (resultsHA == null || resultsHA.Count < numFramesToSample - 1)
                {
                    return false;
                }
            }
            //if (results == null)
            //{
            //    results = tradeEngine.GetResults(frame - numFramesToSample * timeFrame, numFramesToSample * timeFrame, timeFrame);
            //    if (results == null || results.Count < numFramesToSample - 1)
            //    {
            //        return false;
            //    }
            //}
            //offset is used with the pre-populated list of results, to speed up drawing
            if (offset == 0 || offset >= resultsHA.Count)
            {
                return false;
            }
            var currItemHA = offset == -1 ? resultsHA[resultsHA.Count - 1] : resultsHA[offset];
            var prevItemHA = offset == -1 ? resultsHA[resultsHA.Count - 2] : resultsHA[offset - 1];
            var currItemUpHA = currItemHA.Close >= currItemHA.Open;
            var prevItemUpHA = prevItemHA.Close >= prevItemHA.Open;


            // exit on red down bar
            if (!currItemUpHA && prevItemUpHA)
            {
                 return true;
            }

            //if current and previous red, exit if current close is lower
            if (!currItemUpHA && !prevItemUpHA)
            {
                if (currItemHA.Close < prevItemHA.Close)
                {
                    return true;
                }
            }

            return false;
        }


        public override void Draw()
        {
            return;

            int startFrame = Chart.ScrollBars;
            int numFrames = Chart.ScaleBars;

            //the macd indicator has good drawing code for lines, etc.
            //but really need to draw on a drawing context
            try
            {

                canvas.Children.Clear();   
                //if (Chart.hideTrades)
                //{
                //    return;
                //}
                (var resultsHA, _) = tradeEngine.GetResultsHA_UI(startFrame * Chart.TimeFrame, numFrames * Chart.TimeFrame, Chart.TimeFrame);
                if (resultsHA == null)
                {
                    return;
                }

                //for (int i = 2; i < resultsHA.Count; i++) //no need to draw on every possible bar
                int i = resultsHA.Count - 1;
                {

                    var currItemHA = resultsHA[i];
                    var prevItemHA = resultsHA[i-1];
                    var currItemUpHA = currItemHA.Close >= currItemHA.Open;
                    var prevItemUpHA = prevItemHA.Close >= prevItemHA.Open;

                    bool draw = false;

                    //entry
                    if (currItemUpHA && !prevItemUpHA)
                    {
                        draw = true;
                    }

                    if (!currItemUpHA && !prevItemUpHA)
                    {
                        if (currItemHA.Close > prevItemHA.Close)
                        {
                            draw = true;
                        }
                    }

                    if (draw)
                    {
                        DrawMarker(MarkerStyle.TRIANGLEUP, new System.Windows.Point(i+1, canvas.ActualHeight - 10), Candle.CANDLE_COLOR_UP_HA, 10);
                    }

                    //exit 
                    draw = false;
                    if (!currItemUpHA && prevItemUpHA)
                    {
                        draw = true;
                    }
                    if (currItemUpHA && prevItemUpHA)
                    {
                        if (currItemHA.Close < prevItemHA.Close)
                        {
                            draw = true;
                        }
                    }
                    if (draw)
                    {
                        DrawMarker(MarkerStyle.TRIANGLEDOWN, new System.Windows.Point(i+1, canvas.ActualHeight - 10), Candle.CANDLE_COLOR_DOWN_HA, 10);
                    }

                }
            }
            catch (Exception e)
            {

            }
        }

    }
}
