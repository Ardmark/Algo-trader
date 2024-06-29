/*just save as csv and open in spreadsheet*/

select symbolname, sum(result) as results, count(result) as trades
from dbo.Trades 
where result <> 0 and timestamp > '2021-04-03' 
group by symbolname 
order by results

/*select result, timestamp from dbo.Trades where symbolname = 'cvcbtc' and result <> 0*/