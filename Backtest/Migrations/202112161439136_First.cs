namespace AlgoTrader.Backtest.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class First : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ChartLines",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Type = c.Int(nullable: false),
                        Style = c.Int(nullable: false),
                        Thickness = c.Double(nullable: false),
                        StartTimeStamp = c.DateTime(nullable: false),
                        EndTimeStamp = c.DateTime(nullable: false),
                        StartPrice = c.Double(nullable: false),
                        EndPrice = c.Double(nullable: false),
                        SymbolName = c.String(),
                        ExchangeId = c.Int(nullable: false),
                        TradeReference = c.String(),
                        IsVisible = c.Boolean(nullable: false),
                        Opacity = c.Double(nullable: false),
                        Red = c.Byte(nullable: false),
                        Green = c.Byte(nullable: false),
                        Blue = c.Byte(nullable: false),
                        Alpha = c.Byte(nullable: false),
                        TradeId = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Trades", t => t.TradeId)
                .Index(t => t.TradeId);
            
            CreateTable(
                "dbo.Trades",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TradeReference = c.String(),
                        ItemId = c.String(),
                        TradeIdExchange = c.Long(nullable: false),
                        Quantity = c.Double(nullable: false),
                        Type = c.Int(nullable: false),
                        Position = c.Int(nullable: false),
                        Action = c.Int(nullable: false),
                        System = c.Int(nullable: false),
                        Variant = c.Int(nullable: false),
                        Stop = c.Int(nullable: false),
                        ItemStatus = c.Int(nullable: false),
                        GlobalStatus = c.Int(nullable: false),
                        ExecutionSystem = c.Int(nullable: false),
                        OrderType = c.Int(nullable: false),
                        Timestamp = c.DateTime(nullable: false),
                        ActionValue = c.Double(nullable: false),
                        TimeFrame = c.Int(nullable: false),
                        Price = c.Double(nullable: false),
                        Result = c.Double(nullable: false),
                        SymbolName = c.String(),
                        ExchangeId = c.Int(nullable: false),
                        Leverage = c.Double(nullable: false),
                        ExchangeRequestIntent = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Timestamp);
            
            CreateTable(
                "dbo.SymbolDataItems",
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
                .PrimaryKey(t => t.Id)
                .Index(t => t.Timestamp);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.ChartLines", "TradeId", "dbo.Trades");
            DropIndex("dbo.SymbolDataItems", new[] { "Timestamp" });
            DropIndex("dbo.Trades", new[] { "Timestamp" });
            DropIndex("dbo.ChartLines", new[] { "TradeId" });
            DropTable("dbo.SymbolDataItems");
            DropTable("dbo.Trades");
            DropTable("dbo.ChartLines");
        }
    }
}
