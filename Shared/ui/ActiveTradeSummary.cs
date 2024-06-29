using AlgoTrader.Shared;
using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using Shared.Trading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

namespace AlgoTrader.Shared
{
    public static class ActiveTradeSummary
    {
        static readonly List<double> prices__8hour = new List<double>();
        static readonly List<double> prices_24hour = new List<double>();
        public static DateTime LastClosedTimeToShow = DateTime.UtcNow.AddDays(-1);
        //public static int LastClosedTradesTimeframe { get; set; } = 24;
        static DispatcherTimer countdownTimer;

        public static void Init()
        {

            int dailySummaryTime = 0;
            const int tradeSummaryInterval = 60; //seconds
                                                 //const int dailySummaryInterval = 60;//do daily summary every 1 min, not every 10 secs
            LastClosedTimeToShow = DateTime.UtcNow;
            countdownTimer = new DispatcherTimer(new TimeSpan(0, 0, tradeSummaryInterval), DispatcherPriority.Normal, async (object sender, EventArgs e) =>
            {
                dailySummaryTime += tradeSummaryInterval;
                await Update(null);
            }, Dispatcher.CurrentDispatcher);
        }

        public static async Task<(List<_Trade>, List<_Trade>)> Update(TradeEngine tradeEngine, bool dailySummary = false)
        {
            try
            {
                var ret = await Task.Run(() =>
                {
                    using (DataContext db = new DataContext())
                    {
                        if (tradeEngine != null)
                        {
                            //this is not required for a timed update, that is just to refresh the info window
                            //otherwise, it is assumed that this call has probably altered the waiting or active trade status somewhere
                            tradeEngine.UpdateCurrentTradeSetReference(db);
                        }
                        var allTrades = db.Trades.AsNoTracking().AsParallel().Where(x => x.GlobalStatus == TradeGlobalStatus.ACTIVE).ToList();
                        //because check is made against script name, if a trade has exited but still has a script it will be interrupted so will show in the list
                        var enteredTrades = allTrades.Where(x => x.Action == TradeAction.ENTRY).ToList();
                        var allOrders = allTrades.Where(x => x.Action == TradeAction.ORDER).ToList();
                        var waitingTrades = allOrders.Where(x => x.ItemStatus == TradeItemStatus.OPEN).ToList();
                        //var stops = allTrades.Where(x => x.Action == TradeAction.STOP && x.ItemStatus == TradeItemStatus.OPEN);
                        //var takeProfits = allTrades.Where(x => x.Action == TradeAction.CEILING && x.ItemStatus == TradeItemStatus.OPEN);
                        var closedOrders = db.Trades.AsNoTracking().AsParallel().Where(x => x.Action == TradeAction.CLOSE && x.GlobalStatus == TradeGlobalStatus.STOPPED && x.Timestamp > LastClosedTimeToShow).ToList();
                        var outputString = string.Empty;
                        string dailySummaryString = string.Empty;

                        #region get 9 and 24-hour summaries

                        unchecked
                        {
                            //if (dailySummary)
                            //{
                            //    countdownTimer.Stop();
                            //    dailySummaryString = string.Empty;
                            //    prices__8hour.Clear();
                            //    prices_24hour.Clear();
                            //    var earliestTime__9 = DateTime.UtcNow.AddHours(-9);
                            //    var earliestTime_24 = DateTime.UtcNow.AddHours(-24);
                            //    //remove seconds to get an exact boundary on a candle
                            //    earliestTime__9 = new DateTime(earliestTime__9.Year, earliestTime__9.Month, earliestTime__9.Day, earliestTime__9.Hour, earliestTime__9.Minute, 0);
                            //    earliestTime_24 = new DateTime(earliestTime_24.Year, earliestTime_24.Month, earliestTime_24.Day, earliestTime_24.Hour, earliestTime_24.Minute, 0);

                            //    var tradeGroups = db.SymbolDataItems.AsNoTracking().AsParallel().GroupBy(x => x.SymbolName);

                            //    foreach (var group in tradeGroups)
                            //    {
                            //        var earliestTrade__9 = group.Where(x => DateTime.Compare(earliestTime__9, x.Timestamp) == 0).FirstOrDefault();
                            //        var earliestTrade_24 = group.Where(x => DateTime.Compare(earliestTime_24, x.Timestamp) == 0).FirstOrDefault();
                            //        var latestTrade = group.LastOrDefault(); //assuming order by timestamp anyway
                            //        if (earliestTrade__9 != null && latestTrade != null)
                            //        {
                            //            var v = MainObj.GetPriceDiff(latestTrade.Close, earliestTrade__9.Close, TradePosition.LONG);
                            //            prices__8hour.Add(v);
                            //        }
                            //        if (earliestTrade_24 != null && latestTrade != null)
                            //        {
                            //            var v = MainObj.GetPriceDiff(latestTrade.Close, earliestTrade_24.Close, TradePosition.LONG);
                            //            prices_24hour.Add(v);
                            //        }
                            //    }
                            //    if (prices__8hour.Any())
                            //    {
                            //        var avg = prices__8hour.Average() * 100;
                            //        dailySummaryString += string.Format("8 hour average: {0}%\n", avg.ToString("F"));
                            //    }
                            //    if (prices_24hour.Any())
                            //    {
                            //        var avg = prices_24hour.Average() * 100;
                            //        dailySummaryString += string.Format("24 hour average: {0}%\n", avg.ToString("F"));
                            //    }

                            //    countdownTimer.Start();
                            //}
                        }

                        #endregion

                        StringBuilder sb = new StringBuilder(dailySummaryString);

                        sb.Append($"{MainObj.GetUpdateCount()} data updates received per minute\n");

                        //build a list of current data items
                        var currentSymbolList = new Dictionary<string, _SymbolDataItem>();

                        foreach (var v in MainObj.displayPanelsForSymbol)
                        {
                            TradeEngine _tradeEngine = v.Value.Where(x => x.GetType() == (TradeEngine.EXCHANGE == ExchangeId.BITMEX ? typeof(TradeEngineBitmex) :  typeof(TradeEngineBinance))).FirstOrDefault() as TradeEngine;
                            if (_tradeEngine != null && _tradeEngine.currentDataItem != null)
                            {
                                currentSymbolList.Add(v.Key, _tradeEngine.currentDataItem);
                            }
                        }

                        string GetTradeType(_Trade _trade)
                        {
                            string tradeType = string.Empty;
                            switch (_trade.Type)
                            {
                                case TradeType.MARKET_ORDER:
                                    tradeType = "Market order";
                                    break;
                                case TradeType.PRICE_BREAK_ORDER:
                                    tradeType = "Price break";
                                    break;
                                case TradeType.LINE_BREAK_ORDER:
                                    tradeType = "Line break";
                                    break;
                                case TradeType.HMA_BREAK_ORDER:
                                    tradeType = "HMA break";
                                    break;
                            }
                            return tradeType;
                        }

                        sb.Append("**************************************************************************************************************************************************\n");

                        #region running base asset trades
                        sb.Append("Running trades:\n");
                        //e.g. Running: Binance BNBBTC   Price +0.5%   Stop -1%    Take profit +16%  (Market Break)
                        foreach (var trade in enteredTrades.OrderBy(x => x.Timestamp))
                        {
                            if (currentSymbolList.TryGetValue(trade.SymbolName, out _SymbolDataItem currentSymbol))
                            {

                                var s = trade.ExchangeStatus == TradeExchangeStatus.ENTERED_ON_EXCHANGE ? " " : "Test ";
                                s += trade.ExchangeMethodScript == null ? "" : "INTERRUPTED";

                                //sb.Append(string.Concat(Pad12(trade.SymbolName)));

                                //var priceDiff = -MainObj.GetPriceDiff(trade.Price, currentSymbol.Close, trade.Position) * 100;
                                //sb.Append(Pad20(string.Format("Price {0}{1}%", priceDiff < 0 ? "" : "+", priceDiff.ToString("F"))));
                                //sb.Append(string.Concat("    ", Enum.GetName(typeof(TradeType), trade.Type)));

                                var entry = db.Trades.Where(x => x.TradeReference == trade.TradeReference && x.Action == TradeAction.ENTRY).FirstOrDefault();
                                //var entryString = entry != null
                                //    ? $",  entered {entry.Timestamp:g} at {entry.Price}"
                                //    : string.Empty;

                                //sb.Append(Pad20(entryString));
                                //sb.Append(s);
                                //sb.Append("\n");
                                var priceDiff = -MainObj.GetPriceDiff(trade.Price, currentSymbol.Close, trade.Position) * 100;
                                var pc = string.Format("Price {0}{1}%", priceDiff < 0 ? "" : "+", priceDiff.ToString("F"));
                                var symbolName = SymbolNames.GetBaseAssetForSymbol(trade.SymbolName) ?? trade.SymbolName;
                                if (string.IsNullOrEmpty(symbolName))
                                {
                                    symbolName = trade.SymbolName;
                                }
                                var pos = entry.Position == TradePosition.LONG ? "long" : "short";

                                sb.Append($"{symbolName}\t" +
                                   $"{entry.Timestamp:d}\t" +
                                   $"{entry.Timestamp:t}\t" +
                                   $"{PriceFormatter.Format(entry.Price)}\t\t" +
                                   $"{pos}\t"
                                   // + $"{profitLoss.ToString("F")}\t" +
                                   //$"{GetTradeType(close)}\t" +
                                   //$"{close.TimeFrame}\t" +
                                   //$"{script}" +
                                   //$"{s}"
                                   );

                                sb.Append("\n");
                            }
                        }

                        #endregion

                        #region running quote asset trade
                        //foreach (var trade in enteredTrades.OrderBy(x => x.ExchangeId).ThenBy(x => x.SymbolName))
                        //{
                        //    if (currentSymbolList.TryGetValue(trade.SymbolName, out SymbolDataItem currentSymbol))
                        //    {
                        //        bool isQuoteAsset = false;
                        //        foreach (var quoteAsset in SymbolNamesBinance.QUOTE_ASSETS)
                        //        {
                        //            if (trade.SymbolName == quoteAsset)
                        //            {
                        //                isQuoteAsset = true;
                        //                break;
                        //            }
                        //        }
                        //        if (isQuoteAsset)
                        //        {
                        //            var s = trade.ExchangeStatus == TradeExchangeStatus.ENTERED_ON_EXCHANGE ? " " : "Test ";
                        //            s += trade.ExchangeMethodScript == null ? "" : " INTERRUPTED";

                        //            sb.Append(string.Concat("\n", Pad12(trade.SymbolName)));

                        //            var priceDiff = -MainObj.GetPriceDiff(trade.Price, currentSymbol.Close, trade.Position) * 100;
                        //            sb.Append(Pad20(string.Format("Price {0}{1}%", priceDiff < 0 ? "" : "+", priceDiff.ToString("F"))));
                        //            sb.Append(string.Concat("    ", Enum.GetName(typeof(TradeType), trade.Type)));

                        //            var entry = db.Trades.Where(x => x.TradeReference == trade.TradeReference && x.Action == TradeAction.ENTRY).FirstOrDefault();
                        //            var entryString = entry != null
                        //                ? $",  entered {entry.Timestamp:g} at {entry.Price}"
                        //                : string.Empty;

                        //            sb.Append(Pad20(entryString));
                        //            sb.Append(s);
                        //            sb.Append("\n");
                        //        }
                        //    }
                        //}

                        #endregion

                        sb.Append("**************************************************************************************************************************************************\n");

                        #region waiting trades
                        //e.g. Waiting: Binance ALGOBTC  
                        sb.Append("Waiting trades:\n");

                        foreach (var trade in waitingTrades.OrderBy(x => x.ExchangeId).ThenBy(x => x.SymbolName))
                        {
                            var pos = trade.Position == TradePosition.LONG ? "long" : "short";
                            sb.Append(string.Concat(
                                
                                Pad20(trade.SymbolName),
                                Pad12($"({GetTradeType(trade)})\t"),
                                $"{pos}",
                                "\n"));
                        }

                        #endregion

                        sb.Append("**************************************************************************************************************************************************\n");

                        #region closed trades
                        sb.Append("Closed trades:\n");
                        //e.g. Closed: Binance THETABTC +2.3%  (Market Break)
                        foreach (var close in closedOrders.OrderBy(x => x.Timestamp))
                        {
                            var s = close.ExchangeStatus == TradeExchangeStatus.ENTERED_ON_EXCHANGE ? "" : "Test closed:  ";
                            var entry = db.Trades.Where(x => x.TradeReference == close.TradeReference && x.Action == TradeAction.ENTRY).FirstOrDefault();
                            //var entryString = entry != null
                            //    ? $" entered {entry.Timestamp:g} at {entry.Price}"
                            //    : string.Empty;

                            //var closeString = $", closed {trade.Timestamp:g} at {trade.Price}";
                            //sb.Append(string.Concat(     Pad20(trade.SymbolName),
                            //                            Pad12($"{trade.Result.ToString("F")}%"),
                            //                            entryString,

                            //                            closeString,
                            //                            "\n"));

                            if (entry != null)
                            {
                                var symbolName = SymbolNames.GetBaseAssetForSymbol(close.SymbolName) ?? close.SymbolName;
                                if (string.IsNullOrEmpty(symbolName))
                                {
                                    symbolName = close.SymbolName;
                                }

                                //bitmex has a close quantity of $ (contract value * leverage) and a close price in BTC $ value
                                //binance has a close quantity of BTC and a close price of BTC $ value
                                double entryEquity = 0;
                                double closeEquity = 0;
                                switch (TradeEngine.EXCHANGE)
                                {
                                    case ExchangeId.BINANCE:
                                        entryEquity = entry.Quantity * entry.Price;
                                        closeEquity = close.Quantity * close.Price;
                                        break;

                                    case ExchangeId.BITMEX:
                                        entryEquity = entry.Quantity;
                                        closeEquity = close.Quantity;
                                        //entry and close quantities are always the same for a 100% close, so calculate profit / loss from %

                                        break;
                                }
                                var profitLoss = entryEquity * close.Result / 100 * (close.Position == TradePosition.LONG ? 1 : -1);
                                var leverage = close.Leverage == 0 ? 1 : close.Leverage; //just to be safe

                                var pos = close.Position == TradePosition.LONG ? "long" : "short";
                                var script = close.ExchangeMethodScript == TradeEngine.CLOSE_ORDER_SCRIPT || string.IsNullOrEmpty(close.ExchangeMethodScript) ? "" : close.ExchangeMethodScript;
                                sb.Append($"{symbolName}\t" +
                                    $"{entry.Timestamp:d}\t" +
                                    $"{entry.Timestamp:t}\t" +
                                    $"{PriceFormatter.Format(entry.Price)}\t" +
                                    $"{close.Timestamp:d}\t" +
                                    $"{close.Timestamp:t}\t" +
                                    $"{PriceFormatter.Format(close.Price)}\t" +
                                    $"  {close.Result.ToString("F")}%\t" +
                                    $"  ${Math.Abs(entryEquity / leverage).ToString("F")}\t" +
                                    $"  {profitLoss.ToString("F2")}\t" +
                                    $"  {leverage}x\t" +
                                    $"{pos}\t"
                                   // + $"{profitLoss.ToString("F")}\t" +
                                    //$"{GetTradeType(close)}\t" +
                                    //$"{close.TimeFrame}\t" +
                                    //$"{script}" +
                                    //$"{s}"
                                    );
                                sb.Append("\n");
                            }
                        }

                        var total = closedOrders.Sum(x => x.Result);
                        sb.Append(string.Concat("Total: ", total.ToString("F"), "%\n"));

                        #endregion

                        sb.Append("**************************************************************************************************************************************************\n");

                        MainObj.TradeSummary = sb.ToString();

                        //var activeTrades = allTrades.Where(x => x.Action == TradeAction.ORDER && x.GlobalStatus == TradeGlobalStatus.ACTIVE).ToList();
                        return (enteredTrades, waitingTrades);
                    }
                });

                return ret;
            }
            catch (Exception ex)
            {
                LogConnection.WriteLine("ActiveTradeSummary exception: " + ex.ToString());
                return (null, null);
            }

            

        }

        internal static async Task ClearClosedTrades(object param)
        {
            int.TryParse(param as string, out int time);
            LastClosedTimeToShow = DateTime.UtcNow.AddHours(-time);
            //LastClosedTradesTimeframe = time;
            await Update(null);
        }

        static string Pad12(string s)
        {
            return s != null ? s.PadRight(12) : string.Empty;
        }

        static string Pad20(string s)
        {
            return s != null ? s.PadRight(20) : string.Empty;
        }
    }
}
