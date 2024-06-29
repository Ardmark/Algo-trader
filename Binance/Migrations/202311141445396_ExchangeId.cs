namespace AlgoTrader.Backtest.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ExchangeId : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo._ChartLines", "ExchangeId");
        }
        
        public override void Down()
        {
            AddColumn("dbo._ChartLines", "ExchangeId", c => c.Int(nullable: false));
        }
    }
}
