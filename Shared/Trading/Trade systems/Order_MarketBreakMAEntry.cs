using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.MovingAverages;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace AlgoTrader.Shared
{
    public class Order_MarketBreakMAEntry : Order_MarketBreak
    {
        #region Fields
        private readonly int lineThickness;
        readonly SolidColorBrush colorUp, colorDown;
        #endregion

        #region Init
        public Order_MarketBreakMAEntry(string symbolName, int timeInterval, TradeEngine tradeEngine, Chart chart, CandlePosition candlePosition, int lineThickness, SolidColorBrush colorDown, SolidColorBrush colorUp) : base(symbolName, timeInterval, tradeEngine, chart, candlePosition, lineThickness, colorDown, colorUp)
        {
            this.lineThickness = lineThickness;
            this.colorUp = colorUp;
            this.colorDown = colorDown;
        }
        #endregion

        #region Enter trade


        public override bool CanEnterTrade(SymbolDataItem item, DataContext db)
        {
            
            try
            {
                int frame = Chart.GetFrameForTimestamp(item.Timestamp); //will work with backtest

                if (frame >= tradeEngine.results.Count)
                {
                    //this would happen if a trade was set after startup but before interpolation of missed frames. However, there is also a flag in tradeEngine to block this
                    frame = tradeEngine.Results.Count - 1;
                }

                if (frame - emaUpDown.timeFrame * 2 < 0)
                {
                    return false;
                }

                #region unused
                /*
                //auto-cancel this order if there is a full break down of the 200 ema
                //this is a rising trend can have an intitial pump, then a ema break order set - if it drops to the 200ema, it can bounce
                // but this will only be a one-time trade, the next one must be a regular break order

                bool _isRising60 = MovingAverage.IsRising(emaUpDown.emaLarge, frameNum, emaUpDown.timeFrame);
                bool _isAbove60 = MovingAverage.IsAbove(emaUpDown.emaSmall, emaUpDown.emaLarge, frameNum, emaUpDown.timeFrame);

                if (ActiveOrder.Position == TradePosition.LONG ? !_isRising60 && !_isAbove60 : _isRising60 && _isAbove60)
                {
                    //remove the order 
                    LogTrades.WriteLine(string.Format("Trade {0} removed at {1} (200 ema crossed negative)", ActiveOrder.Id, item.Timestamp));
                    MainObj.activeOrders.Remove(ActiveOrder);
                    db.Trades.RemoveRange(db.Trades.Where(x => x.TradeId == ActiveOrder.TradeId));
                    db.SaveChanges();
                    ActiveOrder = null;
                    MainObj.TradeStatusChanged();
                    MainObj.Draw(db);
                }
                */
                #endregion

                //will enter immediately if break from below or above, no wait for first bar to open above, etc.

                double sma_threshold_pc = TradeEngine.SMA_THRESHOLD_PC / 100 + 1; //allow a quarter percent threshold range for SMA price cross

                //offset the SMA price by a small percentage, so price can just miss and will still trigger
                var emaPrice = emaUpDown.GetResultLarge(frame);
                var emaAdjustedPrice = emaPrice * (ActiveOrder.Position == TradePosition.LONG ? sma_threshold_pc : 1 / sma_threshold_pc);
                var prevPrice = tradeEngine.Results[frame - emaUpDown.timeFrame * 2].Close;
                var currPrice = tradeEngine.currentDataItem.Close;

                return 
                    (prevPrice > emaAdjustedPrice && currPrice <= emaAdjustedPrice) ||
                    (prevPrice <= emaAdjustedPrice && currPrice > emaAdjustedPrice);  
            }
            catch
            {
            }
            return false;
        }
        #endregion

        #region Draw
        /*
        public override void Draw(DataContext db)
        {
            if (MainObj.IsBacktesting)
            {
          //      return;
            }
            //can only draw one line here, as the base class will re-init the line list count and then reuse line segments which will already be added to canvas 
            //DrawLineOverlayChart(ema_60.ma, 1, Brushes.Gray);
            //DrawLineOverlayChart(ema_60.ma, 2, Brushes.Blue, true); - set param to true, to draw more than one line

            int startFrame = Chart.ScrollBars;
            int numFrames = Chart.ScaleBars;

            InitDraw();

            //todo - rework to draw on the chart canvas, not using the ancilliary canvases now
            for (int i = 0; i < indicators.Count; i++)
            {
                canvas = Chart.canvas;

                List<double> items1 = emaUpDown.GetResultsLarge(startFrame, numFrames);
                List<double> items2 = emaUpDown.GetResultsSmall(startFrame, numFrames);

                #region set the scaling to exactly fill the panel

                Low = Chart.Low;
                High = Chart.High;

                SetScaling();

                #endregion


                Point start = new Point();
                Point end = new Point();

                void DrawEMAColored(EMA emaLarge, List<double> itemsLarge, EMA emaSmall, List<double> itemsSmall, int lineWidth)
                {
                    int count = 0;

                    //need the actual start frame in the visible timeframe, which is available from GetFrameCount()
                    int frame = startFrame * MainObj.TimeFrame;
                    int _numFrames = numFrames;
                    foreach (double val in itemsLarge) 
                    {
                        if (frame < 2)
                        {
                            continue;
                        }
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

                        //get a suitable color
                        bool _isRising60 = MovingAverage.IsRising(emaUpDown.emaLarge, frame, emaUpDown.timeFrame);
                        bool _isAbove60 = MovingAverage.IsAbove(emaUpDown.emaSmall, emaUpDown.emaLarge, frame, emaUpDown.timeFrame);

                        SolidColorBrush color = _isAbove60 && _isRising60 ? colorUp : colorDown;

                        //this method basically replicates PanelBase.DrawLineOverlayChart, which is in visible time scale.
                        frame += TimeFrame;

                        DrawLineSegment(start, end, lineWidth, color);
                        count++;
                    }

                }
                void DrawEMA(EMA ema, List<double> items, int lineWidth, SolidColorBrush color)
                {
                    int count = 0;

                    //need the actual start frame in the visible timeframe, which is available from GetFrameCount()
                    int frame = startFrame * TimeFrame;
                    int _numFrames = numFrames;
                    foreach (double val in items) // emaLarge.GetResult(startFrame, numFrames))
                    {
                        if (frame < 2)
                        {
                            continue;
                        }
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

                        frame += TimeFrame;

                        DrawLineSegment(start, end, lineWidth, color);
                        count++;
                    }
                }

                DrawEMAColored(emaUpDown.emaLarge, items1, emaUpDown.emaSmall, items2, 3);
                //DrawEMA(emaUpDown_60.emaSmall, items2, 1, Brushes.Black);
            }
        }
        */
        #endregion

        #region Unused
        /*
        protected override bool HasCrossedEntryPoint(SymbolDataItem item, DataContext db)
        {
#if !BACKTEST
            if (tradeEngine.dataReady == false)
            {
                return false;
            }
#endif

            if (ActiveOrder != null && ActiveOrder.Position == TradePosition.LONG
                              ? item.Close > ActiveOrder.ActionValue
                              : item.Close < ActiveOrder.ActionValue)
            {
                //previous bar MUST close below the order price, otherwise an interruption or other trade close will start trades which are past actual entry time
                var frame = chart.GetFrameForTimestamp(item.Timestamp); //because a backtest trade can be at any point
                if (frame > tradeEngine.results.Count)
                {
                    return false; //this would happen if a trade was set after startup but before interpolation of missed frames. However, there is also a flag in tradeEngine to block this
                }
                int minTime = 60 * TradeEngine.MIN_SMA_ENTRY_BARS;
                var prevItem = tradeEngine.Results[frame - minTime];
                var ret = ActiveOrder.Position == TradePosition.LONG
                    ? prevItem.Open <= ActiveOrder.ActionValue
                    : prevItem.Open >= ActiveOrder.ActionValue;

                if (!ret)
                {
                    //close the order at this point, the entry position has moved on past the immediate break point
                    LogTrades.WriteLine(string.Format("Trade {0} was past entry point, closed at {1}", ActiveOrder.Id, item.Timestamp));
                    tradeEngine.ClosePendingTrade(db);
                }
                   
                return ret;
            }
            return false;
        }
        */
        #endregion
    }
}
