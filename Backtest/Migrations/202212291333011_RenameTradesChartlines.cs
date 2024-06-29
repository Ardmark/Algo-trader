namespace AlgoTrader.Backtest.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RenameTradesChartlines : DbMigration
    {
        public override void Up()
        {
            RenameTable(name: "dbo.ChartLines", newName: "_ChartLine");
            RenameTable(name: "dbo.Trades", newName: "_Trade");
            RenameTable(name: "dbo.SymbolDataItems", newName: "_SymbolDataItem");
            DropColumn("dbo._SymbolDataItem", "SymbolName");
        }
        
        public override void Down()
        {
            AddColumn("dbo._SymbolDataItem", "SymbolName", c => c.String());
            RenameTable(name: "dbo._SymbolDataItem", newName: "SymbolDataItems");
            RenameTable(name: "dbo._Trade", newName: "Trades");
            RenameTable(name: "dbo._ChartLine", newName: "ChartLines");
        }
    }
}
