using System.ComponentModel.DataAnnotations;

namespace AlgoTrader.Shared.Enums
{
    /*
     * Naming convention:
     * the rest API method name, all caps, split with an underscore
     * */

    #region Trade execution
    //ExchangeRequest - this wraps the various types of request which can be sent via API methods
    public enum ExchangeRequest
    {
        /* 0 */
        COMPLETED,
        /* 1 */
        EXCHANGE_INFO,
        /* 2 */
        ACCOUNT,
        /* 3 */
        QUOTE_PRICE,
        /* 4 */
        QUERY_ORDER,
        /* 5 */
        ALL_ORDERS,
        /* 6 */
        OPEN_ORDERS,
        /* 7 */
        MY_TRADES,
        /* 8 */
        CANCEL_ORDER,
        /* 9 */
        NEW_ORDER,
        /* 10 */
        NEW_OCO
    }


    //enums which map to actual strings required for parameter name-value pairs in REST api calls
    public enum ApiParamNames
    {
        //NOTE - when adding more, put in ExchangeDataProcessorBinance.AddRequest() as well, and keep order the same there as here for clarity
        SYMBOL, //symbol name
        TRADE_ID_ORIGINAL, //the TradeId for an existing trade
        TRADE_ID_NEW,       //when submitting a new trade, use this for the TradeId
        EXCHANGE_ORDER_ID,  //the exchange-generated order ID, can use this instead of TRADE_ID_ORIGINAL
        SIDE, //BUY or SELL
        ORDER_TYPE, //LIMIT, MARKET, STOP_LOSS, STOP_LOSS_LIMIT, TAKE_PROFIT, TAKE_PROFIT_LIMIT, LIMIT_MAKER
        QUOTE_ORDER_QTY,
        QUANTITY,
        ICEBERG_QUANTITY,
        PRICE,
        STOP_PRICE,
        RESPONSE_TYPE,
        TIME_IN_FORCE,
        TIMEOUT_MS, //this defaults to 5000ms. It is recommended to use this value or less, cannot go beyond 60000
        ORIG_CLIENT_ORDER_ID,

        /*
         * BUY - QuoteOrderQuantity will buy as much of the base asset as can be purchased for the underlying asset,
         *  so BNBBTC will buy as much BNB as can be purchased for a specified amount of BTC.
         * SELL - QuoteOrderQuantity will sell as much of the underlying asset as needed to receive the base asset amount specified
         *  so BNBBTC will sell as much BNB as needed to receive the specified amount of BTC, so the amount specified is in BTC NOT BNB
         *  it seems that, since the minimum trade of BNB is 0.01, the exchange will only trade in MULTIPLES of this amount. If the sell would
         *  require 0.015 BNB to receive the specified amount of BTC, it will only sell 0.01 BNB
         * */
        BUY,
        SELL,

        LIMIT,
        MARKET,
        STOP_LOSS,
        STOP_LOSS_LIMIT,
        LIMIT_MAKER,
        TAKE_PROFIT_LIMIT, //not used now?
        ABOVE_TYPE, //STOP_LOSS_LIMIT, STOP_LOSS, LIMIT_MAKER - limit maker should trigger the actual stop loss limit, presumable at the above stop price
        ABOVE_PRICE,
        ABOVE_STOP_PRICE,
        ABOVE_TIMEINFORCE,
        BELOW_TYPE,
        BELOW_PRICE,
        BELOW_STOP_PRICE,
        BELOW_TIMEINFORCE,
        ACK,
        RESULT,
        FULL,
    }

    public enum CandlePosition
    {
        OPEN,
        CLOSE,
        HIGH,
        LOW
    }

    public enum ExchangeId
    {
        [Display(Name = "Binance")]
        BINANCE = 1,
        [Display(Name = "Bitmex")]
        BITMEX
    }

    //Order frequency - used to ensure only a certain number of trades can run each 24 hours, and whether a trade can run live or not
    public enum OrderFrequency
    {
        NULL_ORDER,
        MAX_CONCURRENT_TRADES_EXCEDED,
        MAX_TRADES_PER_24_HOURS_EXCEDED,
        OK_TO_ENTER_LIVE
    } 
    #endregion

