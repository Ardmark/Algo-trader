using AlgoTrader.Shared;
using AlgoTrader.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AlgoTrader.Shared.ViewModels
{
    public class ResultsViewModel
    {


        public ResultsViewModel()
        {

        }

        const double LEVERAGE = 5;

        public void Populate(Grid grid)
        {

            int row = 0;
            double total = 0;
            double totalWinning = 0;
            double totalLosing = 0;
            int up = 0;
            int down = 0;
            double totalCompound = 1;
            double totalCompoundNoLeverage = 1;

            using (DataContext db = new DataContext())
            {

                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Auto)
                });
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Auto)
                });
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Auto)
                });
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Auto)
                });
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Auto)
                });
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Auto)
                });
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Auto)
                });



                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = new GridLength(30, GridUnitType.Auto)
                });

                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = new GridLength(30, GridUnitType.Auto)
                });


                row++;
                row++;


                //this assumes backtests will not be mixed between manual and auto-generated
                var allTrades = db.Trades.AsNoTracking().AsParallel().Where(d => d.GlobalStatus == TradeGlobalStatus.STOPPED).ToList();
                var allCloses = allTrades.Where(x => x.Action == TradeAction.CLOSE).ToList();
#if BACKTEST
                /*
                 * Issue with backtests: tests are done per-symbol, so cannot account for concurrency of trades
                 * Therefore, must filter out here if required.
                 * This will show either ONLY non-concurrent live trades, or ALL as live trades regardless of concurrency
                 * */
                //if (!TradeEngine.ENTER_CONCURRENT_TRADES_AS_TESTS)
                //{
                //    //filter out entries which should not have been entered
                //    var entries = allTrades.Where(x => x.Action == TradeAction.ENTRY).OrderBy(x => x.Timestamp).ToList();
                //    var tradesToRemove = new List<Trade>();
                //    foreach (var entry in entries)
                //    {
                //        if (entry.OrderType != OrderType.NEVER_ENTERED)
                //        {
                //            var close = allTrades.Where(x => x.TradeId == entry.TradeId && x.Action == TradeAction.CLOSE).FirstOrDefault();
                //            if (close != null)
                //            {
                //                var overlapStartTrades = entries.Where(x => x.Action == TradeAction.ENTRY && x.TradeId != entry.TradeId && x.OrderType != OrderType.NEVER_ENTERED && x.Timestamp <= entry.Timestamp).ToList();
                //                if (overlapStartTrades.Any())
                //                {
                //                    foreach (var overlapEntry in overlapStartTrades)
                //                    {
                //                        var overlapClose = allTrades.Where(x => x.Action == TradeAction.CLOSE && x.TradeId == overlapEntry.TradeId && x.Timestamp > entry.Timestamp).FirstOrDefault();
                //                        if (overlapClose != null)
                //                        {
                //                            tradesToRemove.Add(close);
                //                            entry.OrderType = OrderType.NEVER_ENTERED;
                //                            break;
                //                        }
                //
                //                    }
                //                }
                //            }
                //        }
                //    }
                //
                //    foreach (var t in tradesToRemove)
                //    {
                //        allCloses.Remove(t);
                //    }
                //    foreach (var t in allCloses)
                //    {
                //        t.OrderType = OrderType.ENTERED_ON_EXCHANGE; //or is confusing as most will show as not entered, as that will come from previous backtest etc.
                //    }
                //
                //}
