using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using AlgoTrader.Shared.TradeIndicators;
using AlgoTrader.Shared.Windows;
using Shared.Trading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using static BitMEX.BitMEXApi;

namespace AlgoTrader.Shared
{
    /*
     * All methods only called from mainViewModel, in response to UI input
     * */
    public partial class TradeEngine : DisplayPanel
    {

        #region MainViewModel-invoked methods

        //there is an unused method in MainObj to kill all orders on all trade engines simultaneously,
        // this will just remove the orders on the current trade engine
        internal void KillOrders_UI_Synchronised()
        {
            CancelTrade_UI_synchronised();
            MainObj.TradeStatusChanged(this);
        }
        internal void MoveStops_UI_Synchronised(bool up)
        {
            var pc = TradeEngine.MOVE_STOPS_AMT / 100 + 1;

            InitForDataContext(() =>
            {
                void MoveStop(_Trade trade)
                {
                    if (trade != null)
                    {
                        var line = AllLinesForSymbol.FirstOrDefault(x => x.TradeId == trade.Id);
                        if (line != null)
                        {
                            line.StartPrice = up ? line.StartPrice * pc : line.StartPrice / pc;
                            line.EndPrice = up ? line.EndPrice * pc : line.EndPrice / pc;
                            SetUpdateRequest(line);//, TradeUpdateRequest.UPDATE);
                        }
                        //stop will eventually track line but want immediate update:
                        trade.Price = up ? trade.Price * pc : trade.Price / pc;

                        //will only set stop on exchange if it is a Stop or if it is ENTERING_ON_EXCHANGE, this is so the stop will always update on each loop
                        // but the floor is bypassed except when it is first set. The status is reset afterwards anyway.
                        //NOTE - Ceiling and TakeProfit is undefined for this behaviour.
                        if (trade.Action == TradeAction.STOP || trade.Action == TradeAction.FLOOR)
                        {
                            trade.ExchangeStatus = TradeExchangeStatus.ENTERING_ON_EXCHANGE;
                            SetStopOnExchange_Synchronised(trade);
                        }

                    }

                }
                MoveStop(Stop);
                MoveStop(Floor);
                MoveStop(Ceiling);
                MoveStop(TakeProfit);

            });
            MainObj.DrawImmediate();

        }
        internal void CleanupTrades_UI_Synchronised()
        {
            InitForDataContext(() =>
            {
                foreach (var trade in AllTrades)
                {
                    if (trade.ItemStatus == TradeItemStatus.CLOSED && trade.GlobalStatus == TradeGlobalStatus.STOPPED)
                    {
                        if (trade.Action == TradeAction.ORDER)
                        {
                            var line = GetLinesForTradeReference(trade.TradeReference).FirstOrDefault();
                            if (line != null)
                            {
                                SetUpdateRequest(line, TradeUpdateRequest.DELETE);
                            }
                        }
                        if (
                            trade.ExchangeStatus == TradeExchangeStatus.NEVER_ENTERED || 
                            (trade.Action != TradeAction.ORDER && trade.Action != TradeAction.ENTRY && trade.Action != TradeAction.CLOSE)
                            )
                        {
                            SetUpdateRequest(trade, TradeUpdateRequest.DELETE);
                        }
    
                    }
                }
            });

            MainObj.DrawImmediate();
        }
        internal void CleanupLines_UI_Synchronised()
        {
            InitForDataContext(() =>
            {
                var linesWithoutTrades = DataContext.Lines.Where(x =>
                                               x.SymbolName == symbolName &&
                                               x.TradeReference == null
                                               ).ToList();
                var linesWithTrades = DataContext.Lines.Where(x =>
                                               x.SymbolName == symbolName &&
                                               x.TradeReference != null
                                               ).ToList();
                foreach (var line in linesWithTrades)
                {
                    //this will leave stop lines, since the stops would also need to be removed first
                    if (!DataContext.Trades.Any(x => x.Id == line.TradeId))
                    {
                        linesWithoutTrades.Add(line);
                    }
                }
                foreach (var line in linesWithoutTrades)
                {
                    SetUpdateRequest(line, TradeUpdateRequest.DELETE);
                }
            });

            MainObj.DrawImmediate();
        }

        internal void SetStopAtPrice_UI_synchronised(DateTime timeStamp, double price, TradeStop tradeStop)
        {
            SetStop_UI(timeStamp, price, TradeAction.STOP, TradeType.NO_FLIP_ORDER, tradeStop);
        }

        internal void SetFloorAtPrice_UI_synchronised(DateTime timeStamp, double price)
        {
            SetStop_UI(timeStamp, price, TradeAction.FLOOR, TradeType.NO_FLIP_ORDER);
        }

        internal void SetCeilingAtPrice_UI_synchronised(DateTime timeStamp, double price)
        {
            //Set Take Profit - this assumes no chart line, so sets that ID to -1.
            //If this is not done, then it will be cloned from the order and that will result in the line changing status if the TP is deleted
            SetStop_UI(timeStamp, price, TradeAction.CEILING, TradeType.NO_FLIP_ORDER);
        }

        internal void SetBreakEvenAtPrice_UI_synchronised(DateTime timeStamp, double price)
        {
            SetStop_UI(timeStamp, price, TradeAction.BREAK_EVEN, TradeType.NO_FLIP_ORDER);
        }
        internal void SetTakeProfitAtPercent_UI_synchronised(DateTime timeStamp, double price)
        {
            SetStop_UI(timeStamp, price, TradeAction.FLOOR, TradeType.NO_FLIP_ORDER, TradeStop.TAKE_PROFIT_PERCENT);
        }

        internal void SetMarketOrderAtTime_UI_synchronised(DateTime timeStamp, double price, TradePosition position)
        {
            if (TradeEngine.EXCHANGE == ExchangeId.BINANCE)
            {
                if (position == TradePosition.SHORT)
                {
                    AlertDialog.Show("Short orders not available on Binance", "Not available");
                    return;
                }
            }
            InitForDataContext(() =>
            {
                if (Order != null)
                {
                    AlertDialog.Show("Cannot set 2 active trades to run concurrently on same symbol", "Not possible");
                    return;
                }

                //must have at least 24 hours of data, or trade can be placed before first midnight boundary so will never be visible unless it closes the following day
                if (results.Count < 24 * 60)
                {
                    AlertDialog.Show("Could not set market break order, needs 24 hours data", "Insufficient data");
                    return;
                }

                var order = new _Trade
                {
                    Timestamp = timeStamp,
                    TradeReference = _Trade.GetNewTradeId(),
                    Type = TradeType.MARKET_ORDER,
                    Position = position,
                    Action = TradeAction.ORDER,
                    Price = price,
                    TimeFrame = Chart.TimeFrame,
                    SymbolName = symbolName,
                    ExchangeId = exchangeId,
                };

                SetUpdateRequest(order);//, TradeUpdateRequest.UPDATE);

                currentTradeSetReference = order.TradeReference;

                var currentTradeIndicator = GetCurrentTradeIndicator(order);

                //when a trade is changed, added or deleted, mark it with a TradeUpdateRequest and add to the trades list
                (bool hasEntered, bool tradeKilled) = EnterIfPossible_synchronised(order, null);
                if (tradeKilled)
                {
                    order.ItemStatus = TradeItemStatus.CLOSED;
                    order.GlobalStatus = TradeGlobalStatus.STOPPED;
                }
                if (hasEntered)
                {
                    Order = order;
                }
            });

            MainObj.TradeStatusChanged(this);
        }

