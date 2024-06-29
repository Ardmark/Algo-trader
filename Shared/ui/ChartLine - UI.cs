using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Forms;
using static BitMEX.BitMEXApi;

namespace AlgoTrader.Shared
{
    /*
     * All methods only called from mainViewModel, in response to UI input
     * */
    public partial class _ChartLine : UIDrawingBase //as opposed to line, which is a WPF object
    {

        #region Mouse

        //cursor exits line when dragging, so chart will get the mouse up event instead of line, generally
        public void MouseUp_UI_synchronised()
        {

            var tradeEngine = MainObj.GetTradeEngineForSymbol(SymbolName);

            //var v = tradeEngine.chart.Results;
            //if (v != null)
            //{
            //    Debug.WriteLine("----------------------------");
            //    for (var i = v.Count - 1; i >= v.Count - 30; i--)
            //    {
            //        Debug.WriteLine(v[i].Timestamp);
            //    }
            //}

            //can't just use UILines here, because the data context they are derived from may be disposed.
            //so instead, need to get all lines with trades
            //If using UILines (persistant data context in TradeEngine) would need to Include Trades as well, not so good for performance
            // - so this may be a better appropach anyway
            if (StartTimeStamp > EndTimeStamp)
            {
                AlertDialog.Show("Start of line must be before end. Try a shorter timeframe.");
                tradeEngine.InitForDataContext(() => 
                {
                    if (Id > 0)
                    {
                        var chartLine = tradeEngine.AllLinesForSymbol.FirstOrDefault(x => x.Id == Id);
                        if (chartLine != null)
                        {
                            StartTimeStamp = chartLine.StartTimeStamp;
                            EndTimeStamp = chartLine.EndTimeStamp;
                            StartPrice = chartLine.StartPrice;
                            EndPrice = chartLine.EndPrice;
                        }
                    }
                });
            }


            tradeEngine.InitForDataContext(() => //need the Entry property set
            {
                movingLine = null;

                isDraggingEnd = isDraggingStart = false;

                //move stop price and time after line move completed
                if (Id > 0)
                {
                    //tradeEngine.Lines is only for the active trade
                    //tradeEngine.UILInes does not Include Trades
                    //so need a new lines list

                    var chartLine = tradeEngine.AllLinesForSymbol.FirstOrDefault(x => x.Id == Id);
                    if (chartLine != null)
                    {
                        //bail if trade is active and line has the order attached
                        if (chartLine.Trade != null && chartLine.Trade.Action == TradeAction.ORDER)
                        {
                            var entry = tradeEngine.GetTradeForTradeReference(TradeAction.ENTRY, chartLine.Trade.TradeReference);
                            if (entry != null)
                            {
                                return;
                            }
                        }

                        //scaling bug will move candles and trade across if the trade is beyond the candle bounds.
                        //This is not an issue if only an empty line is drawn
                        //this will show as the line starting before the last candle, with the trade being apparently disconnected
                        // - but the timestamp for the last candle will appear to be wrong as the candles are shifted across
                        //Fix by not allowing the trade to go that far, by moving the line start and end back to just before the last candle
                        var lastItemTime = tradeEngine.Results.Last().Timestamp;
                        if (StartTimeStamp > lastItemTime)
                        {
                            var diff1 = lastItemTime.Subtract(StartTimeStamp).TotalMinutes - 1;
                            StartTimeStamp = StartTimeStamp.AddMinutes((int)diff1);
                            EndTimeStamp = EndTimeStamp.AddMinutes((int)diff1);
                            if (chartLine.Trade != null)
                            {
                                chartLine.Trade.Timestamp = StartTimeStamp;
                            }
                        }

                        var lineCopy = new _ChartLine();
                        chartLine.CopyTo(lineCopy);
                        chartLine.StartPrice = StartPrice;
                        chartLine.EndPrice = EndPrice;
                        chartLine.StartTimeStamp = StartTimeStamp;
                        chartLine.EndTimeStamp = EndTimeStamp;

                        //set start always before end
                        if (chartLine.StartTimeStamp > chartLine.EndTimeStamp)
                        {
                            var v1 = chartLine.StartTimeStamp;
                            var v2 = chartLine.StartPrice;
                            chartLine.StartTimeStamp = chartLine.EndTimeStamp;
                            chartLine.StartPrice = chartLine.EndPrice;
                            chartLine.EndTimeStamp = v1;
                            chartLine.EndPrice = v2;
                        }


                        tradeEngine.SetUpdateRequest(chartLine);//, TradeUpdateRequest.UPDATE);

                        //SetMinimumLength(tradeEngine.TimeFrame);

                        var diff = chartLine.EndTimeStamp - chartLine.StartTimeStamp;
                        //need to show the line as a minimum of one frame length, on the current timeframe
                        if (diff.TotalMinutes < tradeEngine.TimeFrame)
                        {
                            chartLine.EndTimeStamp = StartTimeStamp.AddMinutes(tradeEngine.TimeFrame);
                        }

                        if (chartLine.Trade != null)
                        {
                            var order = tradeEngine.GetTradeForTradeReference(TradeAction.ORDER, chartLine.Trade.TradeReference);
                            var entry = tradeEngine.GetTradeForTradeReference(TradeAction.ENTRY, chartLine.Trade.TradeReference);

                            if (order != null)
                            {
                                var orderLine = tradeEngine.AllLinesForSymbol.FirstOrDefault(x => x.TradeId == order.Id);

                                bool canSet = true;
                                if (chartLine.Trade.Action == TradeAction.ORDER && entry == null)
                                {
                                    if (tradeEngine.currentDataItem != null)
                                    {
                                        canSet = true; //order can be set above or below current price

                                        //price must be above current close
                                        //(var price, _) = chartLine.GetPriceAtTime(tradeEngine.currentDataItem.Timestamp, true);
                                        //var v = MainObj.GetPriceDiff(price, tradeEngine.currentDataItem.Close, order.Position) * 100;
                                        //canSet = v > TradeEngine.MIN_ORDER_THRESHOLD;

                                        //if (canSet)
                                        //{
                                        //price must be above or below floor, ceiling etc
                                        var floor = tradeEngine.GetTradeForTradeReference(TradeAction.FLOOR, chartLine.Trade.TradeReference);
                                        if (floor != null)
                                        {
                                            canSet = tradeEngine.CanSetTrade(orderLine, null, floor.Price, TradeAction.FLOOR, entry, order.Position);
                                        }
                                        if (canSet)
                                        {
                                            var ceiling = tradeEngine.GetTradeForTradeReference(TradeAction.CEILING, chartLine.Trade.TradeReference);
                                            if (ceiling != null)
                                            {
                                                canSet = tradeEngine.CanSetTrade(orderLine, null, ceiling.Price, TradeAction.CEILING, entry, order.Position);
                                            }

                                            if (canSet)
                                            {
                                                //price must be above or below stop, etc
                                                var stop = tradeEngine.GetTradeForTradeReference(TradeAction.STOP, chartLine.Trade.TradeReference);
                                                if (stop != null)
                                                {
                                                    var stopLine = tradeEngine.AllLinesForSymbol.FirstOrDefault(x => x.TradeId == stop.Id);
                                                    if (stopLine != null)
                                                    {
                                                        var pos = stop.Type == TradeType.STOP_WAITING
                                                            ? order.Position == TradePosition.LONG ? TradePosition.SHORT : TradePosition.LONG
                                                            : order.Position;
                                                        canSet = tradeEngine.CanSetTrade(orderLine, stopLine, 0, TradeAction.STOP, entry, pos);
                                                    }
                                                }

                                                if (canSet)
                                                {
                                                    //price must be above or below stop, etc
                                                    var takeProfit = tradeEngine.GetTradeForTradeReference(TradeAction.TAKE_PROFIT, chartLine.Trade.TradeReference);
                                                    if (takeProfit != null)
                                                    {
                                                        var tpLine = tradeEngine.AllLinesForSymbol.FirstOrDefault(x => x.TradeId == takeProfit.Id);
                                                        if (tpLine != null)
                                                        {
                                                            canSet = tradeEngine.CanSetTrade(orderLine, tpLine, 0, TradeAction.TAKE_PROFIT, entry, order.Position);
                                                        }
                                                    }
                                                }
                                            }
                                            //}
                                        }
                                    }
                                }
                                else
                                {
                                    canSet = tradeEngine.CanSetTrade(orderLine, chartLine, 0, chartLine.Trade.Action, entry, order.Position); 
                                }
                                if (chartLine.Trade.Type == TradeType.STOP_WAITING)
                                {
                                    canSet = true;
                                }
                                if (MainObj.BACKTEST)
                                {
                                    canSet = true;
                                }
                                if (!canSet)
                                {
                                    AlertDialog.Show($"Order entry, stop or take profit would be too close to, or would trigger, terminating trade.", $"Position not valid", true);
                                    lineCopy.CopyTo(chartLine);
                                    return;
                                }
                            }

                            tradeEngine.LineUpTrades_UI_Synchronised(chartLine.Trade, chartLine);
                            tradeEngine.currentTradeSetReference = chartLine.Trade.TradeReference;
                            MainObj.DrawCurrentDispatcherDelayed();
                        }
                    }
                }
                
                if (Id == 0)
                {
                    //line is not in DB and is being drawn
                    var id = tradeEngine.SetUpdateRequestAdd_UI(this);
                    line.Name = "_" + Id.ToString(); //cannot start with a number
                    tradeEngine.SetUpdateRequest(this);//, TradeUpdateRequest.UPDATE);
                }
            });

           // MainObj.DrawImmediate();
        }