#endif
           
         //TODO - this needs changing for tables
                //var symbolGroups = db.SymbolDataItems.AsNoTracking().AsParallel().GroupBy(x => x.SymbolName);

                #region group for 9-hr percent calculation (unused)
                //var sg = new Dictionary<string, List<SymbolDataItem>>();
                //foreach (var group in symbolGroups)
                //{
                //    if (SymbolNamesBinance.SymbolNames().Contains(group.Key))
                //    {
                //        sg.Add(group.Key, group.ToList());
                //    }
                //}
                #endregion

                //will assume all backtests or all live
                var tradeGroups = allCloses.GroupBy(d => d.TradeReference);
                List<AlgoTrader.Shared._Trade> trades = new List<AlgoTrader.Shared._Trade>();
                foreach (var tradeGroup in tradeGroups)
                {
                    var close = (from t in tradeGroup where t.Action == TradeAction.CLOSE select t).FirstOrDefault();
                    if (close != null)
                    {
                        trades.Add(close);

                        #region get 9-hr percent for symbol
                        
                        //var order = allTrades.Where(x => x.Action == TradeAction.ORDER && x.TradeId == close.TradeId).First();
                        //var orderTime = new DateTime(order.Timestamp.Year, order.Timestamp.Month, order.Timestamp.Day, order.Timestamp.Hour, order.Timestamp.Minute, 0);
                        //
                        //var earliestTime__9 = order.Timestamp.AddHours(-9);
                        //var earliestTime_24 = order.Timestamp.AddHours(-24);
                        ////remove seconds to get an exact boundary on a candle
                        //earliestTime__9 = new DateTime(earliestTime__9.Year, earliestTime__9.Month, earliestTime__9.Day, earliestTime__9.Hour, earliestTime__9.Minute, 0);
                        //earliestTime_24 = new DateTime(earliestTime_24.Year, earliestTime_24.Month, earliestTime_24.Day, earliestTime_24.Hour, earliestTime_24.Minute, 0);
                        //
                        //try
                        //{
                        //    var earliestTrade__9 = sg[order.SymbolName].Where(x => DateTime.Compare(earliestTime__9, x.Timestamp) == 0).FirstOrDefault();
                        //    var orderSymbolPrice = sg[order.SymbolName].Where(x => DateTime.Compare(orderTime, x.Timestamp) == 0).FirstOrDefault();
                        //    var v = TradeEngine.GetPriceDiffPercent(orderSymbolPrice, earliestTrade__9.Close, TradePosition.LONG) * 100;
                        //    close.ActionValue = v;
                        //}
                        //catch
                        //{
                        //
                        //}
                        

                        #endregion

                        #region get 9-hr percent of entire market


                        //get 9-hr percent of entire market
                        //var order = allTrades.Where(x => x.Action == TradeAction.ORDER && x.TradeId == close.TradeId).First();
                        //var orderTime = new DateTime(order.Timestamp.Year, order.Timestamp.Month, order.Timestamp.Day, order.Timestamp.Hour, order.Timestamp.Minute, 0);
                        //
                        //var prices__8hour = new List<double>();
                        //var prices_24hour = new List<double>();
                        //var earliestTime__9 = order.Timestamp.AddHours(-9);
                        //var earliestTime_24 = order.Timestamp.AddHours(-24);
                        ////remove seconds to get an exact boundary on a candle
                        //earliestTime__9 = new DateTime(earliestTime__9.Year, earliestTime__9.Month, earliestTime__9.Day, earliestTime__9.Hour, earliestTime__9.Minute, 0);
                        //earliestTime_24 = new DateTime(earliestTime_24.Year, earliestTime_24.Month, earliestTime_24.Day, earliestTime_24.Hour, earliestTime_24.Minute, 0);
                        //
                        //
                        //
                        //foreach (var group in sg.Values)
                        //{
                        //    var earliestTrade__9 = group.Where(x => DateTime.Compare(earliestTime__9, x.Timestamp) == 0).FirstOrDefault();
                        //    var earliestTrade_24 = group.Where(x => DateTime.Compare(earliestTime_24, x.Timestamp) == 0).FirstOrDefault();
                        //    var orderSymbolPrice = group.Where(x => DateTime.Compare(orderTime, x.Timestamp) == 0).FirstOrDefault();
                        //
                        //    try
                        //    {
                        //        if (earliestTrade__9 != null)
                        //        {
                        //            var v = TradeEngine.GetPriceDiffPercent(orderSymbolPrice, earliestTrade__9.Close, TradePosition.LONG);
                        //            prices__8hour.Add(v);
                        //        }
                        //        if (earliestTrade_24 != null)
                        //        {
                        //            var v = TradeEngine.GetPriceDiffPercent(orderSymbolPrice, earliestTrade_24.Close, TradePosition.LONG);
                        //            prices_24hour.Add(v);
                        //        }
                        //    }
                        //    catch
                        //    {
                        //
                        //    }
                        //}
                        //if (prices__8hour.Any())
                        //{
                        //    var avg = prices__8hour.Average() * 100;
                        //    
                        //}
                        //if (prices_24hour.Any())
                        //{
                        //    var avg = prices_24hour.Average() * 100;
                        //    close.ActionValue = avg;
                        //}
                        
                        #endregion
                    }
                }

                //trades = trades.Where(x => x.ActionValue >= 0).ToList();

                int AddRow()
                {
                    var ret = row;
                    row++;
                    grid.RowDefinitions.Add(new RowDefinition
                    {
                        Height = new GridLength(1, GridUnitType.Auto)
                    });
                    return ret;
                }
                void AddText(int _row, int _column, string text, SolidColorBrush color = null, TextDecorationCollection decorations = null)
                {

                    TextBox t1 = new TextBox
                    {
                        Text = text,
                        BorderBrush = Brushes.Transparent,
                        Margin = new Thickness(6, 2, 6, 2)
                    };
                    if (color != null)
                    {
                        t1.Foreground = color;
                    }
                    if (decorations != null)
                    {
                        t1.TextDecorations = decorations;
                    }

                    grid.Children.Add(t1);
                    Grid.SetColumn(t1, _column);
                    Grid.SetRow(t1, _row);
                }


                var r = AddRow();
                AddText(r, 0, "Date");
                AddText(r, 1, "Symbol");
                AddText(r, 2, "OrderType");
                AddText(r, 3, "Result");
                AddText(r, 4, "Swing");
                AddText(r, 5, "Frame");
                AddText(r, 6, "View");




                var count = 0;
                //var groups = trades.GroupBy(x => x.SymbolName);
                //foreach (var group in groups.OrderBy(x => x.Key))
                //{
                //    var v = group.OrderBy(x => x.Timestamp).ToList();

                //    foreach (var close in v)
                foreach (var close in trades.OrderBy(x => x.Timestamp))
                {
                    count++;
                    Chart chart = MainObj.GetChartForSymbol(close.SymbolName);
                    int frameClose = chart != null ? chart.GetFrameForTimestamp(close.Timestamp) : 0;
                    var open = db.Trades.Where(x => x.TradeReference == close.TradeReference && x.Action == TradeAction.ENTRY).FirstOrDefault();
                    int frameOpen = chart != null && open != null ? chart.GetFrameForTimestamp(open.Timestamp) : 0;
                    if (frameOpen < 0)
                    {
                        frameOpen = 0; //when only small amount of data is available, midnight boundary can be after first trade was placed
                    }
                    /*
                     * Will only show live total. To get all totalled, including concurrent trades, must get result in the backtest project,
                     *  and have ENTER_CONCURRENT_TRADES_AS_TESTS set to true
                     * */
                    var result = close.Result - 0.2;

                    var c = result * close.Leverage;

                    total += result;

                    if (close.Result >= 0)
                    {
                        totalWinning += result;
                    }
                    if (result < 0)
                    {
                        totalLosing += result;
                    }
                    totalCompound *= 1 + c / 100;
                    totalCompoundNoLeverage *= 1 + result / 100;

                    r = AddRow();
                    AddText(r, 0, close.Timestamp.ToString());

                    AddText(r, 1, close.SymbolName);

                    var orderType = "Unentered";
                    switch (close.ExchangeStatus)
                    {
                        case TradeExchangeStatus.ENTERED_OFF_EXCHANGE:
                            orderType = "";
                            break;
                        case TradeExchangeStatus.ENTERED_ON_EXCHANGE:
                            orderType = "LIVE";
                            break;
                        case TradeExchangeStatus.ENTERING_ON_EXCHANGE:
                            orderType = "AWAITING ENTRY...";
                            break;
                    }
                    AddText(r, 2, orderType);

                    var posMarker = close.Position == TradePosition.LONG ? "% (long)" : "% (short)";
                    var text = result == 0 ? "--" : result.ToString("0.00") + posMarker;
                    AddText(r, 3, text, result < 0 ? Brushes.Red : Brushes.Blue, close.Leverage > 1 ? TextDecorations.Underline : null);

                    AddText(r, 4, close.ActionValue.ToString("0:00%"));
                    AddText(r, 5, frameOpen.ToString(), Brushes.DarkGray);

                    Button button = new Button
                    {
                        Content = "View Trade"
                    };
                    button.Click += (object sender, RoutedEventArgs e) =>
                    {
                        if (chart != null)
                        {
                            MainObj.GoToFrame_ChangeSymbol(frameClose, 7, close.SymbolName);
                        }
                    };
                    grid.Children.Add(button);
                    Grid.SetColumn(button, 6);
                    Grid.SetRow(button, r);

                    if (close.Result > 0) up++;
                    if (close.Result < 0) down++;
                    //    }
                }

                TextBox textTotal0 = new TextBox
                {
                    Text = $"Number of trades: ",
                    Margin = new Thickness(10, 10, 10, 10),
                    FontSize = 20,
                    Foreground = total < 0 ? Brushes.Red : Brushes.Blue,
                    BorderBrush = Brushes.Transparent
                };
                grid.Children.Add(textTotal0);
                Grid.SetColumn(textTotal0, 0);
                Grid.SetRow(textTotal0, 0);

                TextBox textTotal01 = new TextBox
                {
                    Text = count.ToString(),
                    Margin = new Thickness(0, 10, 10, 10),
                    FontSize = 20,
                    Foreground = total < 0 ? Brushes.Red : Brushes.Blue,
                    BorderBrush = Brushes.Transparent
                };
                grid.Children.Add(textTotal01);
                Grid.SetColumn(textTotal01, 1);
                Grid.SetRow(textTotal01, 0);

                totalCompound -= 1;
                totalCompound *= 100;

                TextBox textTotal1 = new TextBox
                {
                    Text = "Exchange entered total:",
                    Margin = new Thickness(10, 10, 10, 10),
                    FontSize = 20,
                    Foreground = total < 0 ? Brushes.Red : Brushes.Blue,
                    BorderBrush = Brushes.Transparent
                };
                grid.Children.Add(textTotal1);
                Grid.SetColumn(textTotal1, 0);
                Grid.SetRow(textTotal1, 1);

                TextBox textTotal2 = new TextBox
                {
                    Text = string.Format("{0:0.0000}%", total),
                    Margin = new Thickness(0, 10, 10, 10),
                    FontSize = 20,
                    Foreground = total < 0 ? Brushes.Red : Brushes.Blue,
                    BorderBrush = total < 0 ? Brushes.Red : Brushes.Blue
                };
                grid.Children.Add(textTotal2);
                Grid.SetColumn(textTotal2, 1);
                Grid.SetRow(textTotal2, 1);

                totalCompoundNoLeverage -= 1;
                totalCompoundNoLeverage *= 100;
                row++;
                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = new GridLength(1, GridUnitType.Auto)
                });
                TextBox textTotal3a = new TextBox
                {
                    Text = "Compound no leverage:",
                    Margin = new Thickness(10, 10, 10, 10),
                    FontSize = 20,
                    Foreground = total < 0 ? Brushes.Red : Brushes.Blue,
                    BorderBrush = Brushes.Transparent
                };
                grid.Children.Add(textTotal3a);
                Grid.SetColumn(textTotal3a, 0);
                Grid.SetRow(textTotal3a, row);

                TextBox textTotal4a = new TextBox
                {
                    Text = string.Format("{0:0.0000}%", totalCompoundNoLeverage),
                    Margin = new Thickness(0, 10, 10, 10),
                    FontSize = 20,
                    Foreground = totalCompound < 0 ? Brushes.Red : Brushes.Blue,
                    BorderBrush = totalCompound < 0 ? Brushes.Red : Brushes.Blue
                };
                grid.Children.Add(textTotal4a);
                Grid.SetColumn(textTotal4a, 1);
                Grid.SetRow(textTotal4a, row);

                row++;
                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = new GridLength(1, GridUnitType.Auto)
                });
                TextBox textTotal3 = new TextBox
                {
                    Text = "Compound:",
                    Margin = new Thickness(10, 10, 10, 10),
                    FontSize = 20,
                    Foreground = total < 0 ? Brushes.Red : Brushes.Blue,
                    BorderBrush = Brushes.Transparent
                };
                grid.Children.Add(textTotal3);
                Grid.SetColumn(textTotal3, 0);
                Grid.SetRow(textTotal3, row);

                TextBox textTotal4 = new TextBox
                {
                    Text = string.Format("{0:0.0000}%", totalCompound),
                    Margin = new Thickness(0, 10, 10, 10),
                    FontSize = 20,
                    Foreground = totalCompound < 0 ? Brushes.Red : Brushes.Blue,
                    BorderBrush = totalCompound < 0 ? Brushes.Red : Brushes.Blue
                };
                grid.Children.Add(textTotal4);
                Grid.SetColumn(textTotal4, 1);
                Grid.SetRow(textTotal4, row);

                row++;
                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = new GridLength(1, GridUnitType.Auto)
                });
                TextBox textTotal5 = new TextBox
                {
                    Text = string.Format("Winning: {0}, Losing: {1}", up, down),
                    Margin = new Thickness(0, 10, 10, 10),
                    FontSize = 16,
                    Foreground = Brushes.Black,
                    BorderBrush = Brushes.Transparent
                };
                grid.Children.Add(textTotal5);
                Grid.SetColumn(textTotal5, 0);
                Grid.SetRow(textTotal5, row);

                row++;
                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = new GridLength(1, GridUnitType.Auto)
                });
                TextBox textTotal6 = new TextBox
                {
                    Text = "Winning:",
                    Margin = new Thickness(10, 10, 10, 10),
                    FontSize = 20,
                    Foreground = total < 0 ? Brushes.Red : Brushes.Blue,
                    BorderBrush = Brushes.Transparent
                };
                grid.Children.Add(textTotal6);
                Grid.SetColumn(textTotal6, 0);
                Grid.SetRow(textTotal6, row);

                TextBox textTotal7 = new TextBox
                {
                    Text = string.Format("{0:0.0000}%", totalWinning),
                    Margin = new Thickness(0, 10, 10, 10),
                    FontSize = 20,
                    Foreground = total < 0 ? Brushes.Red : Brushes.Blue,
                    BorderBrush = total < 0 ? Brushes.Red : Brushes.Blue
                };
                grid.Children.Add(textTotal7);
                Grid.SetColumn(textTotal7, 1);
                Grid.SetRow(textTotal7, row);


                row++;
                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = new GridLength(1, GridUnitType.Auto)
                });
                //need another one for some reason
                row++;
                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = new GridLength(1, GridUnitType.Auto)
                });

                TextBox textTotal8 = new TextBox
                {
                    Text = "Losing:",
                    Margin = new Thickness(10, 10, 10, 10),
                    FontSize = 20,
                    Foreground = total < 0 ? Brushes.Red : Brushes.Blue,
                    BorderBrush = Brushes.Transparent
                };
                grid.Children.Add(textTotal8);
                Grid.SetColumn(textTotal8, 0);
                Grid.SetRow(textTotal8, row);

                TextBox textTotal9 = new TextBox
                {
                    Text = string.Format("{0:0.0000}%", totalLosing),
                    Margin = new Thickness(0, 10, 10, 10),
                    FontSize = 20,
                    Foreground = total < 0 ? Brushes.Red : Brushes.Blue,
                    BorderBrush = total < 0 ? Brushes.Red : Brushes.Blue
                };
                grid.Children.Add(textTotal9);
                Grid.SetColumn(textTotal9, 1);
                Grid.SetRow(textTotal9, row);

            }
        }
    }
}