        internal void AddBreakToLineLocal_UI_synchronised(int chartLineId, TradePosition position, double price, bool isBacktest)
        {
            if (currentDataItem == null)
            {
                AlertDialog.Show("Waiting for connection", "Not possible");
                return;
            }

            InitForDataContext(() =>
            {
                var chartLine = AllLinesForSymbol.FirstOrDefault(x => x.Id == chartLineId);

                if (chartLine.Trade != null)
                {
                    AlertDialog.Show("Trade already set on this line");
                    return;
                }

                Order = SetLineOrder_UI_synchronised(position, isBacktest, chartLine);

                currentTradeSetReference = Order.TradeReference;

                if (!isBacktest)
                {
                    EnterTradeOnSystem_synchronised((decimal)price, 0, TradeExchangeStatus.ENTERED_ON_EXCHANGE);
                    Order.ActionValue = Entry.ActionValue = 0; //mark as using zero equity, to keep this off the profit / loss calculations
                }

            });

            MainObj.TradeStatusChanged(this);
        }

        internal void SetMarketOrderLocal_UI_synchronised(double price, TradePosition position)
        {
            if (currentDataItem == null)
            {
                AlertDialog.Show("Waiting for connection", "Not possible");
                return;
            }

            InitForDataContext(() =>
            {
                if (Order != null)
                {
                    AlertDialog.Show("Cannot set 2 active trades to run concurrently on same symbol", "Not possible");
                    return;
                }


                //must have at least 24 hours of data, or trade can be placed before first midnight boundary so will never be visible unless it closes the following day
                if (results.Count < 24 * 60)
                {
                    AlertDialog.Show("Could not set market break order, needs 24 hours data", "Insufficient data");
                    return;
                }

                Order = new _Trade
                {
                    Timestamp = DateTime.UtcNow,
                    TradeReference = _Trade.GetNewTradeId(),
                    Type = TradeType.MARKET_ORDER,
                    Position = position,
                    Action = TradeAction.ORDER,
                    Price = price,
                    TimeFrame = TimeFrame,
                    SymbolName = symbolName,
                    ExchangeId = exchangeId,
                    ItemStatus = TradeItemStatus.CLOSED
                };

                SetUpdateRequest(Order);//, TradeUpdateRequest.UPDATE);

                currentTradeSetReference = Order.TradeReference;

                EnterTradeOnSystem_synchronised((decimal)price, 0, TradeExchangeStatus.ENTERED_ON_EXCHANGE);

                Order.ActionValue = Entry.ActionValue = 0; //mark as using zero equity, to keep this off the profit / loss calculations
            });

            MainObj.TradeStatusChanged(this);
        }

        internal void SetLineBreakOrderClosed_UI_synchronised(DateTime timeStamp, double price, TradePosition position)
        {
            SetLineBreakOrder_UI_synchronised(timeStamp, price, position, true);
        }

        //sets a break order on a new horizontal line at a specific price
        internal void SetLineBreakOrder_UI_synchronised(DateTime timeStamp, double price, TradePosition position, bool addClosed = false, bool isBacktest = false)
        {
            if (currentDataItem == null)
            {
                AlertDialog.Show("Waiting for connection", "Not possible");
                return;
            }
            InitForDataContext(() =>
            {
                //ensure break is above current price
                //var frame = chart.GetFrameForTimestamp(currentDataItem.Timestamp);
                var currentPrice = currentDataItem.Close;
                bool canSet = true;
                if (position == TradePosition.LONG && price < currentPrice ||
                    position == TradePosition.SHORT && price > currentPrice)
                {
                    canSet = false;
                }

                if (!MainObj.BACKTEST && !canSet)
                {
                    var m = position == TradePosition.LONG ? "below" : "above";
                    AlertDialog.Show($"Requested position is {m} current price ", "Order position not valid");
                    return;
                }

                timeStamp = DateTime.UtcNow.AddMinutes(-1);


                var nextEarliest = MainObj.GetNextInterval_UI(timeStamp, currentDataItem);
                var line = new _ChartLine
                {
                    StartTimeStamp = timeStamp,
                    EndTimeStamp = nextEarliest,
                    StartPrice = price,
                    EndPrice = price,
                    SymbolName = symbolName
                };

                SetUpdateRequest(line);//, TradeUpdateRequest.UPDATE);

                _Trade trade = SetLineOrder_UI_synchronised(position, isBacktest, line, addClosed);
            });

            MainObj.TradeStatusChanged(this);
        }

        internal void SetHMABreakOrder_UI_synchronised(DateTime timeStamp, double price)
        {
            if (hmaPriceBreakOrderIndicator == null)
            {
                AlertDialog.Show("No HMA indicator!", "Not possible");
                return;
            }
            if (currentDataItem == null)
            {
                AlertDialog.Show("Waiting for connection", "Not possible");
                return;
            }

            InitForDataContext(() =>
            {
                //ensure break is above current price
                //var frame = chart.GetFrameForTimestamp(currentDataItem.Timestamp);
                var currentPrice = currentDataItem.Close;
                if (!MainObj.IsBacktesting)
                {
                    timeStamp = DateTime.UtcNow.AddMinutes(-1);
                }

                var frame = chart.GetFrameForTimestamp(timeStamp); //because a backtest trade can be at any point, although backtesting not implemented on this order type

                if (currentDataItem != null)
                {
                    var order = hmaPriceBreakOrderIndicator.SetOrder_UI_synchronised(TimeFrame, TradeType.HMA_BREAK_ORDER, TradePosition.LONG, symbolName, false, currentDataItem.Close, frame);

                    if (order != null)
                    {
                        currentTradeSetReference = order.TradeReference;
                        SetUpdateRequest(order);//, TradeUpdateRequest.UPDATE);
                    }
                }
                else
                {
                    AlertDialog.Show($"Waiting for connection... ", "Cannot set trade");
                }
            });

            MainObj.TradeStatusChanged(this);
        }
        //sets an order which will enter on (hourly) macd histogram zero level cross up
        internal void SetMacdOrder_UI_synchronised(DateTime timeStamp)
        {
            if (macdPriceBreakOrderIndicator == null)
            {
                return;
            }
            if (currentDataItem == null)
            {
                AlertDialog.Show("Waiting for connection", "Not possible");
                return;
            }

            InitForDataContext(() =>
            {
                //ensure break is above current price
                //var frame = chart.GetFrameForTimestamp(currentDataItem.Timestamp);
                var currentPrice = currentDataItem.Close;
                if (!MainObj.IsBacktesting)
                {
                    timeStamp = DateTime.UtcNow.AddMinutes(-1);
                }

                var frame = chart.GetFrameForTimestamp(timeStamp); //because a backtest trade can be at any point, although backtesting not implemented on this order type

                if (currentDataItem != null)
                {
                    var order = macdPriceBreakOrderIndicator.SetOrder_UI_synchronised(TimeFrame, TradeType.HMA_BREAK_ORDER, TradePosition.LONG, symbolName, false, currentDataItem.Close, frame);

                    if (order != null)
                    {
                        currentTradeSetReference = order.TradeReference;
                        SetUpdateRequest(order);//, TradeUpdateRequest.UPDATE);
                    }
                }
                else
                {
                    AlertDialog.Show($"Waiting for connection... ", "Cannot set trade");
                }
            });

            MainObj.TradeStatusChanged(this);
        }

        internal void EditTradeSet_UI(_Trade trade)
        {
            currentTradeSetReference = trade.TradeReference;
        }

        internal void CloseTrade_UI_synchronised(int tradeId = 0)
        {
            CancelTrade_UI_synchronised(tradeId);
            MainObj.TradeStatusChanged(this);
        }

        internal void MarkAsSynched_UI_synchronised(int tradeId = 0)
        {
            InitForDataContext(() =>
            {
                var trade = AllTrades.FirstOrDefault(x => x.Id == tradeId);
                if (trade != null)
                {
                    var trades = AllTrades.Where(x => x.TradeReference == trade.TradeReference);
                    foreach (var t in trades)
                    {
                        t.ExchangeMethodScript = null;// "Exchange not synched";
                        SetUpdateRequest(t);//, TradeUpdateRequest.UPDATE);
                    }
                    MainObj.DrawCurrentDispatcherDelayed();
                }
            });

        }
        internal void RemoveTradeAndLine_UI_synchronised(int tradeId)
        {
            InitForDataContext(() =>
            {
                var trade = AllTrades.FirstOrDefault(x => x.Id == tradeId);
                if (trade == null)
                {
                    AlertDialog.Show("Could not find trade");
                    return;
                }

                RemoveTradeFromSystem_UI_synchronised(trade, true);

            });

            MainObj.TradeStatusChanged(this);
        }

