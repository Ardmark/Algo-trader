using AlgoTrader.Shared;
using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Interfaces;
using AlgoTrader.Shared.TradeIndicators;
using AlgoTrader.Shared.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using _Trade = AlgoTrader.Shared._Trade;

namespace AlgoTrader.Backtest.Backtesting
{
    public class Backtest_AutoOrder : Backtest_base
    {


        public Backtest_AutoOrder(IMainViewModel mainViewModel) : base(mainViewModel) { }

        #region Init backtests
        public override void InitForBacktests(DataContext db, string symbolName)
        {
            base.InitForBacktests(db, symbolName);
            db.Trades.RemoveRange(db.Trades); //clear the lot
            db.SaveChanges();
            var ret = GetAllOrders(db, symbolName);

            //since this is a basic system, not using indicator, can just save all the trades here rather than run each as a full test trade
            db.Trades.AddRange(ret);
        }
        #endregion

        public override bool StartSingleBacktest(_Trade tradeToBacktest = null)
        {
            using (DataContext dbBacktest = new DataContext())
            {
                //clear out all the sub-timeframe trades
                var startTime = tradeToBacktest.Timestamp;
                DateTime endTime = DateTime.UtcNow;

                //if the selected trade is a group trade, it has exited prematurely and needs to be reset to order
                var order = dbBacktest.Trades.Where(x => x.Id == tradeToBacktest.Id).FirstOrDefault();
                if (order != null)
                {
                    order.Action = TradeAction.ORDER;
                }

                //look for the next trade with this time, this will skip any sub-timeframe trades
                var endTrade = dbBacktest.Trades.Where(x => 
                    x.Timestamp > startTime && 
                    x.TimeFrame == tradeToBacktest.TimeFrame &&
                    (x.Action == TradeAction.CLOSE || x.Action == TradeAction.ORDER))
                    .FirstOrDefault();

                if (endTrade != null)
                {
                    endTime = endTrade.Timestamp;
                }

                //clear the sub-timeframe trades
                var subTimeframeTrades = dbBacktest.Trades.Where(x => x.Timestamp > startTime && x.Timestamp < endTime);
                dbBacktest.Trades.RemoveRange(subTimeframeTrades);

                dbBacktest.SaveChanges();
            }
                
            return base.StartSingleBacktest(tradeToBacktest);
        }
        public async override Task<bool> StartAllBacktests(string symbolName = null)
        {
#if !BACKTEST
return false;
#endif
            void DisplayCompletedPercent(double completed, double total)
            {
                mainViewModel.ProgressPercent = (completed / total * 100).ToString("0.00") + "%";
            }


            using (DataContext dbBacktest = new DataContext())
            {
                InitForBacktests(dbBacktest, symbolName);
                //MainObj.IsBacktesting = true;
                //double totResults = 0;
                //foreach (var symbol in SymbolNamesBinance.SymbolNames())
                //{
                //    if (symbolName != null && symbol != symbolName)
                //    {
                //        continue;
                //    }
                //    totResults += MainObj.GetTradeEngineForSymbol(symbol).Results.Count;
                //}
                //
                //double resultsComplete = 0;
                //foreach (var symbol in SymbolNamesBinance.SymbolNames())
                //{
                //    if (symbolName != null && symbol != symbolName)
                //    {
                //        continue;
                //    }
                //    await Task.Run(() =>
                //    {
                //        var tradeEngine = MainObj.GetTradeEngineForSymbol(symbol);
                //        var autoOrderIndicator = tradeEngine.autoOrderIndicator;
                //        var macdIndicator = autoOrderIndicator.macdIndicator;
                //        var results = macdIndicator.histogram.GetResultsFromDictionary(TimeFrame);
                //
                //        for (var frame = 1; frame < tradeEngine.Results.Count; frame++)
                //        {
                //            DisplayCompletedPercent(++resultsComplete, totResults);
                //
                //            if (macdIndicator.CanEnterTrade(TimeFrame, frame))
                //            {
                //                var item = tradeEngine.Results[frame];
                //                var entryTime = item.Timestamp;
                //                Trade order = new Trade
                //                {
                //                    TradeId = Trade.GetNewTradeId(),
                //                    Timestamp = entryTime,
                //                    Price = item.Close,
                //                    TimeFrame = TimeFrame,
                //                    Type = TradeType.AUTO_ORDER,
                //                    Position = TradePosition.LONG,
                //                    Action = TradeAction.ORDER,
                //                    System = TradeSystem.LIVE,
                //                    ExecutionSystem = TradeExecutionSystem.AUTO,
                //                    SymbolName = item.SymbolName,
                //                    ExchangeId = item.ExchangeId,
                //                    ItemStatus = TradeItemStatus.CLOSED,
                //                    GlobalStatus = TradeGlobalStatus.STOPPED
                //                };
                //
                //                db.Trades.Add(order);
                //
                //                for (; frame < tradeEngine.Results.Count; frame++)
                //                {
                //                    DisplayCompletedPercent(++resultsComplete, totResults);
                //
                //                    var item1 = tradeEngine.Results[frame];
                //                    if (
                //                        macdIndicator.TestForStopExit(TimeFrame, frame, entryTime, item1.Timestamp) ||
                //                        frame == tradeEngine.Results.Count - 1)
                //                    {
                //                        Trade close = order.Clone();
                //                        close.Action = TradeAction.CLOSE;
                //                        close.GlobalStatus = TradeGlobalStatus.STOPPED;
                //                        close.ItemStatus = TradeItemStatus.CLOSED;
                //                        close.Timestamp = item1.Timestamp;
                //                        close.Price = item1.Close;
                //                        close.Result = TradeEngine.GetPriceDiff(item1.Close, order.Price, TradePosition.LONG) * 100;
                //                        db.Trades.Add(close);
                //                        break;
                //                    }
                //                }
                //            }
                //        }
                //    });
                //}
                Chart.canDraw = true;
                dbBacktest.SaveChanges();
                mainViewModel.ProgressPercent = "100%";
                mainViewModel.BacktestResultEnabled = true;
                ResetBacktestMenus();
                var resultsWindow = new ResultsWindow();//do the summary! Don't put this in the calling method (command-event driven) as this will execute at the end of this method anyway
                resultsWindow.Show();
                MainObj.DrawCurrentDispatcher();
                MainObj.IsBacktesting = false;                    //MainObj.PlaySound(MainObj.ALERT_SOUND);
                return true;
            }
        }

