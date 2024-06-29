using AlgoTrader.Shared;
using AlgoTrader.Shared.ExchangeConnection;

namespace AlgoTrader.Windows
{
    public partial class MainWindow
    {
        void SetupConstants()
        {
            //****//
            //LIVE//
            //****//

            //Connection
            MainObj.BACKTEST = false; //set TRUE for testnet trading
            MainObj.READ_LIVE_DATA = true; //if true, will update items live. If writing to DB, this is ignored
            MainObj.WRITE_TO_DB = true;

            //Trading
            TradeEngine.EXCHANGE = Shared.Enums.ExchangeId.BITMEX;
            TradeEngine.USE_SIMULATED_EXCHANGE = false;
            TradeEngine.RESTRICTED_TRADE_VALUE = 100;

            TradeEngine.TAKE_PROFIT_PERCENT_PC = 33;
            TradeEngine.SET_TAKE_PROFIT_THRESHOLD_PC = 3;

            //in Binance, these are sent in the node script so these values are not in this project

            /*************************************************************/
            /* Set correct domain in BitMEXAPI.cs */
            /* API keys need 'Order', not 'Order Cancel' permission, as the latter is to only cancel orders */
            /*************************************************************/

            if (!MainObj.BACKTEST)
            {
                TradeEngineBitmex.KEY = "6_Y9ZZuQJLxl09BFTNNMHu8L"; //LIVE TRADING - bitmex.com, not testnet.bitmex.com
                TradeEngineBitmex.SECRET = "lr1tQ7L1p2p6O0UKryJ0GXSb3ay5EBEFylFfaVeXSDn2snMP";
                TradeEngineBitmex.DOMAIN = "https://www.bitmex.com";
            }
            else
            {
                TradeEngineBitmex.KEY = "p0Y99qfKEYSYjsXwNgvegSMp"; //TESTNET TRADING - testnet.bitmex.com
                TradeEngineBitmex.SECRET = "G6HCrOwgYU0MlRXLY4JiHCYKjKIfltOOAhkdGEg5rM0JzfHc";
                TradeEngineBitmex.DOMAIN = "https://testnet.bitmex.com";
            }

            //the default floor offset will be scaled by leverage, so if the offset is 20%, and leverage is 10X, the actual stop will be at 2%
            TradeEngineBitmex.DEFAULT_FLOOR_OFFSET_PERCENT = 6; //just to offset the default floor from the stop
            TradeEngineBitmex.MAX_LOSS_PC = 5; //max loss for calculation of auto leverage, typically the same as the default floor
            TradeEngineBitmex.MAX_LEVERAGE = 2;

            //Bitmex has a maximum order price of 1000000, and a short stop will excede this even though the liquidation price will be set automatically to well above this maximum.
            //Limit the value of any short stops, otherwise a Floor will not be set and this is just inconsistent even though it is rather pointless to set one anyway.
            TradeEngine.MAX_SHORT_STOP = 500000;

            TradeEngine.RETAINED_BNB_PERCENTAGE = 10;
            TradeEngine.MAX_CONCURRENT_TRADES = 1;

            TradeEngine.MAX_TRADES_PER_24_HOURS = 6;
            TradeEngine.MAX_FLIP_DIFF_PERCENT = 0.5;
            TradeEngine.MIN_ORDER_THRESHOLD = 0.05;
            TradeEngine.BREAK_EVEN_THRESHOLD = 0.5; //break even price for spike (if set)
            TradeEngine.STOPLINE_MIN_VALUE = 0.1; //min UI value for stop
            TradeEngine.FLOOR_STOP_MIN_VALUE = 0.2; //when trade enters, the stop or floor will be moved to below this percent from the trade, if closer
            TradeEngine.INITIAL_TAKE_PROFIT_PERCENT_MARKET = 0; //will auto-set t/p if this value is set
            TradeEngine.SPIKE_TRIGGER_PERCENT = 5; //trigger percent for spike
            TradeEngine.SPIKE_TRIGGER_FRAMES = 15;

            TradeEngine.NUM_BARS_FOR_FLOOR = 0;//when this many bars clear (higher lows) will set floor
            TradeEngine.MINIMUM_TRADE_TIME = 60; //lines will be set to this as a minimum length if shorter than this value
            TradeEngine.AUTO_EXTEND_LINES = true;

            //Display
            MainObj.NUM_DAYS_TO_SHOW = 90;
            MainObj.INITIAL_TIMEFRAME = 60;

            //////////////////////////////////////////////////////////////////////
            //Backup DB
#if !BACKTEST
            ExchangeConnector.NUM_INCREMENTAL_BACKUPS = 2; //set to zero to disable
            ExchangeConnector.BACKUP_FOLDER = "C:\\Users\\richa\\MyFiles\\Visual Studio Projects\\AlgoTrader DB Backups";
            ExchangeConnector.BACKUP_FREQUENCY_HOURS = 24;
#endif
            /*
             * Database will be backed up using the database name and the current time in ticks. Use the Modified time to get the last one saved
             * To restore:
             * 1) Must create the database first, this can be done in Microsoft SQL Server Management Studio but name MUST match the backed up database name
             * 2) In Microsoft SQL Server Management Studio:
             *      R-click Databases and choose Restore Database
             *      Select Device as source, click Add and browse to .bak file
             *      Select Destination database
             *      That's it.
             * 
             * Note, that a Bacpac file will be MUCH smaller, restoring from backup will also restore the .ldf file which will bloat.
             * To clean this out, do a Bacpac backup - restore.
             * */
            //////////////////////////////////////////////////////////////////////



        }
    }

}
