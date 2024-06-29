using AlgoTrader.Windows;
using AlgoTrader.Shared;
using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using _Trade = AlgoTrader.Shared._Trade;

namespace AlgoTrader.Backtest.Backtesting
{
    public class Backtest_MarketOrder : Backtest_base
    {
        List<_Trade> runningMarketBreakOrders = new List<_Trade>();

        public Backtest_MarketOrder(IMainViewModel mainViewModel) : base(mainViewModel)  {}

        //There is no batch backtesting with market orders, so no RunBacktestForProperty method, etc.

        #region Init backtests
        public override void InitForBacktests(DataContext db, string symbolName)
        {
#if !BACKTEST
return;
#endif      //just to be safe  
            base.InitForBacktests(db, symbolName);


            backtestTradePositions = GetAllOrders(db, symbolName);
        }
        #endregion

        #region Get orders
        protected override List<_Trade> GetAllOrders(DataContext db, string symbolName)
        {
            //get all the market break orders and store in the tradeOrders list
            //this is for consistency with the auto orders system, but it also removes the need to query the db repeatedly during backtests
            List<_Trade> ret;
            var startTime = tradeEngine.USE_BACKTEST_START_DATE ? tradeEngine.BACKTEST_START_DATE : tradeEngine.firstTimestamp;   
            if (symbolName == null)
            {
                ret = db.Trades.Where(d =>
                        d.Type != TradeType.AUTO_ORDER &&
                        d.Action == TradeAction.ORDER &&
                        DateTime.Compare(d.Timestamp, startTime) >= 0).OrderBy(d => d.Timestamp).ToList();
            }
            else
            {

                ret = db.Trades.Where(d =>
                        d.Type != TradeType.AUTO_ORDER &&
                        d.Action == TradeAction.ORDER &&
                        d.SymbolName == symbolName &&
                        DateTime.Compare(d.Timestamp, startTime) >= 0).OrderBy(d => d.Timestamp).ToList();
            }

            foreach (var trade in ret)
            {
                trade.ExchangeStatus = TradeExchangeStatus.ENTERED_OFF_EXCHANGE;
            }

            return ret;
        }

        #endregion

        public override bool BacktestsAvailable()
        {
            var order = GetVisibleOrder();
            return order != null;
        }

    }
}