        internal void RemoveTrade_UI_synchronised(int tradeId, bool allowForceRemove = false)
        {
            InitForDataContext(() =>
            {
                var trade = AllTrades.FirstOrDefault(x => x.Id == tradeId);

                if (trade == null)
                {
                    AlertDialog.Show("Could not find trade");
                    return;
                }

                RemoveTradeFromSystem_UI_synchronised(trade, false, allowForceRemove);
            });
            MainObj.TradeStatusChanged(this);
        }

        internal void SetMovingAverageStop_UI_synchronised()
        {

            //get the moving average
            double lastResult = 0;
            var MA_Indicator = MainObj.displayPanelsForSymbol[symbolName].FirstOrDefault(x => x.GetType() == typeof(SMA_Indicator));
            if (MA_Indicator != null)
            {
                //only one moving average
                var movingAverage = MA_Indicator.movingAverages[0];
                var results = movingAverage.GetResultsFromDictionary(TimeFrame);
                lastResult = results[results.Count - 1];
            }

            _Trade entry = null;
            _Trade _stop = null;
            //no need for a synchronisation here
            using (DataContext db = new DataContext())
            {
                _entry = db.Trades.FirstOrDefault(x =>
                           x.SymbolName == symbolName &&
                           x.GlobalStatus == TradeGlobalStatus.ACTIVE &&
                           x.ItemStatus == TradeItemStatus.OPEN &&
                           x.Action == TradeAction.ENTRY
                           );
                if (_entry != null)
                {

                    _stop = db.Trades.FirstOrDefault(x =>
                               x.TradeReference == _entry.TradeReference &&
                               x.GlobalStatus == TradeGlobalStatus.ACTIVE &&
                               x.ItemStatus == TradeItemStatus.OPEN &&
                               x.Action == TradeAction.STOP
                               );
                }
            }

            //cant synchronise this block as it contains SetStop_UI()
            if (_entry != null && currentDataItem != null) //only set on a live order
            {
                if (_entry.Position == TradePosition.LONG ? lastResult > currentDataItem.Close : lastResult < currentDataItem.Close)
                {
                    AlertDialog.Show("Moving average price is wrong side of order");
                    return;
                }
                //just easier to allow the loop to calculate the first actual price, than do it here...
                var initialPrice = lastResult != 0 ? lastResult : _entry.Position == TradePosition.LONG ? currentDataItem.Close / 1.1 : currentDataItem.Close * 1.1; 

                if (_stop == null)
                {
                    SetStop_UI(DateTime.UtcNow, initialPrice, TradeAction.STOP, TradeType.NO_FLIP_ORDER, TradeStop.TAKE_PROFIT_MA_CROSS);
                }
                else
                {
                    InitForDataContext(() =>
                    {
                        if (Stop.Id == _stop.Id)
                        {
                            Stop.Stop = TradeStop.TAKE_PROFIT_MA_CROSS;
                            var chartLine = AllLinesForSymbol.GetLineForTrade(Stop);
                            if (chartLine != null)
                            {
                                chartLine.TradeId = null;
                                chartLine.TradeReference = null;
                                chartLine.Type = LineType.NONE;
                                SetUpdateRequest(chartLine);//, TradeUpdateRequest.UPDATE);
                            }
                            SetUpdateRequest(Stop);//, TradeUpdateRequest.UPDATE);
                        }
                    });

                }
            }
            MainObj.TradeStatusChanged(this);
        }

        internal void DeleteLine_UI_synchronised(int chartLineId)
        {
            InitForDataContext(() =>
            {
                //need all the lines here
                var chartLine = AllLinesForSymbol.FirstOrDefault(x => x.Id == chartLineId);
               
                _Trade trade = chartLine.Trade;
                if (trade != null && trade.GlobalStatus == TradeGlobalStatus.ACTIVE)
                {
                    AlertDialog.Show("Cannot delete a line when active trade is attached");
                    return;
                }

                SetUpdateRequest(chartLine, TradeUpdateRequest.DELETE);
            });

            MainObj.TradeStatusChanged(this);
        }

        internal void RestartTrade_UI_synchronised(int tradeId)
        {
            //restart a trade which has been closed but not yet entered, and extend any line to the next interval (+24 hours)
            InitForDataContext(() =>
            {
                //get the trade from the current data context
                var trade = AllTrades.FirstOrDefault(x => x.Id == tradeId);

                //Order will be null at this point, must use trade
                if (trade == null)
                {
                    AlertDialog.Show("Could not find trade");
                    return;
                }

                var entry = AllTrades.FirstOrDefault(x => 
                    x.Action == TradeAction.ENTRY && 
                    x.TradeReference == trade.TradeReference &&
                    x.GlobalStatus == TradeGlobalStatus.ACTIVE);
                if (entry != null)
                {
                    AlertDialog.Show("Cannot restart a trade which has already entered");
                    return;
                }
                var allTrades = AllTrades.Where(x => x.TradeReference == trade.TradeReference);
                foreach (var t in allTrades)
                {
                    t.ItemStatus = TradeItemStatus.OPEN;
                    t.GlobalStatus = TradeGlobalStatus.ACTIVE;
                    SetUpdateRequest(t);//, TradeUpdateRequest.UPDATE);

                    if (t.Action == TradeAction.CLOSE)
                    {
                        SetUpdateRequest(t, TradeUpdateRequest.FORCE_DELETE);
                    }
                }

                //extend line - not actually necessary as this will be done on first run anyway
                var line = AllLinesForSymbol.GetLineForTrade(trade);
                if (line != null)
                {
                    if (line.EndTimeStamp < DateTime.Now.AddDays(1))
                    {
                        line.Extend(DateTime.UtcNow.AddDays(2), TimeFrame);
                    }
                    SetUpdateRequest(line);//, TradeUpdateRequest.UPDATE);
                }
                else
                {
                    line = new _ChartLine
                    {
                        StartTimeStamp = trade.Timestamp,
                        EndTimeStamp = trade.Timestamp.AddDays(1),
                        StartPrice = trade.Price,
                        EndPrice = trade.Price,
                        SymbolName = symbolName,
                        Trade = trade,
                        TradeReference = trade.TradeReference,
                        Type = LineType.BREAK
                    };

                    SetUpdateRequest(line);//, TradeUpdateRequest.UPDATE);

                }
            });

            MainObj.TradeStatusChanged(this);
        }

