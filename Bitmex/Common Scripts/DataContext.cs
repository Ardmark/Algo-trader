namespace AlgoTrader.Shared
{
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    using System.Data.Entity.SqlServer;

    /*
     * DataContext is in Backtest project as pre-generated mapping views don't work in the shared project
     * https://docs.microsoft.com/en-us/ef/ef6/fundamentals/performance/pre-generated-views
     * MUST R-CLICK THIS FILE AND GENERATE VIEWS FOR EF WHEN MODEL CHANGES
     * To stop using pre-generated views, just delete the DataContext.Views file
     * This does not seem to make much difference, using AsParallel() in the query seems to fix the slow load issue
     * - this returns IParallelEnumerable objects which work the same as IEnumerable
     * 
     * */


    //Handling transaction commit failures: see https://docs.microsoft.com/en-gb/ef/ef6/fundamentals/connection-resiliency/commit-failures?redirectedfrom=MSDN
    public class TransactionConfig : DbConfiguration
    {
        public TransactionConfig()
        {
            SetTransactionHandler(SqlProviderServices.ProviderInvariantName, () => new CommitFailureHandler());
            SetExecutionStrategy(SqlProviderServices.ProviderInvariantName, () => new SqlAzureExecutionStrategy());
        }
    }
    

    public class DataContext : DbContext
    {
        // Your context has been configured to use a 'DataContext' connection string from your application's 
        // configuration file (App.config or Web.config). By default, this connection string targets the 
        // 'Backtest.DataObjects.DataContext' database on your LocalDb instance. 
        // 
        // If you wish to target a different database and/or database provider, modify the 'DataContext' 
        // connection string in the application configuration file.
        public DataContext()
            : base("name=DataContext")
        {
    //        var objectContext = (this as System.Data.Entity.Infrastructure.IObjectContextAdapter).ObjectContext;// Get the ObjectContext related to this DbContext
     //       objectContext.CommandTimeout = 120;// Sets the command timeout for all the commands

        }
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<_Trade>().HasIndex(p => new { p.Timestamp });
           // modelBuilder.Entity<_SymbolDataItem>().HasIndex(p => new { p.Timestamp });

            // Change table names
            modelBuilder.Entity<_Trade>().ToTable("_Trades");
            modelBuilder.Entity<_ChartLine>().ToTable("_ChartLines");
        }

        // Add a DbSet for each entity type that you want to include in your model. For more information 
        // on configuring and using a Code First model, see http://go.microsoft.com/fwlink/?LinkId=390109.

        //public virtual DbSet<_SymbolDataItem> SymbolDataItems { get; set; }
        public virtual DbSet<_Trade> Trades { get; set; }
        public virtual DbSet<_ChartLine> Lines { get; set; }
    }




}
 