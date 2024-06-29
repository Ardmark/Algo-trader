namespace AlgoTrader.Backtest.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RenameTradesChartlines1 : DbMigration
    {
        public override void Up()
        {
            RenameTable(name: "dbo._ChartLine", newName: "_ChartLines");
            RenameTable(name: "dbo._Trade", newName: "_Trades");
            DropIndex("dbo._SymbolDataItem", new[] { "Timestamp" });
            DropTable("dbo._SymbolDataItem");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo._SymbolDataItem",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Timestamp = c.DateTime(nullable: false),
                        Open = c.Double(nullable: false),
                        Close = c.Double(nullable: false),
                        High = c.Double(nullable: false),
                        Low = c.Double(nullable: false),
                        ExchangeId = c.Int(nullable: false),
                        Interrupted = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateIndex("dbo._SymbolDataItem", "Timestamp");
            RenameTable(name: "dbo._Trades", newName: "_Trade");
            RenameTable(name: "dbo._ChartLines", newName: "_ChartLine");
        }
    }
}