        private void SetStop_UI(DateTime timeStamp, double price, TradeAction tradeAction, TradeType tradeType = TradeType.NO_FLIP_ORDER, TradeStop tradeStop = TradeStop.STOP_ON_LINE_BREAK, _ChartLine chartLine = null)
        {

            var isWaitingStop = tradeAction == TradeAction.WAITING_STOP;

            if (TradeEngine.EXCHANGE == ExchangeId.BINANCE)
            {
                if (tradeType == TradeType.FLIP_ORDER || isWaitingStop)
                {
                    AlertDialog.Show("Flip orders not available on Binance", "Not available");
                    return;
                }
            }

            var order = UI_Trades.GetTradeForTradeReference(TradeAction.ORDER, currentTradeSetReference);

            if (order == null)
            {
                AlertDialog.Show("No trade matches current trade set reference", "Could not set trade");
                return;
            }

            //ensure stop is below (or above) current price
            var entry = UI_Trades.GetTradeForTradeReference(TradeAction.ENTRY, currentTradeSetReference);

            if (tradeType == TradeType.STOP_WAITING)
            {
                var v = UI_Trades.FirstOrDefault(x => 
                            x.Action == TradeAction.STOP && 
                            x.Type == TradeType.FLIP_ORDER &&
                            x.Position == entry.Position &&
                            x.TradeReference == entry.TradeReference);
                if (v == null)
                {
                    AlertDialog.Show("Cannot place a Flip Order Stop without a flip order!");
                    return;
                }
            }

            if (tradeStop == TradeStop.TAKE_PROFIT_PERCENT)
            {
                if (SET_TAKE_PROFIT_THRESHOLD_PC <= 0)
                {
                    AlertDialog.Show($"SET_BREAK_EVEN_THRESHOLD_PC not set");
                    return;
                }
                //can this order be placed?
                var diff = MainObj.GetPriceDiffPercent(currentDataItem, entry.Price, entry.Position) * 100;
                if (diff < SET_TAKE_PROFIT_THRESHOLD_PC)
                {
                    AlertDialog.Show($"Need at least {SET_TAKE_PROFIT_THRESHOLD_PC}% to set a Take Profit At % order");
                    return;
                }

                //this is recalculated in Order on each update, but won't go lower
                price = AlgoTrader.Shared.Order.GetTakeProfitAtPC_Price(entry, currentDataItem.Close);
            }


            //for a flip stop, the orderLine will be the line with the flip stop set on it
            var orderLine = isWaitingStop
                ? UI_Lines.GetLineForTrade(UI_Trades.FirstOrDefault(x => x.TradeReference == order.TradeReference && x.Action == TradeAction.STOP && x.Type == TradeType.FLIP_ORDER))
                : UI_Lines.GetLineForTrade(order);

            void SetStop()
            {
                InitForDataContext(() =>
                {
                    if (chartLine != null)
                    {
                        //get the line for the current data context, the chartLine parameter will not track in EF
                        chartLine = AllLinesForSymbol.FirstOrDefault(x => x.Id == chartLine.Id);

                        if (chartLine.Trade != null)
                        {
                            return;
                        }
                    }
                    ////////////////////////
                    //Set the stop

                       //if this is a trailing stop, need the action value
                    double actionValue = tradeStop == TradeStop.STOP_TRAIL ? MainObj.GetPriceDiffPercent(currentDataItem, price, order.Position) : 0;

                    _Trade trade = GetTradeForTradeReference(tradeAction);
                    if (trade == null)
                    {
                        //add a new stop derived from the trade
                        trade = order.Clone(); //this should only happen if a stop has been deleted completely
                        trade.ItemId = null; //because Bitmex will have a new ID for the stop
                        SetUpdateRequest(trade);//, TradeUpdateRequest.UPDATE);
                    }
                    else
                    {
                        //if existing stop is a line stop, just remove the line attachment
                        RemoveTradeFromLine_UI_synchronised(trade); 
                    }

                    trade.ItemStatus = TradeItemStatus.OPEN;
                    trade.Stop = tradeStop;
                    trade.Action = tradeAction;
                    trade.ActionValue = actionValue;
                    trade.Timestamp = timeStamp;
                    trade.TimeFrame = TimeFrame;

                    //this may be an existing Moving Average stop, if so just changing the Type will stop the moving average update loop (see Order::ExecuteStop_synchronised())
                    trade.Type = tradeType;
                    //flip side if this is a waiting stop
                    trade.Quantity = isWaitingStop ? order.Quantity : -order.Quantity;
                    trade.Position = isWaitingStop ? order.Position == TradePosition.LONG ? TradePosition.SHORT : TradePosition.LONG : order.Position;

                    switch (trade.Action)
                    {
                        case TradeAction.STOP:
                        case TradeAction.FLOOR:
                            trade.ExchangeStatus = TradeExchangeStatus.ENTERING_ON_EXCHANGE;
                            break;
                        default:
                            trade.ExchangeStatus = TradeExchangeStatus.ENTERED_OFF_EXCHANGE; //will always be this, to allow for removal at any time
                            break;
                    }

                    //set stop on exchange
                    LineUpTrades_UI_Synchronised(trade, chartLine); //this will set the stop or floor on the exchange. It is also called when moving a line, if the stop is not set on the exchange then one is created there.

                    trade.Price = price; //because the above method will use the old attached line if the trade has moved to a new line

                    //set stop on exchange
                    bool isEntered = order.ItemStatus == TradeItemStatus.CLOSED && order.GlobalStatus == TradeGlobalStatus.ACTIVE;
                    if (isEntered)
                    {
                        SetStopOnExchange_Synchronised(trade);
                    }
                    SetUpdateRequest(trade);//, TradeUpdateRequest.UPDATE);

                    if (IsActiveTrade(order))
                    {
                        switch (tradeAction)
                        {
                            //waiting stops will do nothing here, obviously
                            case TradeAction.STOP:
                                Stop = trade;
                                break;
                            case TradeAction.FLOOR:
                                Floor = trade;
                                break;
                            case TradeAction.CEILING:
                                Ceiling = trade;
                                break;
                            case TradeAction.TAKE_PROFIT:
                                TakeProfit = trade;
                                break;
                            case TradeAction.BREAK_EVEN:
                                BreakEven = trade;
                                break;
                        }
                    }



                    if (chartLine != null)
                    {
                        chartLine.TradeReference = trade.TradeReference;
                        chartLine.TradeId = trade.Id; //in case lines have switched
                        SetUpdateRequest(chartLine);//, TradeUpdateRequest.UPDATE);

                        chartLine.Type = LineType.NONE;
                        switch (tradeAction)
                        {
                            case TradeAction.STOP:
                            case TradeAction.WAITING_STOP:
                            case TradeAction.FLOOR:
                                chartLine.Type = LineType.SUPPORT;
                                break;
                            case TradeAction.CEILING:
                            case TradeAction.TAKE_PROFIT:
                            case TradeAction.BREAK_EVEN:
                                chartLine.Type = LineType.RESISTANCE;
                                break;
                        }
                        tradeEngine.SetUpdateRequest(chartLine);//, TradeUpdateRequest.UPDATE);
                    }

                });


                MainObj.DrawCurrentDispatcherDelayed();
                MainObj.TradeStatusChanged(this);
            }

            var pos = isWaitingStop
                    ? order.Position == TradePosition.LONG ? TradePosition.SHORT : TradePosition.LONG
                    : order.Position;

            if (!CanSetTrade(orderLine, chartLine, price, tradeAction, entry, pos))
            {
                var m = tradeAction == TradeAction.CEILING || tradeAction == TradeAction.TAKE_PROFIT || tradeAction == TradeAction.BREAK_EVEN
                    ? order.Position == TradePosition.LONG ? "below" : "above"
                    : order.Position == TradePosition.LONG ? "above" : "below";

                AlertDialog.ShowCallback($"Requested position is either too close to, or wrong side of, Order, and will trigger immediate exit.\nDo you wish to procede? ", $"Terminating trade position not valid", () =>
                {
                    SetStop();
                });
            }
            else
            {
                SetStop();
            }
        }

        internal void AddStopToLine_UI(int chartLineId, TradeType tradeType)
        {
            var chartLine = UI_Lines.FirstOrDefault(x => x.Id == chartLineId);
            if (chartLine.Trade != null && (chartLine.Trade.Action != TradeAction.STOP || chartLine.Trade.Action == TradeAction.WAITING_STOP) )
            {
                AlertDialog.Show("Trade already set on this line");
                return;
            }

            var order = UI_Trades.GetTradeForTradeReference(TradeAction.ORDER, currentTradeSetReference);

            if (order == null)
            {
                AlertDialog.Show("Could not set trade", "No trade matches current trade set reference");
                return;
            }

            (var price, var isWithinLine) = chartLine.GetPriceAtTime(order.Timestamp, true);
            
            TradeAction tradeAction;
            switch (tradeType)
            {
                case TradeType.FLIP_ORDER:
                case TradeType.NO_FLIP_ORDER:
                    tradeAction = TradeAction.STOP; break;

                case TradeType.STOP_WAITING:
                    tradeAction = TradeAction.WAITING_STOP; break;

                default: tradeAction = TradeAction.STOP; break;

            }
            SetStop_UI(order.Timestamp, price, tradeAction, tradeType, 0, chartLine);
        }

