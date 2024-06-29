namespace AlgoTrader.Backtest.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ExchangeMethod : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Trades", "ExchangeStatus", c => c.Int(nullable: false));
            AddColumn("dbo.Trades", "ExchangeMethodIndex", c => c.Int(nullable: false));
            AddColumn("dbo.Trades", "ExchangeMethodScript", c => c.String());
            DropColumn("dbo.Trades", "System");
            DropColumn("dbo.Trades", "Variant");
            DropColumn("dbo.Trades", "OrderType");
            DropColumn("dbo.Trades", "ExchangeRequestIntent");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Trades", "ExchangeRequestIntent", c => c.Int(nullable: false));
            AddColumn("dbo.Trades", "OrderType", c => c.Int(nullable: false));
            AddColumn("dbo.Trades", "Variant", c => c.Int(nullable: false));
            AddColumn("dbo.Trades", "System", c => c.Int(nullable: false));
            DropColumn("dbo.Trades", "ExchangeMethodScript");
            DropColumn("dbo.Trades", "ExchangeMethodIndex");
            DropColumn("dbo.Trades", "ExchangeStatus");
        }
    }
}