    #region Trade enums


    public enum TradeExchangeStatus 
    {  
        ENTERED_OFF_EXCHANGE, //order is not entered on the exchange, but runs anyway for testing
        ENTERED_ON_EXCHANGE,
        NEVER_ENTERED, //order was never entered,
        ENTERING_ON_EXCHANGE,
        NO_WARNING
    }
    // trade type - manual market break or auto-generated, etc.
    //NOTE - not implementing MARKET as that could be set as order type by mistake, resulting in an immediate entry
    // - similarily, LIMIT could be set without realising it, so limit orders will only be part of break orders, if ever implemented
    public enum TradeType
    {
        PRICE_BREAK_ORDER,  //Unused - for a break when a specific price is crossed. This is redundant as now all price breaks are done with lines
        LINE_BREAK_ORDER,   //price breaks line
        HMA_BREAK_ORDER,    //price breaks HMA
        MARKET_ORDER,       //immediate order
        STUB_ORDER,         //for use internally with quote asset trading (i.e. in and out of BTC, not USD(T)
        AUTO_ORDER,         //for auto-generated order
        FLIP_ORDER,         //for stops, this is a flip order (TradeAction Action will be STOP)
        NO_FLIP_ORDER,      //for stops, this will dead exit rather than flip (TradeAction Action will be WAITING_STOP)
        STOP_WAITING        //as above but dead stop (TradeAction Action will be WAITING_STOP)
    }

    // long or short
    public enum TradePosition
    {
        LONG,
        SHORT
    }

    // action - the core of the system. Each trade of this type is read, the appropriate one taken from any grouping, and action decided accordingly
    public enum TradeAction
    {
        ORDER,          
        ENTRY,                  
        CLOSE,          
        STOP,           //line-attached
        FLOOR,          //additional stop, not line attached
        CEILING,        //additional take profit, not line attached
        TAKE_PROFIT,
        BREAK_EVEN,
        WAITING_STOP
    }

    // trade status - either running or stopped, this applies to individual trades
    public enum TradeItemStatus
    {
        OPEN,
        CLOSED
    }

    // global status - this is the status of the entire trade, applies to all trades 
    public enum TradeGlobalStatus
    {
        ACTIVE,
        STOPPED
    }

    //this was used for auto entry, but now re-purposed. Auto entry would need to be defined globally now if ever used
    public enum TradeExecutionSystem
    {
        NORMAL,
        EXCHANGE_UNSYNCHED      //for floor or stop, this is set until exchange returns trade as set
    }

    //stop type, used only with TradeAction == STOP, although the default will be present on all orders obviously
    public enum TradeStop
    {
        STOP_ON_LINE_BREAK,              // - line (price break) stop
        TAKE_PROFIT_PERCENT,         //this is for a Floor which is auto-set at some percent between entry and current high
        TAKE_PROFIT_MA_CROSS,          //for a Stop which is set on a moving average (it is a stop but is only used as a take profit)
        STOP_TRAIL,             // - a trailing stop
        STOP_TRAIL_SPIKE,       // - for a spike event
        CEILING_TRIGGERED,       //for ceiling, allows ceiling value to be below actual line price
    }

    public enum TradeUpdateRequest
    {
        //create is handled by a zero Id, so if update is set on a new trade it will get added
        UPDATE = 1,
        DELETE,
        FORCE_DELETE
    }

    #endregion

    #region Graphics
    public enum LineType
    {
        NONE,
        BREAK,
        SUPPORT,
        RESISTANCE,
        CLOSED_ENTERED,
        CLOSED_UNENTERED
    }

    public enum LineStyle
    {
        SOLID,
        DOTTED,
        DASHED
    }
    
    public enum MarkerStyle
    {
        SQUARE,
        TRIANGLEUP,
        TRIANGLEDOWN,
        CIRCLE
    }
    #endregion

}