        internal void AddTakeProfitToLine_UI(int chartLineId, TradeType tradeType)
        {
            var chartLine = UI_Lines.FirstOrDefault(x => x.Id == chartLineId);
            if (chartLine.Trade != null && chartLine.Trade.Action != TradeAction.TAKE_PROFIT)
            {
                AlertDialog.Show("Trade already set on this line");
                return;
            }

            var order = UI_Trades.GetTradeForTradeReference(TradeAction.ORDER, currentTradeSetReference);

            if (order == null)
            {
                AlertDialog.Show("Could not set trade", "No trade matches current trade set reference");
                return;
            }

            (var price, var isWithinLine) = chartLine.GetPriceAtTime(order.Timestamp, true);
            SetStop_UI(order.Timestamp, price, TradeAction.TAKE_PROFIT, tradeType, 0, chartLine);
        }

        internal void AddBreakToLine_UI_synchronised(int chartLineId, TradePosition position)
        {
            if (TradeEngine.EXCHANGE == ExchangeId.BINANCE)
            {
                if (position == TradePosition.SHORT)
                {
                    AlertDialog.Show("Short orders not available on Binance", "Not available");
                    return;
                }
            }
            InitForDataContext(() =>
            {
                var chartLine = AllLinesForSymbol.FirstOrDefault(x => x.Id == chartLineId);

                if (chartLine.Trade != null)
                {
                    AlertDialog.Show("Trade already set on this line");
                    return;
                }

                SetLineOrder_UI_synchronised(position, false, chartLine);

            });

            MainObj.TradeStatusChanged(this);
        }

        public void ChangePrice_UI_Synchronised(_Trade _trade, double price)
        {
            AlertDialog.Show("Unimplemented, needs to be changed to allow reversed position for waiting stop");
            return;
            void ChangePrice()
            {
                InitForDataContext(() =>
                {
                    var trade = AllTrades.FirstOrDefault(x => x.Id == _trade.Id);
                    if (trade != null)
                    {
                        trade.Price = price;
                        tradeEngine.SetUpdateRequest(trade);//, TradeUpdateRequest.UPDATE);

                        var chartLine = AllLinesForSymbol.FirstOrDefault(x => x.TradeId == trade.Id);

                        if (chartLine != null)
                        {
                            //get the line for the current data context, the chartLine parameter will not track in EF
                            chartLine.StartPrice = chartLine.EndPrice = price;
                            tradeEngine.SetUpdateRequest(chartLine);//, TradeUpdateRequest.UPDATE);
                        }
                        MainObj.DrawCurrentDispatcher();
                        MainObj.TradeStatusChanged(this);
                    }
                });
            }
        }

        public void ChangeTimeframe_UI_Synchronised(_Trade _trade, int timeframe)
        {
            InitForDataContext(() =>
            {
                var trades = AllTrades.Where(x => x.TradeReference == _trade.TradeReference);
                foreach (var trade in trades)
                {
                    trade.TimeFrame = timeframe;
                    tradeEngine.SetUpdateRequest(trade);//, TradeUpdateRequest.UPDATE);
                }

                MainObj.DrawCurrentDispatcher();
                MainObj.TradeStatusChanged(this);
            });
        }
        internal void LineUpTrades_UI_Synchronised(_Trade trade, _ChartLine chartLine = null)
        {

           var trades = GetTradesForTradeReference(trade.TradeReference);
            if (!trades.Any(x => x.Id == trade.Id))
            {
                //add trade to list, as it will not be in the db yet
                trades.Add(trade);
            }

            var order = trades.FirstOrDefault(x => x.Action == TradeAction.ORDER);
            if (order != null)
            {
                if (chartLine != null)
                {
                    //line could be for order or stop, and could be for moving the line or when adding a stop
                    chartLine.TradeId = trade.Id; //won't propagate from the Trade property until save...
                    SetUpdateRequest(chartLine);//, TradeUpdateRequest.UPDATE);
                    if (!AllLinesForSymbol.Contains(chartLine))
                    {
                        //add it so we can find it later
                        AllLinesForSymbol.Add(chartLine);
                    }
                }
          
                var orderLine = AllLinesForSymbol.FirstOrDefault(x => x.TradeId == order.Id);

                if (order.ItemStatus == TradeItemStatus.OPEN)
                {
                    //order has not entered, so move to line start
                    if (orderLine != null) //order could be market, with no line
                    {
                        order.Timestamp = orderLine.StartTimeStamp;
                        order.Price = orderLine.StartPrice;
                        SetUpdateRequest(order);//, TradeUpdateRequest.UPDATE);
                    }
                }

                //move any stop to the start of the line, and adjust line length etc.
                //this will work for any line-attached trade (later)
                var entry = trades.FirstOrDefault(x => x.Action == TradeAction.ENTRY);
                if (entry == null) //only set line start to order time if not entered - otherwise, cannot move stop line start once entered
                {
                    foreach (var attachedTrade in trades)
                    {
                        if (attachedTrade != null)
                        {
                            //move stop to correct price
                            var stopLine = AllLinesForSymbol.FirstOrDefault(x => x.TradeId == attachedTrade.Id);
                            if (stopLine != null)
                            {
                                //set line start and end times, if required, just for display purposes
                                if (orderLine != null)
                                {
                                    var gradient = (stopLine.EndPrice - stopLine.StartPrice) / (stopLine.EndTimeStamp - stopLine.StartTimeStamp).Ticks;
                                    if (stopLine.StartTimeStamp > order.Timestamp)
                                    {
                                        stopLine.StartPrice = stopLine.StartPrice - gradient * (stopLine.StartTimeStamp - order.Timestamp).Ticks;
                                        stopLine.StartTimeStamp = order.Timestamp;
                                        SetUpdateRequest(stopLine);//, TradeUpdateRequest.UPDATE);
                                    }
                                    else if (stopLine.EndTimeStamp < orderLine.EndTimeStamp)
                                    {
                                        stopLine.EndPrice = stopLine.EndPrice + gradient * (orderLine.EndTimeStamp - stopLine.EndTimeStamp).Ticks;
                                        stopLine.EndTimeStamp = orderLine.EndTimeStamp;
                                        SetUpdateRequest(stopLine);//, TradeUpdateRequest.UPDATE);
                                    }
                                }
                            }
                        }
                    }
                }

                bool isEntered = order.ItemStatus == TradeItemStatus.CLOSED && order.GlobalStatus == TradeGlobalStatus.ACTIVE;
                foreach (var attachedTrade in trades)
                {
                    if (attachedTrade != null)
                    {
                        //move stop to correct price
                        var stopLine = AllLinesForSymbol.FirstOrDefault(x => x.TradeId == attachedTrade.Id);
                        if (stopLine != null)
                        {
                            (attachedTrade.Price, _) = stopLine.GetPriceAtTime(attachedTrade.Timestamp, true);
                            if (attachedTrade.Action == TradeAction.STOP || attachedTrade.Action == TradeAction.FLOOR) // && attachedTrade.Type != TradeType.FLIP_ORDER)
                            {
                                SetStopOnExchange_Synchronised(attachedTrade);
                            }
                            SetUpdateRequest(attachedTrade);//, TradeUpdateRequest.UPDATE);
                        }
                    }
                }
            }
        } 

