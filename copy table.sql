/*
    This will copy data from the live DB to the backtesting DB, and can filter out date range and symbols

	+++++++ ENSURE DESTINATION DATABASE IS AlgoTrader.DataContext.Backtest+++++++++
	+++++++ OR WILL OVERWRITE AND LOSE ALL SAVED DATA!!!!!!!!!       ++++++++
*/

/* Do not backtest on BTCUSDT as the quote asset returned will be BTC not USDT from the simulated exchange*/
/* Timestamp format is yyyy-mm-dd */

/* Must select the object table as project, not the target table */
drop table [AlgoTrader.Backtest].dbo.ADAUSDT
drop table [AlgoTrader.Backtest].dbo.ATOMUSDT
drop table [AlgoTrader.Backtest].dbo.BNBUSDT
drop table [AlgoTrader.Backtest].dbo.EOSUSDT
drop table [AlgoTrader.Backtest].dbo.ETHUSDT
drop table [AlgoTrader.Backtest].dbo.LUNAUSDT
drop table [AlgoTrader.Backtest].dbo.POLYXUSDT
drop table [AlgoTrader.Backtest].dbo.RVNUSDT
drop table [AlgoTrader.Backtest].dbo.TRXUSDT
drop table [AlgoTrader.Backtest].dbo.XRPUSDT
drop table [AlgoTrader.Backtest].dbo.XECUSDT
drop table [AlgoTrader.Backtest].dbo.XLMUSDT
drop table [AlgoTrader.Backtest].dbo.ACAUSDT
drop table [AlgoTrader.Backtest].dbo.ANKRUSDT
drop table [AlgoTrader.Backtest].dbo.BATUSDT
drop table [AlgoTrader.Backtest].dbo.BNXUSDT
drop table [AlgoTrader.Backtest].dbo.CAKEUSDT
drop table [AlgoTrader.Backtest].dbo.CKBUSDT
drop table [AlgoTrader.Backtest].dbo.DCRUSDT
drop table [AlgoTrader.Backtest].dbo.DOGEUSDT
drop table [AlgoTrader.Backtest].dbo.ETCUSDT
drop table [AlgoTrader.Backtest].dbo.FILUSDT
drop table [AlgoTrader.Backtest].dbo.FLUXUSDT
drop table [AlgoTrader.Backtest].dbo.HOTUSDT
drop table [AlgoTrader.Backtest].dbo.KNCUSDT
drop table [AlgoTrader.Backtest].dbo.MATICUSDT
drop table [AlgoTrader.Backtest].dbo.PUNDIXUSDT
drop table [AlgoTrader.Backtest].dbo.QNTUSDT
drop table [AlgoTrader.Backtest].dbo.QTUMUSDT
drop table [AlgoTrader.Backtest].dbo.REEFUSDT
drop table [AlgoTrader.Backtest].dbo.RLCUSDT
drop table [AlgoTrader.Backtest].dbo.STORJUSDT
drop table [AlgoTrader.Backtest].dbo.SYSUSDT
drop table [AlgoTrader.Backtest].dbo.UNIUSDT
drop table [AlgoTrader.Backtest].dbo.VGXUSDT
drop table [AlgoTrader.Backtest].dbo.WAVESUSDT
drop table [AlgoTrader.Backtest].dbo.XMRUSDT
drop table [AlgoTrader.Backtest].dbo.XTZUSDT
drop table [AlgoTrader.Backtest].dbo.ZENUSDT
drop table [AlgoTrader.Backtest].dbo.ZRXUSDT
drop table [AlgoTrader.Backtest].dbo.BTCUSDT


