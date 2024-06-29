delete from [AlgoTrader.DataContext.Live].dbo.Trades 
where Timestamp < '2021-04-15 09:00:00.000' 

set identity_insert [AlgoTrader.DataContext.Live].dbo.Trades on
insert into [AlgoTrader.DataContext.Live].dbo.Trades (Id, TradeId, Type, Position, Action, System, Variant, Timestamp, ActionValue, TimeFrame, Price, Result, ExecutionSystem, Stop, ItemStatus, GlobalStatus, SymbolName, ExchangeId, Leverage, ChartLineId, OrderType, TradeIdExchange, ItemId, Quantity, ExchangeRequestIntent)
select Id, TradeId, Type, Position, Action, System, Variant, Timestamp, ActionValue, TimeFrame, Price, Result, ExecutionSystem, Stop, ItemStatus, GlobalStatus, SymbolName, ExchangeId, Leverage, ChartLineId, OrderType, TradeIdExchange, ItemId, Quantity, ExchangeRequestIntent 
from [AlgoTrader.DataContext.Backtest].dbo.Trades
where Timestamp < '2021-04-15 09:00:00.000' 
set identity_insert [AlgoTrader.DataContext.Live].dbo.Trades off


delete from [AlgoTrader.DataContext.Live].dbo.ChartLines 
where EndTimeStamp < '2021-04-15 09:00:00.000' 

set identity_insert [AlgoTrader.DataContext.Live].dbo.ChartLines on
insert into [AlgoTrader.DataContext.Live].dbo.ChartLines (Id, Type, Style, Thickness, SymbolName, ExchangeId, TradeId, IsVisible, Opacity, Red, Green, Blue, Alpha, StartTimeStamp, EndTimeStamp, StartPrice, EndPrice)
select Id, Type, Style, Thickness, SymbolName, ExchangeId, TradeId, IsVisible, Opacity, Red, Green, Blue, Alpha, StartTimeStamp, EndTimeStamp, StartPrice, EndPrice 
from [AlgoTrader.DataContext.Backtest].dbo.ChartLines
where EndTimeStamp < '2021-04-15 09:00:00.000' 
set identity_insert [AlgoTrader.DataContext.Live].dbo.ChartLines off