        //unused
        internal void AdvanceTrade_UI_synchronised(int tradeId)
        {
            InitForDataContext(() =>
            {
                var trade = AllTrades.FirstOrDefault(x => x.Id == tradeId);
                if (trade == null)
                {
                    AlertDialog.Show("Could not find trade");
                    return;
                }
                if (Entry != null)
                {
                    AlertDialog.Show("Cannot advance a trade start time which has already entered");
                    return;
                }

                //extend line
                var line = AllLinesForSymbol.GetLineForTrade(trade);
                if (line != null)
                {
                    line.StartTimeStamp = DateTime.UtcNow;
                    line.EndTimeStamp = line.StartTimeStamp.AddDays(1);
                    SetUpdateRequest(line);//, TradeUpdateRequest.UPDATE);

                    //var nextEarliest = GetNextInterval_UI(DateTime.UtcNow);
                    //var nextPrice = line.EndPrice + (line.EndPrice - line.StartPrice) * nextEarliest.Subtract(line.EndTimeStamp).TotalSeconds / line.EndTimeStamp.Subtract(line.StartTimeStamp).TotalSeconds;
                    trade.Timestamp = line.StartTimeStamp;
                    trade.Price = line.StartPrice;
                }
                else
                {
                    trade.Timestamp = DateTime.UtcNow;
                }
                SetUpdateRequest(trade);//, TradeUpdateRequest.UPDATE);

            });

            MainObj.TradeStatusChanged(this);
        }

        #endregion

        #region MainObj - called methods (on batches of trades)

        internal void CancelTrade_UI_synchronised(int tradeId = 0)
        {
            InitForDataContext(() =>
            {
                List<_Trade> tradesToClose = null;

                if (tradeId != 0)
                {
                    var trade = AllTrades.FirstOrDefault(x => x.Id == tradeId);
                    if (trade != null)
                    {
                        tradesToClose = new List<_Trade>
                        {
                            //just the single order to close
                            GetTradeForTradeReference(TradeAction.ORDER, trade.TradeReference)
                        };
                    }    
                }
                else
                {
                    //if no tradeId specified, close all open orders
                    tradesToClose = AllTrades.Where(x => x.Action == TradeAction.ORDER).ToList();
                }

                foreach (var order in tradesToClose)
                {
                    if (order.GlobalStatus == TradeGlobalStatus.ACTIVE) 
                    {
                        if (order.ExchangeMethodScript != null)
                        {
                            var s = USE_SIMULATED_EXCHANGE ? "Simulated exchange in use so trade must be force-removed (shift click) as it may not timeout" : " Please wait for timeout, then check Exchange and remove trade..";
                            AlertDialog.Show($"Order for {order.SymbolName} has a pending Exchange Method, so this may be an interrupted trade. {s}.");
                            return;
                        }

                        if (order.ItemStatus == TradeItemStatus.OPEN)
                        {
                            CloseUnenteredTrade_UI_synchronised(order.TradeReference);
                        }
                        else
                        {
                            //order has entered
                            if (order.ExchangeStatus == TradeExchangeStatus.ENTERED_ON_EXCHANGE) //don't bother to do this with backtests
                            {
                                CloseTradeLive_synchronised(false);
                            }
                            else
                            {
                                CloseUnenteredTrade_UI_synchronised(order.TradeReference);
                            }
                        }
                    }
                    else
                    {
                        #region Force-close interrupted trade
                        //this would happen if a trade has been interrupted, in which case everything is closed so there will be no active trades or order to close
                        //but the trade still needs to be closed on the system or it cannot be deleted
                        if (order.ExchangeMethodScript != null)
                        {
                            var trades = GetTradesForTradeReference(order.TradeReference);

                            foreach (var trade in trades)
                            {
                                trade.ExchangeMethodScript = null;//or will continue to show as interrupted
                                SetUpdateRequest(trade);//, TradeUpdateRequest.UPDATE);

                                if (trade.Action == TradeAction.CLOSE) //or will show at the bottom
                                {
                                    var entry = GetTradeForTradeReference(TradeAction.ENTRY, order.TradeReference);
                                    if (entry != null)
                                    {
                                        trade.Price = entry.Price;
                                    }
                                }
                                if (trade.Action == TradeAction.ENTRY)
                                {
                                    SetUpdateRequest(trade, TradeUpdateRequest.DELETE);
                                }
                            }

                            AlertDialog.Show($"{order.SymbolName} had an Interrupted trade so exchange status is indeterminate. Check exchange!!!!");
                        } 
                        #endregion
                    }
                }             
            });
        }

        internal void StopWaitingTrades_UI_synchronised()
        {
            InitForDataContext(() =>
            {
                foreach (var order in WaitingOrders)
                {
                     CloseUnenteredTrade_UI_synchronised(order.TradeReference, false);
                }
            });
        }

        internal void RemoveWaitingTrades_UI_synchronised()
        {
            InitForDataContext(() =>
            {
                foreach (var order in WaitingOrders)
                {
                    RemoveTradeFromSystem_UI_synchronised(order, false);
                }
            });
        }

        internal void RemoveUnallocatedLines_UI_synchronised()
        {
            InitForDataContext(() =>
            {
                if (AllLinesForSymbol.Count > 0)
                {
                    var unallocated = AllLinesForSymbol.Where(x => x.Trade == null);
                    foreach (var line in unallocated)
                    {
                        SetUpdateRequest(line, TradeUpdateRequest.DELETE);
                    }
                }
            });
        }

        internal void ExtendLines_UI_synchronised(DateTime nextEarliest)
        {
            InitForDataContext(() =>
            {
                foreach (var line in ActiveLines)
                {
                    line.EndPrice = line.EndPrice + (line.EndPrice - line.StartPrice) * nextEarliest.Subtract(line.EndTimeStamp).TotalSeconds / line.EndTimeStamp.Subtract(line.StartTimeStamp).TotalSeconds;
                    line.EndTimeStamp = nextEarliest;
                    SetUpdateRequest(line);//, TradeUpdateRequest.UPDATE);
                }
            });
        }

        #endregion


        //this allows for selective removal of stops before exchange removal, if the latter is not used (stop and floor on Bitmex or just stop on Binance)
        protected virtual void RemoveTradeBeforeExchangeRemoval_Synchronised(_Trade trade, bool removeLine, bool allowForceRemove) { }

