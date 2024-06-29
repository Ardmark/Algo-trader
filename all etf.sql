drop table [AlgoTrader.DataContext.Backtest].dbo.SymbolDataItems

select * into [AlgoTrader.DataContext.Backtest].dbo.SymbolDataItems 
		 from [AlgoTrader.DataContext.Live].dbo.SymbolDataItems  
		 where timestamp > '2022-08-01'
and symbolname='ADAUPUSDT'
or symbolname='BNBUPUSDT'
or symbolname='DOTUPUSDT'
or symbolname='ETHUPUSDT'
or symbolname='LINKUPUSDT'
or symbolname='TRXUPUSDT'
or symbolname='XRPUPUSDT'
or symbolname='ADADOWNUSDT'
or symbolname='BNBDOWNUSDT'
or symbolname='DOTDOWNUSDT'
or symbolname='ETHDOWNUSDT'
or symbolname='LINKDOWNUSDT'
or symbolname='TRXDOWNUSDT'
or symbolname='XRPDOWNUSDT'


