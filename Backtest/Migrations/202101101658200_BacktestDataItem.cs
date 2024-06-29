namespace AlgoTrader.Backtest.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class BacktestDataItem : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BacktestDataItems",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Timestamp = c.DateTime(nullable: false),
                        Open = c.Double(nullable: false),
                        Close = c.Double(nullable: false),
                        High = c.Double(nullable: false),
                        Low = c.Double(nullable: false),
                        SymbolName = c.String(),
                        ExchangeId = c.Int(nullable: false),
                        Interrupted = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            DropColumn("dbo.Trades", "Order");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Trades", "Order", c => c.Int(nullable: false));
            DropTable("dbo.BacktestDataItems");
        }
    }
}