        //note, this method uses the AllTrades list
        private bool RemoveTradeFromSystem_UI_synchronised(_Trade trade, bool removeLine, bool allowForceRemove = false)
        {
            //can be called on any trade, not just the order
            switch (trade.Action)
            {
                //if this is to delete the whole trade set, must be closed already
                case TradeAction.ORDER:
                case TradeAction.ENTRY:
                case TradeAction.CLOSE:
                    
                    var tradesToRemove = AllTrades.Where(x => x.TradeReference == trade.TradeReference);
                    var entry = tradesToRemove.Where(x => x.Action == TradeAction.ENTRY).FirstOrDefault();

                    //Binance_RemovePendingTradeRequest();

                    //if this is a locally entered trade, can remove it without closing on the exchange
                    if (allowForceRemove)
                    {
                        foreach (var t in tradesToRemove)
                        {
                            RemoveTradeFromLine_UI_synchronised(t, removeLine);
                            SetUpdateRequest(t, TradeUpdateRequest.FORCE_DELETE);
                        }
                        break;
                    }
                   
                    if (!CanDeleteTrade_synchronised(trade, entry))
                    {
                        AlertDialog.Show("Close trade first before removing", "Remove running trade");
                        {
                            return false;
                        }
                    }
                    //dump the lot
                    if (entry != null && entry.ExchangeMethodScript != null)
                    {
                        AlertDialog.Show($"{entry.SymbolName} was an interrupted trade. Check status on Exchange!");
                    }
                    foreach (var t in tradesToRemove)
                    {
                        RemoveTradeFromLine_UI_synchronised(t, removeLine);

                        
                        if (entry == null || entry.GlobalStatus == TradeGlobalStatus.STOPPED)
                        {
                            SetUpdateRequest(t, TradeUpdateRequest.FORCE_DELETE);
                        }
                        else
                        {
                            SetUpdateRequest(t, TradeUpdateRequest.DELETE);
                        }
                    }
                    break;


                //for stops and take profits, these can be removed while trade is live
                case TradeAction.STOP:
                case TradeAction.WAITING_STOP:
                case TradeAction.CEILING:
                case TradeAction.TAKE_PROFIT:
                case TradeAction.BREAK_EVEN:
                case TradeAction.FLOOR:

                    RemoveTradeBeforeExchangeRemoval_Synchronised(trade, removeLine, allowForceRemove || Entry == null);

                    if (trade.Type == TradeType.FLIP_ORDER)
                    {
                        //also remove waiting stop
                        var waitingStop = AllTrades.FirstOrDefault(x => x.TradeReference == trade.TradeReference && x.Action == TradeAction.WAITING_STOP && x.Type == TradeType.STOP_WAITING);
                        if (waitingStop != null)
                        {
                            RemoveTradeFromLine_UI_synchronised(waitingStop, false);
                            SetUpdateRequest(waitingStop, TradeUpdateRequest.DELETE);
                        }
                    }
                    if (Entry != null)
                    {
                        if (trade.ExchangeStatus == TradeExchangeStatus.ENTERED_ON_EXCHANGE)
                        {
                            //if stop is added then removed before first update, won't have been set on exchange
                            //also, if not setting stops on exchange then bail here
                            if (trade.Action == TradeAction.STOP || trade.Action == TradeAction.FLOOR)
                            {
                                RemoveStopFromExchange_Synchronised(trade);
                                //SetUpdateRequest(trade, TradeUpdateRequest.DELETE);
                            }
                            return true;
                        }
                    }

                    break;

               // case TradeAction.FLOOR:
                    //will allow floor delete while trade is live
                    //if (trade.ItemStatus == TradeItemStatus.CLOSED && trade.GlobalStatus == TradeGlobalStatus.STOPPED || trade.ExchangeStatus != TradeExchangeStatus.ENTERED_ON_EXCHANGE)
                    //{
                    //    SetUpdateRequest(trade, TradeUpdateRequest.DELETE);
                    //}
                    
                   // break;
            }

            return true;
        }

        internal _Trade SetLineOrder_UI_synchronised(TradePosition position, bool isBacktest, _ChartLine line, bool addClosed = false)
        {
            if (currentDataItem != null)
            {
                var frameNum = chart.GetFrameForTimestamp(line.StartTimeStamp);

                (var price, _) = line.GetPriceAtTime(currentDataItem.Timestamp, true);

                bool canSet = isBacktest == true || (position == TradePosition.LONG && price > currentDataItem.Close || position == TradePosition.SHORT && price < currentDataItem.Close);

                if (!canSet)
                {
                    var m = position == TradePosition.LONG ? "below" : "above";
                    AlertDialog.Show($"Warning ", $"Requested position is {m} current price, ensure break position is valid");
                }

                if (addClosed)
                {
                    isBacktest = true; //this will just set the order as STOPPED + CLOSED
                }
                if (isBacktest)
                {
                    addClosed = true;
                }
                var order = priceBreakOrderIndicator.SetOrder_UI_synchronised(TimeFrame, TradeType.LINE_BREAK_ORDER, position, symbolName, isBacktest, line.StartPrice, frameNum, line.Id);

                if (order != null)
                {
                    line.Trade = order;
                    line.TradeReference = order.TradeReference;
                    line.Type = LineType.BREAK;
                    SetUpdateRequest(order);//, TradeUpdateRequest.UPDATE);
                    SetUpdateRequest(line);//, TradeUpdateRequest.UPDATE);
                    currentTradeSetReference = order.TradeReference;
                }

                return order;
            }
            else
            {
                AlertDialog.Show($"Waiting for connection... ", "Cannot set trade");
            }
            return null;
        }

        internal void CloseUnenteredTrade_UI_synchronised(string tradeReference, bool removeTrades = false)
        {
            var trades = GetTradesForTradeReference(tradeReference);

            foreach (var trade in trades)
            {
                trade.GlobalStatus = TradeGlobalStatus.STOPPED;
                trade.ItemStatus = TradeItemStatus.CLOSED;
                trade.ExchangeStatus = TradeExchangeStatus.NEVER_ENTERED; //keep for reference
                SetUpdateRequest(trade, removeTrades ? TradeUpdateRequest.DELETE : TradeUpdateRequest.UPDATE);
            }

            //detach from any line, so line can then be reused if it is desirable to leave the trade in place
            // - no, otherwise restarting will not have an attached line
            //RemoveTradeFromLine_UI_synchronised(trades.FirstOrDefault(x => x.Action == TradeAction.ORDER));

            //clear the stops as they are no longer relevant anyway
            // however, since these are unentered trades, leave them in in case trade is to be restarted later
            // - but stops will still be deleted when system closes a trade
            //var stops = trades.Where(x => x.Action != TradeAction.ENTRY && x.Action != TradeAction.CLOSE && x.Action != TradeAction.ORDER);
            //foreach (var stop in stops)
            //{
            //    RemoveTradeFromLine_UI_synchronised(stop); //or will throw exception on saving
            //    SetUpdateRequest(stop, TradeUpdateRequest.DELETE);
            //}

            var lines = GetLinesForTradeReference(tradeReference);
            foreach (var line in lines)
            {
                line.TruncateLineToTime();
                line.Type = LineType.CLOSED_UNENTERED;
                SetUpdateRequest(line);//, TradeUpdateRequest.UPDATE);
            }
        }

        internal void RemoveTradeFromLine_UI_synchronised(_Trade trade, bool removeLine = false, bool activeTradesOnly = false)
        {
            if (trade != null)
            {
                //an optimization if only active trades considered
                var line = activeTradesOnly ? ActiveLines.GetLineForTrade(trade) : AllLinesForSymbol.GetLineForTrade(trade);
                if (line != null)
                {
                    line.Trade = null;
                    line.TradeId = null; //must do this, not just set the trade to null!
                    line.TradeReference = null;
                    line.Type = LineType.NONE;
                    SetUpdateRequest(line);//, TradeUpdateRequest.UPDATE);
                    //remove any tiny lines completely

                    var timeSpan = line.EndTimeStamp.Subtract(line.StartTimeStamp).TotalMinutes;
                    var tinyLine = timeSpan < MainObj.visibleChart.TimeFrame * 2;
                    if (tinyLine || removeLine)
                    {
                        SetUpdateRequest(line, TradeUpdateRequest.DELETE);
                    }
                }
            }
        }


        #region Get results

        internal double lastCandleCountdown;
        //GetResults methods also called in Backtesting