        //implement unused overrides
        public override bool BacktestsAvailable()
        {
            return true;
        }


        protected override List<_Trade> GetAllOrders(DataContext db, string symbolName)
        {
            double tot = 0;
            //generate a list of trades
            List<AlgoTrader.Shared._Trade> ret = new List<AlgoTrader.Shared._Trade>();

            var tradeEngine = MainObj.GetTradeEngineForSymbol(symbolName);
            Backtest_execution.tradeEngine = tradeEngine;
            int numFrames = Backtest_execution.tradeEngine.Results.Count;

            (var results, _) = tradeEngine.GetResults_UI(0, numFrames, tradeEngine.TimeFrame);
            //results = results.Where(x => x.Timestamp > new System.DateTime(2021, 7, 11)).ToList();
            (var resultsHA, _) = tradeEngine.GetResultsHA_UI(0, numFrames, tradeEngine.TimeFrame);
            //resultsHA = resultsHA.Where(x => x.Timestamp > new System.DateTime(2021, 7, 11)).ToList();

            var indicator = new HeikinAshi_Indicator(tradeEngine, null);
            int count = 0;
            double totMins = 0;
            for (int frame = 1; frame < resultsHA.Count; frame++)
            {
                var e = indicator.CanEnterTrade(TimeFrame, frame, count, resultsHA); //count is one frame prior to [frame]
                if (e > 0)
                {
                    var currCandle = results[frame];
                    var currCandleHA = resultsHA[frame];
                    var entryPrice = currCandle.Open;
                    var trade = new _Trade
                    {
                        //defaults are active
                        TradeReference = _Trade.GetNewTradeId(),
                        Timestamp = currCandleHA.Timestamp,
                        Price = entryPrice,
                        TimeFrame = tradeEngine.TimeFrame,
                        Type = TradeType.AUTO_ORDER,
                        Position = TradePosition.LONG,
                        Action = TradeAction.ORDER,
                        SymbolName = symbolName,
                        ExchangeId = TradeEngine.EXCHANGE,
                        ItemStatus = TradeItemStatus.CLOSED,
                        GlobalStatus = TradeGlobalStatus.STOPPED,
                        //Variant = _isRising240 ? TradeVariant.ENTERED_STRONG : TradeVariant.ENTERED_WEAK //this improves matters
                        Leverage = 1
                    };
                    //ret.Add(trade);
                    var entry = trade.Clone();
                    entry.Action = TradeAction.ENTRY;
                    entry.ItemStatus = TradeItemStatus.CLOSED;
                    entry.GlobalStatus = TradeGlobalStatus.STOPPED;
                    ret.Add(entry);


                    while (++frame < resultsHA.Count)
                    {
                        count++;

                        if (indicator.CanExitTrade(TimeFrame, frame, count, resultsHA, results, entry))
                        { 
                            currCandleHA = resultsHA[frame];
                            currCandle = results[frame];

                            var exitPrice = currCandle.Open;
                            var close = trade.Clone();
                            close.Action = TradeAction.CLOSE;
                            close.ItemStatus = TradeItemStatus.CLOSED;
                            close.GlobalStatus = TradeGlobalStatus.STOPPED;
                            close.Price = exitPrice;
                            close.Timestamp = currCandleHA.Timestamp;
                            close.Result = entry.Result = trade.Result = -MainObj.GetPriceDiff(entryPrice, exitPrice, TradePosition.LONG) * 100;
                            tot += close.Result;
                            ret.Add(close);
                            totMins += (close.Timestamp - entry.Timestamp).TotalMinutes;
                            break;
                        }
                    }
                }
                count++;

            }

            var totTime = (results.Last().Timestamp - results.First().Timestamp).TotalMinutes;
            var timeUsed = totMins / totTime;

            //bypass the results window. This also filters for trades 'which should not have been entered' so this should probably be
            //abandoned for live results
            try
            {
                var text = SpreadsheetBuilder.BuildSpreadsheet(ret);
                var fileName = $"{symbolName}.fods";
                var path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "../../Results", fileName);
                File.WriteAllText(path, text);
            }
            catch { }
            return ret;

        }
    }
}
