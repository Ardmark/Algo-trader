using AlgoTrader.Shared;
using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Interfaces;
using System.Collections.Generic;
using System.Linq;
using _Trade = AlgoTrader.Shared._Trade;

namespace AlgoTrader.Backtest.Backtesting
{
    public class Backtest_AutoOrder_Test : Backtest_base
    {


        public Backtest_AutoOrder_Test(IMainViewModel mainViewModel) : base(mainViewModel) { }

        #region Init backtests
        public override void InitForBacktests(DataContext db, string symbolName)
        {
            base.InitForBacktests(db, symbolName);
            db.Trades.RemoveRange(db.Trades); //clear the lot
            db.SaveChanges();
            backtestTradePositions = GetAllOrders(db, null);
        }
        #endregion

        #region Get orders
        protected override List<AlgoTrader.Shared._Trade> GetAllOrders(DataContext db, string symbolName)
        {

            //generate a list of trades
            List<AlgoTrader.Shared._Trade> ret = new List<AlgoTrader.Shared._Trade>();

            foreach (var symbol in SymbolNames.GetSymbolNames())
            {
                //Backtest_execution.tradeEngine = MainObj.GetTradeEngineForSymbol(symbol);
                //int numFrames = Backtest_execution.tradeEngine.Results.Count;
                //for (int frame = 2; frame < numFrames; frame += MainObj.visibleChart.TimeFrame)
                //{
                //    var item = Backtest_execution.tradeEngine.Results[frame];
                //
                //    Trade newTrade = Backtest_execution.tradeEngine.autoOrderIndicator.TestForTradeEntry(item, frame, true);
                //
                //    if (newTrade != null)
                //    {
                //        newTrade.ActionValue = 0; //this is used by the SMA entry orders, which derives from this class
                //        newTrade.OrderType = OrderType.ENTERED_OFF_EXCHANGE;
                //        ret.Add(newTrade);
                //    }
                //
                //}
                var tradeEngine = MainObj.GetTradeEngineForSymbol(symbol);
                Backtest_execution.tradeEngine = tradeEngine;
                int numFrames = Backtest_execution.tradeEngine.Results.Count;

                (var results, _) = tradeEngine.GetResults_UI(0, numFrames, tradeEngine.TimeFrame);
                (var resultsHA, _) = tradeEngine.GetResultsHA_UI(0, numFrames, tradeEngine.TimeFrame);

                for (int frame = 1; frame < resultsHA.Count; frame ++)
                {
                    var currCandle = results[frame];
                    var currCandleHA = resultsHA[frame];
                    var prevCandleHA = resultsHA[frame - 1];
                    var currHeightHA = currCandleHA.Close - currCandleHA.Open;
                    var prevHeightHA = prevCandleHA.Close -prevCandleHA.Open;
                    if (currHeightHA > 0 && prevHeightHA < 0)
                    {
                        var trade =  new _Trade
                        {
                            //defaults are active
                            TradeReference = _Trade.GetNewTradeId(),
                            Timestamp = currCandleHA.Timestamp,
                            Price = currCandle.Close,
                            TimeFrame = tradeEngine.TimeFrame,
                            Type = TradeType.AUTO_ORDER,
                            Position = TradePosition.LONG,
                            Action = TradeAction.ORDER,
                            SymbolName = symbol,
                            ExchangeId = TradeEngine.EXCHANGE,
                            Leverage = 1
                        };
                        ret.Add(trade);

                        while (++frame < resultsHA.Count)
                        {
                            currCandleHA = resultsHA[frame];
                            currHeightHA = currCandleHA.Close - currCandleHA.Open;
                            if (currHeightHA < 0)
                            {
                                frame--;
                                break;
                            }
                        }
                    }
                

                }
            }
            return ret;
         
            //return ret.OrderBy(x => x.Timestamp).ToList();
        }

        public override bool BacktestsAvailable()
        {
            return true;
        }

        #endregion




    }
}