        #endregion

        #region Draw

        public void Draw_UI(Chart chart)
        {
            try
            {
                //SetMinimumLength(chart.TimeFrame);

                (line.X1, line.Y1) = chart.GetDrawPosition1(StartTimeStamp, StartPrice);
                (line.X2, line.Y2) = chart.GetDrawPosition1(EndTimeStamp, EndPrice);

                if (!chart.canvas.Children.Contains(line))
                {
                    chart.canvas.Children.Add(line);
                }
            }
            catch
            {

            }
        }

        #endregion

        #region Move

        public void MoveLine_UI(double oldMouseX, double newMouseX, double oldMouseY, double newMouseY, bool shiftDown, bool ctrlDown, Chart chart)
        {
            const double endDistToDrag = 20;

            if (Trade != null)
            {

                //trade may be order or stop. If order and has entered, cannot move line
                if (Trade.Action == TradeAction.ORDER && Trade.ItemStatus == TradeItemStatus.CLOSED)
                {
                    return;
                }

                //block stop movement above current price, with a small margin of 1% (or would just close straight away if on exact price anyway)
                //if (Trade.Action == TradeAction.STOP)
                //{
                //    var tradeEngine = MainObj.GetTradeEngineForSymbol(Trade.SymbolName);
                //    if (tradeEngine.currentDataItem != null)
                //    {
                //        var entry = tradeEngine.UI_Trades.FirstOrDefault(x => x.Action == TradeAction.ENTRY && x.TradeReference == Trade.TradeReference);
                //        if (entry != null && entry.GlobalStatus == TradeGlobalStatus.ACTIVE)
                //        {
                //            //this only applies to live orders
                //            (var currLinePrice, _) = GetPriceAtTime(tradeEngine.currentDataItem.Timestamp, true);

                //            bool not_ok = Trade.Position == TradePosition.LONG
                //                ? currLinePrice > tradeEngine.currentDataItem.Close
                //                : currLinePrice < tradeEngine.currentDataItem.Close;
                //            if (not_ok)
                //            {
                //                //locks trade to the price, cannot move
                //                AlertDialog.Show("Cannot move stop above current price!");
                //                return;
                //            }
                //        }   
                //    }
                //}
            }

            var deltaX = newMouseX - oldMouseX;
            var deltaY = newMouseY - oldMouseY;

            if (isDraggingEnd)
            {
                line.X2 += deltaX;
                line.Y2 += deltaY;
                if (shiftDown)
                {
                    line.Y2 = line.Y1;
                }
            }
            else if (isDraggingStart)
            {
                line.X1 += deltaX;
                line.Y1 += deltaY;
                if (shiftDown)
                {
                    line.Y1 = line.Y2;
                }
            }
            else
            {
                //test for drag starting
                isDraggingStart = Math.Sqrt((newMouseX - line.X1) * (newMouseX - line.X1) + (newMouseY - line.Y1) * (newMouseY - line.Y1)) < endDistToDrag;
                isDraggingEnd = Math.Sqrt((newMouseX - line.X2) * (newMouseX - line.X2) + (newMouseY - line.Y2) * (newMouseY - line.Y2)) < endDistToDrag;

                if (isDraggingEnd && isDraggingStart)
                {
                    isDraggingStart = false; //no need for both
                }
                if (!(isDraggingStart || isDraggingEnd))
                {
                    line.X1 += deltaX;
                    line.Y1 += deltaY;
                    line.X2 += deltaX;
                    line.Y2 += deltaY;
                }
            }

            if (line.X1 == 0)
            {
                return;
            }
          
            var frame = chart.GetFrameNumFromMouseX(line.X1); //frame matches display output.
            int barNum = (int)(line.X1 / MainObj.chart_Canvas.ActualWidth * Chart.ScaleBars) + chart.ScrollBars;
                       
            //this matches screen output
            //var dateTime = barNum < Chart.items_chart.Count ? Chart.items_chart[barNum].Timestamp : Chart.items_chart.Last().Timestamp.AddMinutes(chart.TimeFrame * numExtraMins);

            (var _StartTimeStamp, _) = chart.GetTimeAndPrice(line.X1, line.Y1);

            var firstTime = chart.Results.First().Timestamp;
            var frameTime = firstTime.AddMinutes(frame);

            //this does not match the above
            (StartTimeStamp, StartPrice) = chart.GetTimeAndPrice(line.X1, line.Y1);
            (EndTimeStamp, EndPrice) = chart.GetTimeAndPrice(line.X2, line.Y2);

          //  SetMinimumLength(chart.TimeFrame);
        }

        #endregion

    }
}
