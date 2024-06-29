delete from [AlgoTrader.Backtest].dbo.XBTUSD
insert into [AlgoTrader.Backtest].dbo.XBTUSD([Timestamp],[Open],[Close],[High],[Low],Interrupted)
		 select [Timestamp],[Open],[Close],[High],[Low],Interrupted from [AlgoTrader.Bitmex].dbo.XBTUSD
		 where Timestamp < '2021-10-01 00:00:00.000'