        //this will return results based on current (display) timeframe, which will be set when the display switches to another symbol / timeframe
        public (List<_SymbolDataItem>, List<_SymbolDataItem>) GetResults_UI(int firstRequestedFrame, int numFrames, int timeFrame, bool align = true)
        {
            framesetResults.Clear();

            if (timeFrame == 0 || results.Count == 0)
            {
                return (null, null);
            }

            int startFrame = firstRequestedFrame;

            //line up with time interval (missed / extra frames will mess this up)
            if (align)
            {
                for (; firstRequestedFrame < results.Count; firstRequestedFrame++)
                {
                    var t = results[firstRequestedFrame].Timestamp;
                    if (t.Hour == START_HOUR)
                    {
                        break;
                    }
                }
            }

            //will have skipped some bars to get to the start bar, so when scrolling right for example, will be moving back down the data list
            // but will continue to skip bars until the timeframes line up, thus causing the scroll to move in big increments
            // - so need to display bars prior to current skip position
            int numSkipped = (firstRequestedFrame - startFrame) / timeFrame;
            firstRequestedFrame -= numSkipped * timeFrame;

            //switching could interrupt this process and there would not be enough results for the requested frame
            if (firstRequestedFrame > results.Count - 1)
            {
                firstRequestedFrame = results.Count - 1;
            }

            //since this is aggregating over a number of frames, the last frame must allow for aggregation
            int lastFrame = firstRequestedFrame + numFrames;
            if (lastFrame > results.Count)
            {
                lastFrame = results.Count;
            }

            var firstTime = results[firstRequestedFrame].Timestamp;

            timeFrameAggregator.Init(timeFrame);

            int lastAggregatedFrame = 0;

            while (firstRequestedFrame < lastFrame) //i is incremented in the loop below, lastFrame is always at least 1 less than results.Count
            { 
                bool starting = true;
                while (true)
                {
                    var nextItem = results[firstRequestedFrame];
                    if (starting)
                    {
                        //due to some frames having a partial minute timestamp, and some getting duplicated due to interuptions,
                        //the intial timestamp can be a minute or so out.
                        //The very first item will be on a time boundary, so this will only affect the timestamps after interrupted items
                        // on the current screen. But it does show times incorrectly when hovering.
                        //Solution is just to reset the first timestamp
                        starting = false;
                        nextItem.Timestamp = firstTime;
                    }

                    _SymbolDataItem ret = timeFrameAggregator.AggregateSingleFrame(nextItem);
                    firstRequestedFrame++;
                    if (firstRequestedFrame == results.Count)
                    {
                        break;
                    }
                    if (ret != null)
                    {
                        framesetResults.Add(ret);
                        firstTime = firstTime.AddMinutes(timeFrame);
                        lastAggregatedFrame = firstRequestedFrame;
                        break;
                    }
                }
            }

            //has completed aggregation of all candles, so get the partial time left before next aggregation loop
            // - 1 minute can be ahead of current time, by 1 or 2 mins for some reason
            while (firstTime > DateTime.UtcNow)
            {
                firstTime = firstTime.AddMinutes(-1);
            }

            lastCandleCountdown = TimeFrame * 60 - DateTime.UtcNow.Subtract(firstTime).TotalSeconds;

            List<_SymbolDataItem> currentCandleItems = new List<_SymbolDataItem>();
            for (int i = lastAggregatedFrame; i < firstRequestedFrame; i++)
            {
                currentCandleItems.Add(results[i]);
            }

            return (framesetResults, currentCandleItems);
        }

        public (List<_SymbolDataItem>, List<_SymbolDataItem>) GetResultsHA_UI(int firstRequestedFrame, int numFrames, int timeFrame, bool align = true)
        {
            /*
                Close = 0.25 * (Open+High+Low+Close)           (The average price of the current bar)
                Open  = 0.5  * (Open of Prev. Bar+Close of Prev. Bar)          (The midpoint of the previous bar)
                High=Max[High, Open, Close]
                Low=Min[Low, Open, Close]
​	        */

            /*
             * So this does not tally with TradingView, as that uses the previous Heikin Ashi bar, and the current candle, for the calculation
             * Therefore, would need to set up a 1-min HA results set as well as the standard one, which kind of implies that instead of a single 1-min
             * data set with aggregation on-the-fly, a better approach would be multiple results sets.
             * Will leave for now.
             * */
            try
            {
                List<_SymbolDataItem> ret = new List<_SymbolDataItem>();
                (var items, var currentCandleItems) = GetResults_UI(firstRequestedFrame, numFrames, timeFrame, align);
                if (items != null && items.Count > 0)
                {
                    for (int i = 1; i < items.Count; i++)
                    {
                        var item = items[i];
                        var prevItem = i == 1 ? items[i - 1] : ret[i - 2];
                        var haItem =
                             new _SymbolDataItem
                             {
                                 Close = 0.25 * (item.Open + item.Close + item.Low + item.High),
                                 Open = 0.5 * (prevItem.Open + prevItem.Close),
                                 High = Math.Max(Math.Max(item.High, item.Open), item.Close),
                                 Low = Math.Min(Math.Min(item.Low, item.Open), item.Close),
                                 Timestamp = item.Timestamp,
                                 //SymbolName = item.SymbolName,
                                 Id = item.Id,
                                 //ExchangeId = item.ExchangeId
                             };
                        ret.Add(haItem);

                    }
                    //ha list is one item shorter than the raw data, so must offset all data by one item for it to line up
                    var firstItem = ret[0];
                    ret.Insert(0, firstItem);
                }

                return (ret, currentCandleItems);
            }
            catch
            {
                return (null, null);
            }
        }


        //worker method to get entry outside of context synch
        public string IsEntered(DataContext db, int id)
        {
            var trade = db.Trades.FirstOrDefault(x =>
                        x.Id == id &&
                        x.GlobalStatus == TradeGlobalStatus.ACTIVE &&
                        x.ItemStatus == TradeItemStatus.OPEN &&
                        x.Action == TradeAction.ENTRY 
                        );
            return trade?.SymbolName;
        }

        public static bool AnyEnteredLive(DataContext db)
        {
            return db.Trades.Any(x =>
                        x.GlobalStatus == TradeGlobalStatus.ACTIVE &&
                        x.ItemStatus == TradeItemStatus.OPEN &&
                        x.Action == TradeAction.ENTRY
                        );
        }

        internal bool CanSetTrade(_ChartLine orderLine, _ChartLine tradeLine, double price, TradeAction action, _Trade entry, TradePosition position)
        {
            if (currentDataItem == null)
            {
                return false;
            }
            //if trade is on a line, use that for price, otherwise use supplied price (i.e. for ceiling, floor etc. where there is no line)
            if (tradeLine != null)
            {
                (price, _) = tradeLine.GetPriceAtTime(currentDataItem.Timestamp, true);
            }

            var msg = entry == null ? "projected entry price" : "current entry price";

            var basePrice = currentDataItem.Close;
            if (entry == null)
            {
                if (orderLine != null)
                {
                    (basePrice, _) = orderLine.GetPriceAtTime(currentDataItem.Timestamp, true);
                }
            }

            if (action == TradeAction.WAITING_STOP && orderLine != null)
            {
                //waiting stop must be measured against order line
                (basePrice, _) = orderLine.GetPriceAtTime(currentDataItem.Timestamp, true);
            }


            var v = MainObj.GetPriceDiff(basePrice, price, position) * 100;

            bool canSet = false;
                
            switch (action) {
                case TradeAction.ORDER:
                    canSet = -v > MIN_ORDER_THRESHOLD;
                    break;
                case TradeAction.BREAK_EVEN:
                    canSet = -v > BREAK_EVEN_THRESHOLD;
                    break;
                case TradeAction.TAKE_PROFIT:
                case TradeAction.CEILING:
                    canSet = -v > STOPLINE_MIN_VALUE;
                    break;
                case TradeAction.FLOOR:
                case TradeAction.STOP:
                case TradeAction.WAITING_STOP:
                    canSet = v > STOPLINE_MIN_VALUE;
                    //break;
                //case TradeAction.WAITING_STOP:
                    //canSet = -v > STOPLINE_MIN_VALUE;
                    //        canSet = position == TradePosition.LONG ? -v > STOPLINE_MIN_VALUE : v > STOPLINE_MIN_VALUE;
                    break;
            }

            if (!canSet)
            {
                var s = string.Empty;
                switch (action)
                {
                    case TradeAction.CEILING:
                        s = "Ceiling";
                        break;
                    case TradeAction.FLOOR:
                        s = "Floor";
                        break;
                    case TradeAction.TAKE_PROFIT:
                        s = "Take Profit";
                        break;
                    case TradeAction.STOP:
                        s = "Stop";
                        break;
                    case TradeAction.BREAK_EVEN:
                        s = "Break even";
                        break;
                    case TradeAction.WAITING_STOP:
                        s = "Waiting stop";
                        break;
                }

                return false;
            }
            return true;
        }

        #endregion


    }
}