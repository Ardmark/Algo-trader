/*Timestamp should be yyyy-mm-dd hh:00 from previous query */

insert into [AlgoTrader.DataContext.Live].dbo.SymbolDataItems ([Timestamp],[Open],[Close],[High],[Low],SymbolName,ExchangeId,Interrupted)
		 select [Timestamp],[Open],[Close],[High],[Low],SymbolName,ExchangeId,Interrupted from [AlgoTrader.DataContext.Archive].dbo.SymbolDataItems 
		 where Timestamp < '2021-10-01 00:00:00.000'