select * into [AlgoTrader.Backtest].dbo.ADAUSDT	   from [AlgoTrader.Binance].dbo.ADAUSDT  
select * into [AlgoTrader.Backtest].dbo.ATOMUSDT   from [AlgoTrader.Binance].dbo.ATOMUSDT
select * into [AlgoTrader.Backtest].dbo.BNBUSDT	   from [AlgoTrader.Binance].dbo.BNBUSDT
select * into [AlgoTrader.Backtest].dbo.EOSUSDT	   from [AlgoTrader.Binance].dbo.EOSUSDT
select * into [AlgoTrader.Backtest].dbo.ETHUSDT	   from [AlgoTrader.Binance].dbo.ETHUSDT
select * into [AlgoTrader.Backtest].dbo.LUNAUSDT   from [AlgoTrader.Binance].dbo.LUNAUSDT
select * into [AlgoTrader.Backtest].dbo.POLYXUSDT  from [AlgoTrader.Binance].dbo.POLYXUSDT
select * into [AlgoTrader.Backtest].dbo.RVNUSDT	   from [AlgoTrader.Binance].dbo.RVNUSDT
select * into [AlgoTrader.Backtest].dbo.TRXUSDT	   from [AlgoTrader.Binance].dbo.TRXUSDT
select * into [AlgoTrader.Backtest].dbo.XRPUSDT	   from [AlgoTrader.Binance].dbo.XRPUSDT
select * into [AlgoTrader.Backtest].dbo.XECUSDT	   from [AlgoTrader.Binance].dbo.XECUSDT
select * into [AlgoTrader.Backtest].dbo.XLMUSDT	   from [AlgoTrader.Binance].dbo.XLMUSDT
select * into [AlgoTrader.Backtest].dbo.ACAUSDT	   from [AlgoTrader.Binance].dbo.ACAUSDT
select * into [AlgoTrader.Backtest].dbo.ANKRUSDT   from [AlgoTrader.Binance].dbo.ANKRUSDT
select * into [AlgoTrader.Backtest].dbo.BATUSDT	   from [AlgoTrader.Binance].dbo.BATUSDT
select * into [AlgoTrader.Backtest].dbo.BNXUSDT	   from [AlgoTrader.Binance].dbo.BNXUSDT
select * into [AlgoTrader.Backtest].dbo.CAKEUSDT   from [AlgoTrader.Binance].dbo.CAKEUSDT
select * into [AlgoTrader.Backtest].dbo.CKBUSDT	   from [AlgoTrader.Binance].dbo.CKBUSDT
select * into [AlgoTrader.Backtest].dbo.DCRUSDT	   from [AlgoTrader.Binance].dbo.DCRUSDT
select * into [AlgoTrader.Backtest].dbo.DOGEUSDT   from [AlgoTrader.Binance].dbo.DOGEUSDT
select * into [AlgoTrader.Backtest].dbo.ETCUSDT	   from [AlgoTrader.Binance].dbo.ETCUSDT
select * into [AlgoTrader.Backtest].dbo.FILUSDT	   from [AlgoTrader.Binance].dbo.FILUSDT
select * into [AlgoTrader.Backtest].dbo.FLUXUSDT   from [AlgoTrader.Binance].dbo.FLUXUSDT
select * into [AlgoTrader.Backtest].dbo.HOTUSDT	   from [AlgoTrader.Binance].dbo.HOTUSDT
select * into [AlgoTrader.Backtest].dbo.KNCUSDT	   from [AlgoTrader.Binance].dbo.KNCUSDT
select * into [AlgoTrader.Backtest].dbo.MATICUSDT  from [AlgoTrader.Binance].dbo.MATICUSDT
select * into [AlgoTrader.Backtest].dbo.PUNDIXUSDT from [AlgoTrader.Binance].dbo.PUNDIXUSDT
select * into [AlgoTrader.Backtest].dbo.QNTUSDT	   from [AlgoTrader.Binance].dbo.QNTUSDT
select * into [AlgoTrader.Backtest].dbo.QTUMUSDT   from [AlgoTrader.Binance].dbo.QTUMUSDT
select * into [AlgoTrader.Backtest].dbo.REEFUSDT   from [AlgoTrader.Binance].dbo.REEFUSDT
select * into [AlgoTrader.Backtest].dbo.RLCUSDT	   from [AlgoTrader.Binance].dbo.RLCUSDT
select * into [AlgoTrader.Backtest].dbo.STORJUSDT  from [AlgoTrader.Binance].dbo.STORJUSDT
select * into [AlgoTrader.Backtest].dbo.SYSUSDT	   from [AlgoTrader.Binance].dbo.SYSUSDT
select * into [AlgoTrader.Backtest].dbo.UNIUSDT	   from [AlgoTrader.Binance].dbo.UNIUSDT
select * into [AlgoTrader.Backtest].dbo.VGXUSDT	   from [AlgoTrader.Binance].dbo.VGXUSDT
select * into [AlgoTrader.Backtest].dbo.WAVESUSDT  from [AlgoTrader.Binance].dbo.WAVESUSDT
select * into [AlgoTrader.Backtest].dbo.XMRUSDT	   from [AlgoTrader.Binance].dbo.XMRUSDT
select * into [AlgoTrader.Backtest].dbo.XTZUSDT	   from [AlgoTrader.Binance].dbo.XTZUSDT
select * into [AlgoTrader.Backtest].dbo.ZENUSDT	   from [AlgoTrader.Binance].dbo.ZENUSDT
select * into [AlgoTrader.Backtest].dbo.ZRXUSDT	   from [AlgoTrader.Binance].dbo.ZRXUSDT
select * into [AlgoTrader.Backtest].dbo.BTCUSDT    from [AlgoTrader.Binance].dbo.BTCUSDT
		 
--drop table [AlgoTrader.DataContext.Backtest].dbo._Trade

--select * into [AlgoTrader.DataContext.Backtest].dbo._Trade 
--		 from [AlgoTrader.DataContext.Live].dbo._Trade  
--		 where symbolname='vgxusdt'


--drop table [AlgoTrader.DataContext.Backtest].dbo._ChartLines

--select * into [AlgoTrader.DataContext.Backtest].dbo._ChartLines 
--		 from [AlgoTrader.DataContext.Live].dbo._ChartLines  
--		 where symbolname='vgxusdt'
	 
	 
