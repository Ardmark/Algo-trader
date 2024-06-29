--delete from [AlgoTrader.Perpetual.Backtest].dbo.XBTUSD
--insert into [AlgoTrader.Perpetual.Backtest].dbo.XBTUSD([Timestamp],[Open],[Close],[High],[Low],Interrupted)
--		 select [Timestamp],[Open],[Close],[High],[Low],Interrupted from [AlgoTrader.Live].dbo.BTCUSDT
		


drop table [AlgoTrader.Backtest].dbo.XBTUSD
select * into [AlgoTrader.Backtest].dbo.XBTUSD	   from [AlgoTrader.Bitmex].dbo.XBTUSD  

drop table [AlgoTrader.Backtest].dbo._ChartLines
select * into [AlgoTrader.Backtest].dbo._ChartLines	   from [AlgoTrader.Bitmex].dbo._ChartLines

drop table [AlgoTrader.Backtest].dbo._Trades
select * into [AlgoTrader.Backtest].dbo._Trades	   from [AlgoTrader.Bitmex].dbo._Trades  


--------------------------------------------------------------------------------------------------------------