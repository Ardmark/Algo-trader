/* Timestamp format is yyyy-mm-dd */
delete from [AlgoTrader.DataContext.Live].dbo.SymbolDataItems
		 where Timestamp < '2021-10-01'
