/*
	to archive and delete items from LIVE, use the 3 scripts (cannot combine)
  - use full date and time to copy to ARCHIVE - can copy from the Result window cell
  - get this wrong and will get duplicates or gaps, either of which are a real problem

	Procedure: Get timestamp, copy from live to archive, backup archive - copy backup to all drives AND to OneDrive repo (no need to backup live)
	- can start to cull old items (older than say 180 days) from live, however this many days is needed to show a reasonably accurate 100ema on the day
	- MainObj will only show a certain number of days back anyway, but Live should be kept as small as possible
	
	DO NOT save incremental database backups, as the last dated item will be different in each one so that is very problematic
  */

select top 1 Timestamp from [AlgoTrader.DataContext.Archive].dbo.SymbolDataItems order by Timestamp desc