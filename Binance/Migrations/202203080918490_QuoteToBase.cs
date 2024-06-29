namespace AlgoTrader.Backtest.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class QuoteToBase : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Trades", "IsQuoteToBasePurchase", c => c.Boolean(nullable: false));
            AddColumn("dbo.Trades", "StubCallbackTradeReference", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Trades", "StubCallbackTradeReference");
            DropColumn("dbo.Trades", "IsQuoteToBasePurchase");
        }
    }
}
