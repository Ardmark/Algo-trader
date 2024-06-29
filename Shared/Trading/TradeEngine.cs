using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.Logging;
using AlgoTrader.Shared.MovingAverages;
using AlgoTrader.Shared.Windows;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
//using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Threading.Tasks;
using BitMEX;

namespace AlgoTrader.Shared
{
    public struct MarketEntry
    {
        public TradePosition position;
        public int frame;

        public MarketEntry(TradePosition position)
        {
            frame = 0;
            this.position = position;
        }
    }

    public class ExchangeEventArgs : EventArgs
    {
        public object Result { get; set; }
    }
    public partial class TradeEngine : DisplayPanel
    {

        #region Constants
        public const int MIN_RIGHT_DISPLAY_BARS_TO_SHOW_TRADE = 2;
        #endregion

        #region Trade engine parameters

        /*
         * To get concurrent result, run as backtest project, do one full backtest and just switch between ENTER_CONCURRENT_TRADES_AS_TESTS value, no need to do a backtest each time,
         *  this just works from whatever results are currently available
         *  
         * Issue with backtests: backtests are done per-symbol, so cannot account for concurrency of trades. They are all marked as entered on exchange (OrderType)
         * Therefore, must filter out in the results based on ENTER_CONCURRENT_TRADES_AS_TESTS.
         * Results (view model) will show either ONLY non-concurrent trades, or ALL trades regardless of concurrency. The backtest results will just show every trade as LIVE regardless.

         * Results window will only show live total in live project, regardless of ENTER_CONCURRENT_TRADES_AS_TESTS
         * To get all totalled, including concurrent trades, must get result in the backtest project, and have ENTER_CONCURRENT_TRADES_AS_TESTS set to true
         * 
         * */


#if !AUTO_ORDER
        #region Manual order parameters 

        //start hour
        //need to offset for bst
        //so when we want to start at 9 AM for example, the candle timestamp will be 8 AM so that is what we need
        //but - this will also be displayed, so need to add the time offset onto the display data as well when drawing time bar
        private static int startHour = 8;
        public static int START_HOUR
        {
            get 
            {
                //line up with BST, by offsetting into the data by 1 hour when necessary
                return startHour - BSTOffset;
            }
        }

        public static int BSTOffset
        {
            get
            {
                return (int) TimeZoneInfo.Local.GetAdjustmentRules()[0].DaylightDelta.TotalHours;
            }
        }

        //timeframe-specific parameters

        public static bool OVERRIDE_MAX_TRADES_LIMIT_BACKTESTING { get; set; } = true; //if true, will enter concurrent trades as tests; otherwise, will only enter live trades

        //for backtests, can have an arbitrary start date
        public bool USE_BACKTEST_START_DATE { get; set; } = false;
        public DateTime BACKTEST_START_DATE { get; set; } = new DateTime(2021, 4, 19);
        
        public bool EXECUTE_STOPS_ON_TIMEFRAME_CLOSE { get; set; } = false; //will only run stop test on hour, etc. to try to avoid spikes
        public int STOP_TIMEFRAME { get; set; } = 60; //timeframe for above
        
        public static double STOPLINE_MIN_VALUE { get; set; } = 1; //Do not set to zero. As above, but for moving stop lines. Closer for easier debugging, etc.
       
        //for price break, have an above order price limit above which it won't enter
        //public double MAX_ENTRY_THRESHOLD_OVER_BREAK { get; set; } = 0; //if more than this % above break, will not enter (i.e. spike of wick above break point) BUT must allow a reasonable margin for ema break entries, as the larger the gap, the faster the momentum up

        //floor
        public static double FLOOR_STOP_MIN_VALUE { get; set; } = 2; //Do not set to zero. This is the minimum percent which a stop can be set below the entry price, when trade runs.
        public double FLOOR_TRAIL { get; set; } = 0; //trailing extra stop, as a floor level. Will set to b/e (dead) when price excedes entry by this percent, then will trail up.Set to zero if not needed


        //initial take profit percent (set to zero to block setting TP initially)
        public static double INITIAL_TAKE_PROFIT_PERCENT_MARKET = 0; //set to any % to place an initial take profit  

        public bool ENTER_ONLY_ON_TIMEFRAME_CANDLE_CLOSE { get; set; } = false;

        public static double MIN_ORDER_THRESHOLD { get; set; } = 0.1;  
        public static double BREAK_EVEN_THRESHOLD { get; set; } = 0.5;  //percent above entry regarded as break-even, will show warning if attempt to set b/e below this

        public static double SET_TAKE_PROFIT_THRESHOLD_PC = 10; //percent above break even which will trigger a break-even (if no break even set manually)
        public static double TAKE_PROFIT_PERCENT_PC = 33; //will set a floor to track this difference between entry and current high
       // public static double BREAK_EVEN_PERCENT = 1; //set in Constants : this is the amount to set stop at when break-even trade is hit
        public static int NUM_BARS_FOR_FLOOR = 0; //set in Constants: when this many bars clear (higher lows) will set floor
        public static bool AUTO_EXTEND_LINES = true;
        //spike
        public static int SPIKE_TRIGGER_PERCENT;   //set to zero to disable spike. 
        public static int SPIKE_TRIGGER_FRAMES;
        public double SPIKE_PERCENT; // will split price from spike start to current price by this amount, and enter floor at that level
        //public double SPIKE_TRAIL_PERCENT { get; set; } = 6;


        public static int MINIMUM_TRADE_TIME = 60;

        #endregion
#else
        #region Auto order parameters
        public static bool ENTER_CONCURRENT_TRADES_AS_TESTS { get; set; } = false; //if true, will enter concurrent trades as tests; otherwise, will only enter live trades

        //for backtests, can have an arbitrary start date
        public bool USE_BACKTEST_START_DATE { get; set; } = false;
        public DateTime BACKTEST_START_DATE { get; set; } = new DateTime(2021, 4, 19);

        public static int EMA_LARGE_LENGTH { get; set; } = 10;
        public static int EMA_SMALL_LENGTH { get; set; } = 5;
        public static int SMA_LENGTH { get; set; } = 200;

        public bool EXIT_ON_SMA_BREAK_BELOW { get; set; } = false; //will exit when price crosses SMA
        public double MAX_ALLOWED_STOP_LOSS { get; set; } = 2; //won't allow more than this loss %, set to zero if not needed
        public bool EXIT_ON_EMA_CROSS_DOWN_IN_LOSS { get; set; } = true; //will exit when price crosses EMA if in loss position; otherwise, will only exit if in profit. Set to false if exiting on SMA break, as that must take precidence
        public double MIN_MINUTES_FOR_EMA_CROSS_DOWN_EXIT { get; set; } = 60; //won't exit ema trade until this time has passed, to avoid 1-min oscillation causing cross back below
        public bool SET_FLOOR_ON_EMA_CROSS_UP { get; set; } = true; //for an ema break entry, this will set a floor at 25% between entry and order, when order price is reached

        //for price break and ema break, have aa above order price limit above which it won't enter
        //this is problematic for ema break entries, as those tend to be way above the order price
        //public double MAX_ENTRY_THRESHOLD_OVER_BREAK { get; set; } = 0; //if more than this % above break, will not enter (i.e. spike of wick above break point) BUT must allow a reasonable margin for ema break entries, as the larger the gap, the faster the momentum up
        public bool ENTER_ON_EMA_BREAK { get; set; } = false; //for ema break orders, will enter on candle break instead of ema break if false - if entering on EMA break, then MAX_ENTRY_THRESHOLD_OVER_BREAK will be problematic if < 10%

        public double FLOOR_TRAIL { get; set; } = 0; //trailing extra stop, as a floor level. Will set to b/e (dead) when price excedes entry by this percent, then will trail up
        public static double MAX_FLOOR_TRAIL { get; set; } = 7.5; //floor will trail until profit percent reaches this value (ignore if FLOOR_TRAIL is zero)

        //initial take profit percent (set to zero to block setting TP initially)
        public double INITIAL_TAKE_PROFIT_PERCENT_MARKET { get; set; } = 0; //set to any % to place an initial take profit        //trailing stop

        public bool ENTER_ONLY_ON_TIMEFRAME_CANDLE_CLOSE { get; set; } = false;
        public bool ENTER_ONLY_ON_1_MIN_CANDLE_CLOSE { get; set; } = false;

        public double BREAK_EVEN_PERCENT { get; set; } = 0.5;  //stop won't go above this value over b/e, will just hold at that level

        //UNUSED

        public double TRAIL_PRICE_TO_FIXED { get; set; } = 8;   //at this percentage, will switch trail to fixed or incremental stop    
        public double WEAK_TRAIL_PERCENT { get; set; } = 4;    //percentage for trailing stop                                                              //trailing stop
        public double STRONG_TRAIL_PERCENT { get; set; } = 8;    //percentage for trailing stop
        public double SPIKE_TRAIL_PERCENT { get; set; } = 6;    //percentage for trailing stop in spike condition
        // public double MINIMUM_STOP_THRESHOLD { get; set; } = 2; //will not set a stop if it cannot be placed more than this value from the entry price

        #endregion
#endif

        #region Backtesting
        //although this is backtesting, it is here so comparisons can be made with the actual parameter names
        public Dictionary<string, List<dynamic>> backtestProperties = new Dictionary<string, List<dynamic>>();
        internal void SetBacktestPropertyDictionary()
        {
            backtestProperties.Clear();

            //set up a dictionary of properties and their range of values
            backtestProperties.Add("MOVE_TO_BREAK_EVEN_ASAP", new List<dynamic> { true });
            backtestProperties.Add("OFFSET_PERCENT", new List<dynamic> { 0.01, 0.02 }); //this will be dependant on TimeScale, smaller == lower values required
            backtestProperties.Add("BREAKEVEN_PERCENT", new List<dynamic> { 0.1, 0.2 }); //ditto
            backtestProperties.Add("INITIAL_STOP_PERCENT", new List<dynamic> { 0.25, 0.5 }); //ditto
            backtestProperties.Add("ELAPSED_BELOW_BREAKEVEN_BARS", new List<dynamic> { 8, 16 });
            backtestProperties.Add("ELAPSED_STOP_BARS", new List<dynamic> { 8, 12, 16 });
            backtestProperties.Add("STOP_INCREMENT", new List<dynamic> { 0.01, 0.02 });
        }

        #endregion
        
        #endregion

        #region Fields
        
        public List<_SymbolDataItem> results = new List<_SymbolDataItem>();
        List<_SymbolDataItem> framesetResults = new List<_SymbolDataItem>();
        public _SymbolDataItem currentDataItem;
        public int currentFrame;
        public DateTime firstTimestamp;
        public bool dataReady = false;
        public string currentTradeSetReference = null;
        public bool hasSpiked; //easier than saving as a persistant property
        protected bool connectionInitialised = false;
        #endregion

        #region Order object fields
        public Order_PriceBreak priceBreakOrderIndicator;
        public Order_PriceBreakMACD macdPriceBreakOrderIndicator;
        public Order_PriceBreakHMA hmaPriceBreakOrderIndicator;
        //public Order_Auto autoOrderIndicator;
        public HMA hma_100, ema_63, ema_27, ema_20, ema_10, ema_5; //unused but will throw compiler errors from other classes if not defined
        public const int emaOffset = 10;
        #endregion

        #region Context synchronisation
        //lock is just a keyword which implements Monitor.Enter,
        //and the locked object is just to lock entry to the method, it is not locked in itself
        //intellisense does not like locking the list
        public readonly object synchLock = new object(); //lock for all exchange-related processing
        public readonly object listUpdateLock = new object(); //lock for list update, so UI cannot access list whilst it is being changed (avoids exception)        

        //Trade fields
        public _Trade _order, _orderOrStub;
        private _Trade _entry, _entryOrStub, _stop, _ceiling, _floor, _breakEven, _takeProfit, _close;
        private List<_Trade> _activeTrades, _allTrades, _waitingOrders;
        private List<_ChartLine> _activeLines, _allLinesForSymbol;
        private bool dbUpdateRequired = false;
        public bool tradeStatusChanged = false;

        //saved data context
        public DataContext DataContext { get; set; }

        //UI drawing lists
        internal List<_Trade> UI_Trades = new List<_Trade>();
        internal List<_ChartLine> UI_Lines = new List<_ChartLine>(); //just the lines, no trades included, for drawing (disposed data context)

        #region Properties
        //gets all waiting orders for this symbol
        public List<_Trade> WaitingOrders
        {
            get
            {
                return _waitingOrders != null
                    ? _waitingOrders
                    : _waitingOrders = AllTrades.Where(x =>
                            x.Action == TradeAction.ORDER &&
                            x.GlobalStatus == TradeGlobalStatus.ACTIVE &&
                            x.ItemStatus == TradeItemStatus.OPEN &&
                            x.ExchangeMethodScript == null).ToList();
            }
            set
            {
                _waitingOrders = value;
            }
        }

        //get all trades associated with a running Order
        public List<_Trade> ActiveTrades
        {
            get
            {
                return _activeTrades != null
                    ? _activeTrades
                    : _activeTrades = Order != null
                        ? AllTrades.Where(x =>
                            x.TradeReference == Order.TradeReference
                            ).ToList()
                    : new List<_Trade>();
            }
            set
            {
                _activeTrades = value;
            }
        }

        //get all lines associated with a running Order
        public List<_ChartLine> ActiveLines
        {
            get
            {

                    return _activeLines != null
                        ? _activeLines
                        : _activeLines = Order != null
                            ? DataContext.Lines.Include(x => x.Trade).Where(x =>
                                x.TradeReference == Order.TradeReference
                                ).ToList()
                            : new List<_ChartLine>();

            }
            set
            {
                _activeLines = value;
            }
        }

        //get a list of all trades, for this symbol
        public List<_Trade> AllTrades
        {
            get
            {
                return _allTrades != null
                ? _allTrades
                : _allTrades = DataContext.Trades.Where(x =>
                                    x.SymbolName == symbolName
                                    ).ToList();
            }
            set
            {
                _allTrades = value;
            }
        }

        //get a list of all lines with trades included, for this symbol
        public List<_ChartLine> AllLinesForSymbol
        {
            get
            {
                    return _allLinesForSymbol != null
                    ? _allLinesForSymbol
                    : _allLinesForSymbol = DataContext.Lines.Include(x => x.Trade).Where(x =>
                                                x.SymbolName == symbolName
                                                ).ToList();

            }
            set
            {
                _allLinesForSymbol = value;
            }
        }
        
        //the running Order and other trades (for Order, item status will be Closed and global status Active)
        public _Trade Order
        {
            get
            {
                return _order != null
                    ? _order
                    : _order = AllTrades.FirstOrDefault(x =>
                                    x.Action == TradeAction.ORDER &&
                                    x.GlobalStatus == TradeGlobalStatus.ACTIVE &&
                                    x.ItemStatus == TradeItemStatus.CLOSED
                                    );
            }
            set
            {
                _order = value;
            }
        }
        //public _Trade OrderOrStub
        //{
        //    get
        //    {
        //        if (_orderOrStub != null)  return _orderOrStub;

        //        _orderOrStub = AllTrades.FirstOrDefault(x =>
        //                            x.Action == TradeAction.ORDER &&
        //                            x.GlobalStatus == TradeGlobalStatus.ACTIVE &&
        //                            x.ItemStatus == TradeItemStatus.CLOSED &&
        //                            x.StubCallbackTradeReference != null
        //                            );

        //        if (_orderOrStub != null) return _orderOrStub;

        //        _orderOrStub = AllTrades.FirstOrDefault(x =>
        //                           x.Action == TradeAction.ORDER &&
        //                           x.GlobalStatus == TradeGlobalStatus.ACTIVE &&
        //                           x.ItemStatus == TradeItemStatus.CLOSED
        //                           );

        //        return _orderOrStub;
        //    }
        //    set
        //    {
        //        _orderOrStub = value;
        //    }
        //}
        public _Trade Entry
        {
            get
            {
                return _entry != null
                    ? _entry
                    : _entry = ActiveTrades.FirstOrDefault(x =>
                                    x.Action == TradeAction.ENTRY &&
                                    x.ItemStatus == TradeItemStatus.OPEN);
            }
            set
            {
                _entry = value;
            }
        }

        //public _Trade EntryOrStub
        //{
        //    get
        //    {
        //        if (_entryOrStub != null) return _entryOrStub;

        //        _entryOrStub = ActiveTrades.FirstOrDefault(x =>
        //                            x.Action == TradeAction.ENTRY &&
        //                            x.ItemStatus == TradeItemStatus.OPEN &&
        //                            x.StubCallbackTradeReference != null);
        //        if (_entryOrStub != null) return _entryOrStub;

        //        _entryOrStub = ActiveTrades.FirstOrDefault(x =>
        //            x.Action == TradeAction.ENTRY &&
        //            x.ItemStatus == TradeItemStatus.OPEN);
        //        return _entryOrStub;
        //    }
        //    set
        //    {
        //        _entryOrStub = value;
        //    }
        //}

        public _Trade Stop
        {
            get
            {
                return _stop != null 
                    ? _stop 
                    : _stop = ActiveTrades.FirstOrDefault(x => 
                                    x.Action == TradeAction.STOP && 
                                    x.ItemStatus == TradeItemStatus.OPEN);
            }
            set
            {
                _stop = value;
            }
        }
        public _Trade Floor
        {
            get
            {
                return _floor != null 
                    ? _floor 
                    : _floor = ActiveTrades.FirstOrDefault(x => 
                                    x.Action == TradeAction.FLOOR && 
                                    x.ItemStatus == TradeItemStatus.OPEN);
            }
            set
            {
                _floor = value;
            }
        }
        public _Trade BreakEven
        {
            get
            {
                return _breakEven != null
                    ? _breakEven
                    : _breakEven = ActiveTrades.FirstOrDefault(x =>
                                    x.Action == TradeAction.BREAK_EVEN &&
                                    x.ItemStatus == TradeItemStatus.OPEN);
            }
            set
            {
                _floor = value;
            }
        }
        public _Trade Ceiling
        {
            get
            {
                return _ceiling != null 
                    ? _ceiling 
                    : _ceiling = ActiveTrades.FirstOrDefault(x => 
                                    x.Action == TradeAction.CEILING && 
                                    x.ItemStatus == TradeItemStatus.OPEN);
            }
            set
            {
                _ceiling = value;
            }
        }
        public _Trade TakeProfit
        {
            get
            {
                return _takeProfit != null
                    ? _takeProfit
                    : _takeProfit = ActiveTrades.FirstOrDefault(x =>
                                    x.Action == TradeAction.TAKE_PROFIT &&
                                    x.ItemStatus == TradeItemStatus.OPEN);
            }
            set
            {
                _takeProfit = value;
            }
        }
        public _Trade Close
        {
            get
            {
                return _close != null
                    ? _close
                    : _close = ActiveTrades.FirstOrDefault(x =>
                                    x.Action == TradeAction.CLOSE &&
                                    x.ItemStatus == TradeItemStatus.OPEN);
            }
            set
            {
                _close = value;
            }
        }
        #endregion

        #region Get Trades and Lines for UI event use

        internal List<_Trade> GetTradesForTradeReference(string reference = null)
        {
            var _reference = reference == null ? currentTradeSetReference : reference;
            return AllTrades.Where(x => x.TradeReference == _reference).ToList();
        }

        internal List<_ChartLine> GetLinesForTradeReference(string reference = null)
        {
            var _reference = reference == null ? currentTradeSetReference : reference;
            return AllLinesForSymbol.Where(x => x.TradeReference == _reference).ToList();
        }

        internal _Trade GetTradeForTradeReference(TradeAction action, string reference = null)
        {
            var _reference = reference == null ? currentTradeSetReference : reference;  
            return AllTrades.FirstOrDefault(x =>
                        x.Action == action &&
                        x.TradeReference == _reference);
        }

        internal bool IsActiveTrade(_Trade trade)
        {
            return (Order != null && Order.TradeReference == trade.TradeReference);
        }

        #endregion

        #region Get Trades and Lines for drawing

        void GetUIListData(DataContext dataContext)
        {
            if (dataContext != null)
            {
                UI_Lines = dataContext.Lines.AsNoTracking().Include(x => x.Trade).Where(x => x.SymbolName == symbolName).ToList();
                UI_Trades = dataContext.Trades.AsNoTracking().Where(x => x.SymbolName == symbolName).ToList();
            }
        }
        public void GetUILists(DataContext db, Action callback = null)
        {
            //get UI lists for drawing. These need now to be updated since there have been some changes somewhere
            lock (listUpdateLock) //locking here in case UI thread tries to read the data for drawing
            {
                if (callback != null)
                {
                    //this will be just to get a shallow copy of some of the trades from the existing lists, by just locking the update method here
                    callback();
                    return; 
                }
                else
                {
                    //refresh the lists - these lists are not tracked
                    GetUIListData(db);

                }
            }
        }
        #endregion

        public void InitForDataContext(Action callback)
        {
            lock (synchLock)
            {
                DataContext = new DataContext();
                //Debug.WriteLine($"InitForDataContext starting {Thread.CurrentThread.ManagedThreadId}");
                try
                {
                    if (callback != null)
                    {
                        callback();
                    }
                }
                finally
                {
                    if (dbUpdateRequired)
                    {
                        try
                        {
                            DataContext.SaveChanges();

                            /*
                             * NOTE - if a stop is deleted, then the stop line updated, an exception will be thrown and save changes will fail
                             *  but if the line is updated then stop deleted, save changes will work but the line will still have the trade Id of the missing stop
                             *  - so always make sure to remove trade from line -
                             * */
                            GetUIListData(DataContext);

                            if (tradeStatusChanged)
                            {
                                tradeStatusChanged = false;
                                MainObj.TradeStatusChanged(this, true);
                            }
                        }
                        catch
                        {
                        }
                    }

                    //must clear everything, or next time the lists will still be attached to the previous (nulled) DataContext so won't propagate
                    //have had some inexplainable behaviour, where ActiveLines is still a list at start of method, but of zero length,
                    //this is with BTCUSDT in a QTB trade, possibly because the properties were referenced in a different process?
                    //seems that you need to null out the underlying fields??
                    _activeLines = _allLinesForSymbol = null;
                    _activeTrades = _allTrades = _waitingOrders = null;
                    _entry = _entryOrStub = _stop = _ceiling = _floor = _takeProfit = _close = _order = _orderOrStub = _breakEven = null;
                    dbUpdateRequired = false;
                    DataContext.Dispose();
                    DataContext = null; //must not hold reference to using parameter
                }
            }


            // Debug.WriteLine($"InitForDataContext ending {Thread.CurrentThread.ManagedThreadId}");
        }

        #region Set update request
        //set the update request status. This is only called from within a valid DataContext loop (InitForDataContext)
        public void SetUpdateRequest(_Trade trade, TradeUpdateRequest updateRequest = TradeUpdateRequest.UPDATE)
        {
            try
            {
                //UpdateRequest.ADD is not used, since it is only the Id which determines if the object should be added to the DB, and it will not duplicate if added twice anyway
                if (trade.Id == 0) //should never be non-zero anyway
                {
                    DataContext.Trades.Add(trade); //cannot duplicate in database if added twice, it will only get added once
                }
                else if (updateRequest == TradeUpdateRequest.DELETE)
                {
                    if (CanDeleteTrade_synchronised(trade, Entry))
                    {
                        DataContext.Trades.Remove(trade);
                    }
                }
                else if (updateRequest == TradeUpdateRequest.FORCE_DELETE)
                {
                    DataContext.Trades.Remove(trade);
                }
                dbUpdateRequired = true;
            }
            catch { }

        }

        //ditto for lines.
        public void SetUpdateRequest(_ChartLine line, TradeUpdateRequest updateRequest = TradeUpdateRequest.UPDATE)
        {
            //UpdateRequest.ADD is not uses, since it is only the Id which determines if the object should be added to the DB, and it will not duplicate if added twice anyway
            if (line.Id == 0)//should never be non-zero anyway
            {
                DataContext.Lines.Add(line); //cannot duplicate in database if added twice, it will only get added once
            }
            else if (updateRequest == TradeUpdateRequest.DELETE)
            {
                DataContext.Lines.Remove(line);
            }

            dbUpdateRequired = true;
        }

        //ditto but returns the Id, needed to add to windows GUI objects to enable Trade to be found from line, etc.
        public int SetUpdateRequestAdd_UI(_ChartLine line)
        {
            if (line.Id == 0)
            {
                //the only way to get the Id is to add to the db and SaveChanges()
                //cannot manually add the Id as it is overwritten on SaveChanges with the next incremental value
                UI_Lines.Add(line);
                DataContext.Lines.Add(line); //this does set the Id, no need to re-assign object to return value
                DataContext.SaveChanges();
            }
            return line.Id;
        } 
        #endregion

        #endregion

        #region Init

        public TradeEngine(string symbolName, int timeInterval) : base(symbolName, timeInterval, null, null)
        {
            InitLiveUpdateTimer();
           // movingAverages.Add(hma_100 = new HMA(CandlePosition.CLOSE, Chart, 100));
        }
  
        public override void Init(int _timeInterval) //time interval does not change so ignore
        {

            base.Init(_timeInterval);

            tradeEngine = this; //in case the Results property is invoked by mistake, which will return Results

            #region Get active trade
            //get any entered trade. By checking for entry first, then open order, it is possible to run multiple orders on a single symbol,
            // to allow a trade to stop out then be re-entered higher on break.
            // however this adds complexity so has not been implemented yet

            //must still be reading this symbol name
            if (!SymbolNames.GetSymbolNames().Contains(symbolName))
            {
                return;
            }

            //get any pending order - ignore Constants.ALLOW_MULTIPLE_SYMBOL_TRADES, pick up whatever is entered
            try
            {
                using (DataContext dbInit = new DataContext())
                {
                    GetUILists(dbInit);
                }
            }
            catch
            {
            }

            if (USE_SIMULATED_EXCHANGE)
            {
                Binance_StartSimulation();
            }
            #endregion
        }

        public override void InitIndicators(int timeFrame)
        {
            base.InitIndicators(timeFrame);
        }

        public override void InitTimeFrame(int timeFrame)
        {
            base.InitTimeFrame(timeFrame);
        }

        #endregion

        #region Add Item

        public override void ItemAdded(_SymbolDataItem item, bool canComputeTradeEntry, bool liveItem)//, bool redrawOnly) //canComputeTradeEntry means outside of interruption periods
        {
            base.ItemAdded(item, canComputeTradeEntry, liveItem);
            try
            {
                results.Add(item);

                if (!liveItem)
                {
                    return;
                }

                //so as to not enter a trade during an interpolation period, i.e. after a connection failure, interpolation must be completed before testing for trade entry
                ItemUpdated(item, true);

                //must have at least one update for data to be ready, so any interpolation will have been performed by now and current data item will be up to date
                dataReady = true;

                //UpdateLiveState(); //conflicts somewhat with setting stop after stop movement

                using (DataContext db = new DataContext())
                {
                    GetUILists(db); //just to synch with db, in case something has been removed or not saved correctly
                }
            }
            catch
            {

            }
        }

        public override void ItemUpdated(_SymbolDataItem item, bool itemAdded = false)
        {

            //wait until confirmed connection before starting to send requests to exchange
            if (!USE_SIMULATED_EXCHANGE) //because it will already have been done
            {
                if (!connectionInitialised)
                {
                    connectionInitialised = true;
                    Binance_InitEvents();
                }
            }

            currentDataItem = item;

            if (!MainObj.IsBacktesting)
            {

                //synchronise the trades with any UI changes
                RunTrade_synchronised(results.Count - 1, itemAdded);

            }
        }

        #endregion

        #region Execute trade

        //public virtual void RunTrade_exchangeSpecific()
        //{
        //    return;
        //}

        //return param is only for backtesting
        public bool RunTrade_synchronised(int currentFrame, bool itemAdded)
        {
            //when called directly from ItemUpdated, this will run live trade
            //when called for backtesting, will exit the test session when this returns false

            bool redrawRequired = false;
            bool ret = false; //this is for returning from backtest loop, so default is to continue backtesting when exiting from tp, stop etc.
            this.currentFrame = currentFrame;

            double timeoutSeconds = USE_SIMULATED_EXCHANGE ? 1000 : TIMEOUT_SECONDS;

            InitForDataContext(() =>
            {
                #region Timeout for queued orders (unused)
                //check for timeout on any queued orders. They should execute quickly. If anything is wrong with the coding and a queued order gets orphaned,
                // it will block all further trades
                //Binance_CheckForPendingTradeRequestTimeout();
                #endregion

                var v = Order;
                if (Order != null) //need to include stubs in Orders to do the exchange trade, but don't execute them here!
                {
                    var posText = Order.Position == TradePosition.LONG ? "Long" : "Short";
                    var currentTradeIndicator = GetCurrentTradeIndicator(Order);

                    #region Timeout for close orders
                    //order may have closed or not, on the exchange
                    // - need to check if it has already closed, or deal with the rejected order if the balance is zero (has already been closed)
                    if (Close != null)
                    {
                        if (CheckForTimeout_Synchronised(Close))
                        {
                            LogTrades.WriteLine(string.Format("Close order for {0} interrupted at {1}", Close.SymbolName, DateTime.UtcNow));
                            Close.ItemStatus = TradeItemStatus.CLOSED; //or cannot delete
                            SetUpdateRequest(Close, TradeUpdateRequest.DELETE); //remove close so it can be attempted again - it will be marked as interrupted if the order is rejected
                            KillEntryOrCloseOrder(true);
                            MainObj.exchangeConnector.RestartConnection();
                            redrawRequired = true;
                            return;
                        }
                        //no further processing whilst close is pending
                        return;
                    }
                    #endregion

                    #region Entry
                    if (Entry != null)
                    {
                        if (Entry.ExchangeMethodScript == null)
                        {
                            currentTradeIndicator.ExecuteEntry_synchronised();
                            //entry should never terminate trade (in backtesting) so cannot return false
                            
                            //RunTrade_exchangeSpecific();
                        }
                        else
                        {
                            if (CheckForTimeout_Synchronised(Entry))
                            {
                                //an interrupted entry won't be closed, since there is no way to tell whether this has entered on the exchange (at time of writing)
                                //leave the script name in place. 

                                Entry.ExchangeMethodScript = TradeEngine.EXCHANGE_NOT_SYNCHED;
                                SetUpdateRequest(Entry);//, TradeUpdateRequest.UPDATE);
                                //won't kill the order, just mark it as not synched. It may still be running and will then get re-synched automatically anyway.

                                //KillEntryOrCloseOrder(true);

                               // MainObj.exchangeConnector.RestartConnection();
                                redrawRequired = true;
                                tradeStatusChanged = true;
                                return;
                            }
                        }
                    }
                    #endregion

                    #region Ceiling

                    if (Ceiling != null && Ceiling.ExchangeMethodScript == null)
                    {
                        //take profit can only execute on price, no line etc. so handle it here
                        var low = tradeEngine.GetLow(Ceiling);
                        var high = tradeEngine.GetHigh(Ceiling);
                        var p = Order.Position == TradePosition.LONG ? "Long" : "Short";
                        //if (Ceiling.Stop != TradeStop.CEILING_TRIGGERED)
                        //{
                        //this will execute first, so if the trigger percent is zero, the ceiling will execute immediately
                        if (Order.Position == TradePosition.LONG
                                                    ? high >= Ceiling.Price
                                                    : high <= Ceiling.Price)
                            {
                                //Ceiling.Stop = TradeStop.CEILING_TRIGGERED;
                                //SetUpdateRequest(Ceiling);//, TradeUpdateRequest.UPDATE);
                                LogTrades.WriteLineOrder($"Exiting {p} {Entry.SymbolName} on ceiling cross at {DateTime.UtcNow}");

                                //not using the trigger, just an immediate exit instead
                                CloseTradeLive_synchronised(false);
                                redrawRequired = true;
                                return;
                                //MainObj.PlaySound(MainObj.ALERT_SOUND);
                            }
                        //}
                        #region Unused (ceiling trigger)

                        //    if (Ceiling.Stop == TradeStop.CEILING_TRIGGERED) //not an else clause, so it will execute straight away
                        //    {
                        //        //ceiling has been hit, exit when price falls to an arbitrary level
                        //        var ceilingPrice = Order.Position == TradePosition.LONG
                        //            ? Ceiling.Price * (1 - TradeEngine.CEILING_TRIGGER_PERCENT / 100)
                        //            : Ceiling.Price * (1 + TradeEngine.CEILING_TRIGGER_PERCENT / 100);

                        //        if (Order.Position == TradePosition.LONG
                        //           ? low <= ceilingPrice
                        //           : high >= ceilingPrice)
                        //            {
                        //                LogTrades.WriteLine($"Exiting {Entry.SymbolName} on ceiling cross");
                        //                CloseTradeLive_synchronised();
                        //                redrawRequired = true;
                        //                return;
                        //            }
                        //    }
                        #endregion
                        
                    } 

                    #endregion

                    #region Take Profit

                    //just a simplified version of the stop logic, no need for a separate order object to do this
                    if (TakeProfit != null && TakeProfit.ExchangeMethodScript == null)
                    {
                        var line = ActiveLines.GetLineForTrade(TakeProfit);
                        if (line != null) //just in casse
                        {
                            (TakeProfit.Price, _) = line.GetPriceAtTime(currentDataItem.Timestamp, true);

                            var low = tradeEngine.GetLow(TakeProfit);
                            var high = tradeEngine.GetHigh(TakeProfit);

                            //check if tp price is crossed now price is set from line

                            //if (TakeProfit.Stop != TradeStop.CEILING_TRIGGERED)
                            //{
                            //this will execute first, so if the trigger percent is zero, the ceiling will execute immediately
                            if (Order.Position == TradePosition.LONG
                                                        ? high >= TakeProfit.Price
                                                        : high <= TakeProfit.Price)
                                {
                                    //TakeProfit.Stop = TradeStop.CEILING_TRIGGERED;
                                    //SetUpdateRequest(TakeProfit);//, TradeUpdateRequest.UPDATE);

                                    //not using the trigger, just an immediate exit instead
                                    LogTrades.WriteLineOrder($"Exiting {posText} {Entry.SymbolName} on take profit cross at {DateTime.UtcNow}");
                                    CloseTradeLive_synchronised(false);
                                    redrawRequired = true;
                                    return;
                                    //MainObj.PlaySound(MainObj.ALERT_SOUND);
                                }
                            //}
                            #region Unused (ceiling trigger)
                            //if (TakeProfit.Stop == TradeStop.CEILING_TRIGGERED) //not an else clause, so it will execute straight away
                            //{
                            //    //ceiling has been hit, exit when price falls to an arbitrary level
                            //    var takeProfitPrice = Order.Position == TradePosition.LONG
                            //        ? TakeProfit.Price * (1 - TradeEngine.CEILING_TRIGGER_PERCENT / 100)
                            //        : TakeProfit.Price * (1 + TradeEngine.CEILING_TRIGGER_PERCENT / 100);

                            //    if (Order.Position == TradePosition.LONG
                            //        ? low <= takeProfitPrice
                            //        : high >= takeProfitPrice)
                            //    {
                            //        LogTrades.WriteLine($"Exiting {Entry.SymbolName} on take profit cross");
                            //        CloseTradeLive_synchronised();
                            //        redrawRequired = true;
                            //        return;
                            //    }
                            //} 
                            #endregion

                            //if (Entry.Position == TradePosition.LONG
                            //               ? high >= TakeProfit.Price
                            //               : high <= TakeProfit.Price)
                            //{
                            //    LogTrades.WriteLine($"Exiting {posText} {Entry.SymbolName} on take profit cross");
                            //    CloseTradeLive_synchronised();
                            //    redrawRequired = true;
                            //    return;
                            //}
                        }
                    }


                    #endregion

                    #region Floor

                    if (Floor != null && Entry != null) //will happen if adding a stop before trade is entered
                    {
                        //indicator can trail floor, etc.
                        (bool floorExecuted, bool updateFloor) = currentTradeIndicator.ExecuteFloor_synchronised(itemAdded);
                        if (updateFloor)
                        {
                            SetUpdateRequest(Floor);//, TradeUpdateRequest.UPDATE);
                        }
                        if (floorExecuted)
                        {
                            LogTrades.WriteLineOrder($"Exiting {posText} {Entry.SymbolName} on floor cross at {DateTime.UtcNow}");
                            CloseTradeLive_synchronised(false);
                            redrawRequired = true;
                            return;
                        }
                    }

                    #endregion

                    #region Stop(s)

                    //stop can be a dead stop (TradeAction == STOP, TradeType == NO_FLIP_ORDER), a flip stop (TradeAction == WAITING_STOP, TradeType == FLIP_ORDER) or a dead stop for a flip stop (TradeAction == WAITING_STOP, TradeType == STOP_WAITING)
                    //but only the single Stop object gets its price updated, so get any flip stops and update those
                    if (Stop != null)
                    {
                        if (Stop.ExchangeMethodScript == null)
                        {
                            //indicator may terminate trade on other stop conditions, e.g. ema crossover
                            (bool stopExecuted, bool updateStop) = currentTradeIndicator.ExecuteStop_synchronised(itemAdded);

 
                            if (stopExecuted)
                            {
                                LogTrades.WriteLineOrder($"Exiting {posText} {Entry.SymbolName} on stop cross at {DateTime.UtcNow}");
                                CloseTradeLive_synchronised(true);
                                redrawRequired = true;
                                return;//terminate in backtesting, will have already closed trade
                            }
                           
                            if (itemAdded) //don't check for updated value, this might not update in this loop
                            {
                                SetStopOnExchange_Synchronised(Stop);
                            }
                        }

                        //move stop to current time, if earlier than this
                        var line = ActiveLines.GetLineForTrade(Stop);
                        if (line != null)
                        {
                            if (line.EndTimeStamp < DateTime.UtcNow)
                            {
                                (var price, _) = line.GetPriceAtTime(DateTime.UtcNow, true);
                                line.EndTimeStamp = DateTime.UtcNow;
                                line.EndPrice = price;
                                SetUpdateRequest(line);//, TradeUpdateRequest.UPDATE);
                            }
                        }
                    }

                    //update any flip stop price to the attached line price here
                    var flipStops = AllTrades.Where(x => x.TradeReference == Order.TradeReference && x.Action == TradeAction.WAITING_STOP);
                    foreach (var flipStop in flipStops)
                    {
                        if (flipStop != null)
                        {
                            var line = tradeEngine.ActiveLines.GetLineForTrade(flipStop);
                            if (line != null)
                            {
                                (flipStop.Price, _) = line.GetPriceAtTime(currentDataItem.Timestamp, true);
                                SetUpdateRequest(flipStop);//, TradeUpdateRequest.UPDATE);
                            }
                        }
                    }

                    #endregion

                    #region Break Even (unused)
                    //if (BreakEven != null)
                    //{
                    //    //just a simplified version of the stop logic, no need for a separate order object to do this

                    //    var low = tradeEngine.GetLow(BreakEven);
                    //    var high = tradeEngine.GetHigh(BreakEven);
                    //    //check if be price is crossed now price is set from line
                    //    if (Entry.Position == TradePosition.LONG
                    //                   ? high >= BreakEven.Price
                    //                   : high <= BreakEven.Price)
                    //    {
                    //        //if no floor, add one at b/e
                    //        //var breakEvenPrice = AlgoTrader.Shared.Order.GetBreakEvenPrice(Entry, this);
                    //        var mult = BREAK_EVEN_PERCENT / 100;
                    //        var breakEvenPrice = (currentDataItem.Close - Entry.Price) * mult + Entry.Price;
                    //        if (Floor == null)
                    //        {
                    //            Floor = Order.Clone();
                    //            Floor.Action = TradeAction.FLOOR;
                    //            Floor.Timestamp = currentDataItem.Timestamp;
                    //            Floor.Price = breakEvenPrice;
                    //            Floor.ActionValue = 0;
                    //            Floor.ItemId = null; //need a new ID for this as it is a new order.
                    //            Floor.ExchangeStatus = TradeExchangeStatus.ENTERING_ON_EXCHANGE;
                    //            SetStopOnExchange_Synchronised(Floor);
                    //        }
                    //        else
                    //        {
                    //            //if floor set below b/e, move it - but don't move if above!!
                    //            if ((Entry.Position == TradePosition.LONG && Floor.Price < breakEvenPrice) ||
                    //                 Entry.Position == TradePosition.SHORT && Floor.Price > breakEvenPrice)
                    //            {
                    //                Floor.Price = breakEvenPrice;
                    //            }
                    //        }
                    //        BreakEven.ItemStatus = TradeItemStatus.CLOSED;
                    //        tradeEngine.SetUpdateRequest(BreakEven);//, TradeUpdateRequest.UPDATE);
                    //        tradeEngine.SetUpdateRequest(Floor);//, TradeUpdateRequest.UPDATE);              
                    //        redrawRequired = true;
                    //    }
                    //}
                    #endregion

                    #region Timeout and Update Time for stops
                    foreach (var trade in AllTrades.Where(x => x.GlobalStatus == TradeGlobalStatus.ACTIVE)) //will move waiting trades, leave stopped ones
                    {
                        switch (trade.Action)
                        {
                            case TradeAction.FLOOR:
                            case TradeAction.STOP:
                            case TradeAction.CEILING:
                            case TradeAction.TAKE_PROFIT:
                            case TradeAction.BREAK_EVEN:

                                //to reduce db load, only save stop, tp, floor timestamps on the minute
                                if (itemAdded)
                                {
                                    trade.Timestamp = currentDataItem.Timestamp;
                                    SetUpdateRequest(trade);//, TradeUpdateRequest.UPDATE);
                                }
                                if (trade.ExchangeMethodScript != null)
                                {
                                    if (CheckForTimeout_Synchronised(trade))
                                    {
                                        redrawRequired = true;
                                        trade.ExchangeMethodScript = null;
                                        MainObj.exchangeConnector.RestartConnection();
                                    }
                                }
                                break;
                        }
                    }
                    #endregion
                }

                #region Test for trade entry
                else
                {
                    foreach (var order in WaitingOrders)
                    {
                        var currentTradeIndicator = GetCurrentTradeIndicator(order);

                        //when a trade is changed, added or deleted, mark it with a TradeUpdateRequest and add to the trades list
                        (bool hasEntered, bool tradeKilled) = EnterIfPossible_synchronised(order, currentTradeIndicator);
                        redrawRequired = hasEntered || tradeKilled;
                        if (tradeKilled)
                        {
                            //order has failed to enter, so don't want to keep retrying especially with market orders as this will just show endless dialogs
                            order.ItemStatus = TradeItemStatus.CLOSED;
                            order.GlobalStatus = TradeGlobalStatus.STOPPED;
                            order.ExchangeMethodScript = null;
                            order.ExchangeMethodIndex = 0;
                            SetUpdateRequest(order);//, TradeUpdateRequest.UPDATE);
                            redrawRequired = true;
                        }
                        if (hasEntered)
                        {
                            Order = order;
                            break;
                        }
                    }
                }
                

                #endregion
                
                ret = MainObj.IsBacktesting ? !WaitingOrders.Any() && Order == null : true;            
            }); 

            //cannot redraw until db write is complete
            if (redrawRequired)
            {
                MainObj.DrawCurrentDispatcher();
            }

            //for backtesting, quit when no more trades to test and nothing running
            //live does not need to return anything
            return ret;

        }

        #endregion

        #region Enter trade

        /*
         * Test for trade entry based on trade type, line break etc.
         * But also take into account the number of concurrent trades and number of recent trades.
         * The former needs to be done on a per-quote asset basis
         * This has nothing to do with base-to-quote asset trading, only for market or break orders
         * */
        protected (bool hasEntered, bool tradeKilled) EnterIfPossible_synchronised(_Trade trade, Order currentTradeIndicator) 
        {
            bool canEnter = 
                trade.Type == TradeType.MARKET_ORDER ||
                currentTradeIndicator == null ||
                currentTradeIndicator.CanEnterTrade_synchronised(trade);

            if (canEnter)
            {
                //determine if max number of concurrent or recent trades has been exceded
                OrderFrequency reason = OrderFrequency.OK_TO_ENTER_LIVE;

                //limit max. number of trades per 24 hour period
                var earliest = DateTime.UtcNow.AddDays(-1);
                var totTrades = DataContext.Trades.Where(x =>
                    x.ExchangeStatus == TradeExchangeStatus.ENTERED_ON_EXCHANGE &&
                    x.Action == TradeAction.ENTRY &&
                    DateTime.Compare(x.Timestamp, earliest) > 0).Count();

                if (totTrades >= TradeEngine.MAX_TRADES_PER_24_HOURS && MainObj.BACKTEST == false)
                {
                    reason = OrderFrequency.MAX_TRADES_PER_24_HOURS_EXCEDED;
                }
                else
                {
                    //need to calculate the maximum number of trades per quote asset
                    //no, this is more complicated with ETFs so have moved this to the Exchange Trade code block
                    //var quoteAsset = SymbolNamesBinance.GetQuoteAssetForSymbol(symbolName);
                    //var tradeCount = MainObj.NumLiveEntriesForQuoteAsset(DataContext, quoteAsset);
                    //if (tradeCount >= TradeEngine.MAX_CONCURRENT_TRADES)
                    //{
                    //    reason = OrderFrequency.MAX_CONCURRENT_TRADES_EXCEDED;
                    //}
                }


                if (MainObj.IsBacktesting)
                {
                    #region Backtesting
                    //this is just a bit easier to read as it matches the on exchange code below
                    //NOTE - the backtest system works on one symbol at a time, so will NEVER show a trade as entered off exchange (i.e. no concurrency)
                    if (OVERRIDE_MAX_TRADES_LIMIT_BACKTESTING || trade.ExchangeStatus != TradeExchangeStatus.ENTERED_OFF_EXCHANGE) //either exceded max. 24 hour trades or max. concurrent trades
                    {
                        Order = trade;
                        Order.ItemStatus = TradeItemStatus.CLOSED;
                        SetUpdateRequest(Order);//, TradeUpdateRequest.UPDATE);
                        EnterTradeOnSystem_synchronised((decimal)currentDataItem.Close, 0, trade.ExchangeStatus);
                        return (true, false);
                    }
                    #endregion
                }

                else
                {
                    switch (reason)
                    {
                        case OrderFrequency.MAX_TRADES_PER_24_HOURS_EXCEDED:
                            LogTrades.WriteLineOrder($" {trade.SymbolName} at {DateTime.Now} - Trade not entered as max. number of entries in 24 hours ({TradeEngine.MAX_TRADES_PER_24_HOURS}) exceded");
                            AlertDialog.Show($"Could not place order for {trade.SymbolName} as there have already been {TradeEngine.MAX_TRADES_PER_24_HOURS} in 24 hours");
                            return (false, true);
                        //case OrderFrequency.MAX_CONCURRENT_TRADES_EXCEDED:
                        //    LogTrades.WriteLine($" {trade.SymbolName} at {DateTime.Now} - Trade not entered as max. number of concurrent entries ({TradeEngine.MAX_CONCURRENT_TRADES}) exceded");
                        //    AlertDialog.Show($"Could not place order for {trade.SymbolName} as there are already a maximum number of concurrent entries");
                        //    return (false, true);
                        case OrderFrequency.OK_TO_ENTER_LIVE:
                            LogTrades.WriteLine($"\n**************************************************************************************");
                            EnterTradeLive_synchronised(trade, (decimal)currentDataItem.Close);
                            return (true, false);
                    }               
                }
            }

            return (false, false);
        }

        internal void EnterTradeLive_synchronised(_Trade order, decimal price)
        {
            //issue with price of 0, this should not happen here, or matter if trade is live, but this is just to check
            if (price == 0)
            {
                LogTrades.WriteLine(string.Format("{0} not entered at {1} as price was zero.", order.SymbolName, DateTime.UtcNow));
                return;
            }

            //Bitmex can get live price straight away via REST
            if (TradeEngine.EXCHANGE == Shared.Enums.ExchangeId.BINANCE)
            {
                if (!MainObj.IsBacktesting && !dataReady)
                {
                    LogTrades.WriteLine(string.Format("{0} not entered at {1} as candle data still pending.", order.SymbolName, DateTime.UtcNow));
                    return;
                }
            }

            //enter trade on system here, but not active yet
            if (Entry != null)
            {
                //pending or live entry, so return
                LogTrades.WriteLine("Attempt to enter trade whilst trade pending or live, exiting");
                return;
            }
            Order = order;

            Order.ExchangeMethodScript = NEW_ORDER_SCRIPT;

            Order.ItemStatus = TradeItemStatus.CLOSED;
            SetUpdateRequest(order);//, TradeUpdateRequest.UPDATE);

            Entry = Order.Clone(); //item status will be open
            Entry.ItemId = Guid.NewGuid().ToString(); //set the client-assigned Id (could wait for the binance-assigned one instead) but this way, can query status even if no confirmation yet received
            Entry.Action = TradeAction.ENTRY;
            Entry.Price = (double)price;
            Entry.ExchangeStatus = TradeExchangeStatus.ENTERED_OFF_EXCHANGE; //only set live when it actually returns from exchange
            Entry.Timestamp = GetTimeStamp();
            Entry.Id = 0; //so it will get added

            SetUpdateRequest(Entry);//, TradeUpdateRequest.UPDATE);

            StartEntryOrClose_Synchronised();
            //EnqueueOrder_synchronised(TradeAction.ENTRY);
        }

        protected virtual void EnterTradeOnSystem_ExchangeSpecific_Synchronised()
        {

        }

        protected void EnterTradeOnSystem_synchronised(decimal price, double quantity, TradeExchangeStatus orderType)
        {
            //note, Quantity is double not decimal, as decimal has 18 digits and 2 decimal places only in db
            try
            {
                hasSpiked = false;

                var currentTradeIndicator = GetCurrentTradeIndicator(Order);

                //get the EF tracked trade - if this is a live entry, will be using a different data context to the one the parameter is running on

                Order.ExchangeStatus = orderType;
                if (MAX_CONCURRENT_TRADES > 0) //obviously it cannot be zero but doesn't hurt to check
                {
                    Order.ActionValue = 1.0 / (double)MAX_CONCURRENT_TRADES; //set this as a record of the equity fraction traded, so this will be copied over to Entry and Close
                }
                SetUpdateRequest(Order);//, TradeUpdateRequest.UPDATE);

                //set trade time, frame and price to entry point (line entry will be different from order position)
                //enter trade on system here, but not active yet
                if (Entry == null)
                {
                    //if not trading live, will bypass exchange (if not using simulator) and come straight here, so no entry yet
                    Entry = Order.Clone();
                    Entry.Action = TradeAction.ENTRY;
                    Entry.Timestamp = GetTimeStamp();
                    SetUpdateRequest(Entry);//, TradeUpdateRequest.UPDATE);
                }

                LogTrades.WriteLine(string.Format("{0} {1} entered at {2}.\nTrade complete.", Order.SymbolName, Order.ExchangeStatus == TradeExchangeStatus.ENTERED_ON_EXCHANGE ? "LIVE" : "TEST", DateTime.UtcNow));
                LogTrades.WriteLine($"**************************************************************************************\n");
                Entry.Price = Order.Price = (double)price;
                Entry.Quantity = Order.Quantity = quantity; //set both the entry and order quantity, as either may be used when closing, etc.
                Entry.ExchangeStatus = orderType;
                SetUpdateRequest(Entry);//, TradeUpdateRequest.UPDATE);

                Order.ItemStatus = TradeItemStatus.CLOSED; //close the order, this is now an active trade
                SetUpdateRequest(Order);//, TradeUpdateRequest.UPDATE);

                EnterTradeOnSystem_ExchangeSpecific_Synchronised();

                //note, will set floor, ceiling, stop and tp quantities here, they are not needed but this is consistent with stops etc. which
                // are added after the trade goes live, as the quantity will then be copied over

                #region Initial take profit

                if (INITIAL_TAKE_PROFIT_PERCENT_MARKET > 0)
                {
                    bool canResetTP = true;
                    if (Ceiling != null)
                    {
                        //check it is within a minimum threshold, if not then reset
                        //untested
                        var minTp = 1 + INITIAL_TAKE_PROFIT_PERCENT_MARKET / 100;
                        canResetTP = Ceiling.Position == TradePosition.LONG ? Ceiling.Price < (double)price * minTp : Ceiling.Price > (double)price / minTp;
                    }
                    else
                    {
                        Ceiling = Order.Clone();
                    }
                    if (canResetTP)
                    {

                        //for debugging, will not have a price from the simulated exchange so will use the original order price
                        var entryPrice = Entry.Price != 0 ? Entry.Price : Order.Price;
                        //take profit trade is just a permanent trigger so it does not use Price, only ActionValue 
                        var tpPrice = Ceiling.Position == TradePosition.LONG
                            ? entryPrice * (1 + INITIAL_TAKE_PROFIT_PERCENT_MARKET / 100)
                            : entryPrice / (1 + INITIAL_TAKE_PROFIT_PERCENT_MARKET / 100);

                        Ceiling.Action = TradeAction.CEILING;
                        Ceiling.Timestamp = GetTimeStamp();
                        Ceiling.ActionValue = Ceiling.Price = tpPrice;

                        SetUpdateRequest(Ceiling);//, TradeUpdateRequest.UPDATE);
                    }
                }

                if (Ceiling != null)
                {
                    Ceiling.Quantity = Entry.Quantity;
                    SetUpdateRequest(Ceiling);//, TradeUpdateRequest.UPDATE);
                }
                #endregion

                // This will ensure stop or floor is not too close.
                void SetInitialStopFloor(_Trade _floorStop)
                {
                    _floorStop.Quantity = Entry.Quantity;
                    SetUpdateRequest(_floorStop);//, TradeUpdateRequest.UPDATE);

                    //make sure it is not too close to price
                    if (FLOOR_STOP_MIN_VALUE > 0)
                    {
                        //ensure floor is not too close to entry price
                        var high = GetHigh(Entry);

                        var line = ActiveLines.FirstOrDefault(x => x.Trade == _floorStop);
                        if (line != null)
                        {
                            (double stopPrice, _) = line.GetPriceAtTime(_floorStop.Timestamp, true);

                            var v = MainObj.GetPriceDiff(high, stopPrice, Entry.Position) * 100;
                            if (v < FLOOR_STOP_MIN_VALUE)
                            {
                                var startPriceDiff = _floorStop.Price - line.StartPrice;
                                var endPriceDiff = line.EndPrice - _floorStop.Price;

                                var s = _floorStop.Action == TradeAction.FLOOR ? "Floor" : "Stop";
                                LogTrades.WriteLine($"{s} too close to order price. {s} price was {_floorStop.Price}, current price is {high}");
                                _floorStop.Price = Entry.Price * (1 - FLOOR_STOP_MIN_VALUE / 100);
                                _floorStop.Timestamp = GetTimeStamp();
                                SetUpdateRequest(_floorStop);//, TradeUpdateRequest.UPDATE);

                                line.StartPrice -= startPriceDiff;
                                line.EndPrice -= endPriceDiff;
                                SetUpdateRequest(line);//, TradeUpdateRequest.UPDATE);
                                LogTrades.WriteLine($"Changing {s} price to {_floorStop.Price}");

                            }
                        }
                    }
                }
                
                #region Initial floor
                //note, if action value is zero, this will not trail

                if (Floor == null && Stop == null)
                {
                    //add trailing floor
                    if (FLOOR_TRAIL > 0)
                    {
                        Floor = Order.Clone();
                        Floor.Price = Entry.Price * (1 - FLOOR_TRAIL / 100);
                        Floor.ActionValue = FLOOR_TRAIL;
                        Floor.Timestamp = GetTimeStamp();
                        Floor.Action = TradeAction.FLOOR;
                        SetUpdateRequest(Floor);//, TradeUpdateRequest.UPDATE);
                    }
                }
                if (Floor != null)
                {
                    SetInitialStopFloor(Floor);
                }

                #endregion

                #region Set stop price to minimum value

                if (Stop != null)
                {
                    SetInitialStopFloor(Stop);
                }

                if (TakeProfit != null)
                {
                    TakeProfit.Quantity = Entry.Quantity;
                    SetUpdateRequest(TakeProfit);//, TradeUpdateRequest.UPDATE);
                }
                
                #endregion

                if (Entry.Price == 0)
                {
                    LogTrades.WriteLine($"New entry price was 0 for {Order.SymbolName} in EnterTradeOnSystem()");
                }
            }
            catch (Exception e)
            {
                LogTrades.WriteLine($"Exception in EnterTradeOnSystem  at {DateTime.UtcNow} (1): {e}");
            }

            if (!MainObj.IsBacktesting)
            {
                //MainObj.PlaySound(MainObj.ALERT_SOUND);
            }
        }
        #endregion

        #region Enter stop / take profit live (unimplemented)

        //these methods should be called when trade executes, to set OCO on exchange, etc.

        //enter stop live, called from stop execution
        //internal void EnterStopTakeProfitLive(Trade trade)
        //{
        //    if (Entry == null)
        //    {
        //        LogTrades.WriteLine($"Could not set stop as quantity not found for {trade.SymbolName}");
        //        return;
        //    }            
        //    if (USE_SIMULATED_EXCHANGE)
        //    {
        //        StartSimulation();
        //    }
        //    if (!MainObj.ALLOW_LIVE_TRADES || SET_STOPS_ON_EXCHANGE == false)
        //    {
        //        return;
        //    }

        //    //close any existing stop or tp, wait for confirmation, then set price and status on new stop
        //    //if stop is new, would need ExchangeRequest.NEW_ORDER_STOP / TAKEPROFIT
        //    //but would really need to check server status first, unless this was a new order placement
        //    trade.ItemId = Guid.NewGuid().ToString();
        //    trade.ExchangeStatus = TradeExchangeStatus.ENTERED_ON_EXCHANGE;

        //    trade.Timestamp = GetTimeStamp();
        //    SetUpdateRequest(trade);//, TradeUpdateRequest.UPDATE);

        //    switch (trade.Action)
        //    {
        //        case TradeAction.STOP:
        //            trade.ExchangeMethodScript = NEW_ORDER_ON_CLOSE_STOP_SCRIPT;
        //            lastStopTime = DateTime.UtcNow;
        //            pendingLiveEntryStop = trade;
        //            break;
        //        case TradeAction.TAKE_PROFIT:
        //            trade.ExchangeMethodScript = NEW_ORDER_ON_CLOSE_TAKEPROFIT_SCRIPT;
        //            lastTakeProfitTime = DateTime.UtcNow;
        //            pendingLiveEntryTP = trade;
        //            break;
        //    }

            //todo - at this point, it is necessary to close any stop or tp on the exchange first, then request a new one

            //LogTrades.WriteLine($"Starting request for NEW_ORDER_ON_CLOSE__STOP");
            //stop already on system, so need to close this on exchange first
            //StartExchangeRequest(
            //    trade.ExchangeRequestIntent,
            //    (ApiParamNames.SYMBOL, trade.SymbolName),
            //    (ApiParamNames.TRADE_ID_ORIGINAL, trade.ItemId));

            //todo - then:

            //switch (trade.Action)
            //{
            //    case TradeAction.STOP:
            //        RequestStopTakeProfitAddition(trade, ApiParamNames.STOP_LOSS_LIMIT);
            //        break;
            //    case TradeAction.TAKE_PROFIT:
            //        RequestStopTakeProfitAddition(trade, ApiParamNames.TAKE_PROFIT_LIMIT);
            //        break;
            //}

        //}
        #endregion

        #region Close trade

        internal void CloseTradeOnSystem_synchronised(decimal price)
        {
            //close a trade, this is a close request with no further intent
            //trade could be an order for a manual close, or a close if system is synching with exchange
            if (Order == null)
            {
                //LogTrades.WriteLine(string.Format(" ++ WARNING ++: Could not close trade for {0} on system as active order was null", symbolName));
                return;
            }

            //if the trade is open, add a closed trade
            //but it could be a running market order, so must still 'close' it      

            //try to get an existing close from the system with this tradeId - if there is one, just update it
            //the _trade parameter may be the order, a stop, or an actual close depending on how this is called
            //- if it is a Close, this would be when synching with exchange. 
            //Otherwise, this will be a manual close or a test trade (closed on stop or tp) and will need to add close trade
            if (Close == null)
            {
                Close = Order.Clone();
                Close.Action = TradeAction.CLOSE;
                Close.Timestamp = GetTimeStamp();
                SetUpdateRequest(Close);//, TradeUpdateRequest.UPDATE);
            }

            Close.Price = (double)price;
            Close.ItemStatus = TradeItemStatus.CLOSED;
            Close.GlobalStatus = TradeGlobalStatus.STOPPED;

            if (Entry != null)
            {
                if (Entry.Price != 0) //if so Result will be infinity and db.SaveChanges() will throw exception
                {
                    //so there is at least an entry trade here, so can close the trade and set the close price
                    //if aborted, this is just a backtest which has hit an interruption, or a line trade which has not entered
                    double price_pc = -MainObj.GetPriceDiff(Entry.Price, Close.Price, Close.Position) * 100;

                    Close.Result = Entry.Result = Order.Result = price_pc; //set result on order for correct color display

                    SetUpdateRequest(Close);//, TradeUpdateRequest.UPDATE);

                    LogTrades.WriteLineOrder(string.Format("{0} {1}% closed at {2}", Close.SymbolName, Close.Result, DateTime.UtcNow));

                }
            }

            //change type to STOPPED for all the trades
            foreach (_Trade t in ActiveTrades)
            {
                t.GlobalStatus = TradeGlobalStatus.STOPPED;
                t.ItemStatus = TradeItemStatus.CLOSED;
                SetUpdateRequest(t);//, TradeUpdateRequest.UPDATE); //update everything (except close if that has just been created, it won't be in the list)

                //remove stops and reset lines
        //just put this back if needed, it was removed to show all trades on close for debugging purposes
        //        if (t.Action != TradeAction.ENTRY && t.Action != TradeAction.CLOSE && t.Action != TradeAction.ORDER)
        //        {
        //            RemoveTradeFromLine_UI_synchronised(t, false, true); //or will throw exception on saving
        //            SetUpdateRequest(t, TradeUpdateRequest.DELETE);  
        //        }
            }

            var line = ActiveLines.GetLineForTrade(Order);
            if (line != null)
            {
                line.TruncateLineToTime();
                line.Type = Entry != null ? LineType.CLOSED_ENTERED : LineType.CLOSED_UNENTERED;
                //detach from any line, so line can then be reused if it is desirable to leave the trade in place
                RemoveTradeFromLine_UI_synchronised(Order);
            }

            Order = null;
            Leverage = 1;
            MainObj.SetLeverageMenuState((int)Leverage); //NOTE, calling Application.Current.Dispatcher.Invoke() within this thread causes a lock condition
        }

        //entry point for trade closure from local system, not called when exchange closes trade
        internal void CloseTradeLive_synchronised(bool allowFlip = false)
        {
            if (Entry != null)
            {
                var price = (decimal)(currentDataItem != null ? currentDataItem.Close : Entry.Price);

                if (!MainObj.IsBacktesting && Order.ExchangeStatus == TradeExchangeStatus.ENTERED_ON_EXCHANGE)
                {
                    //close on the exchange

                    if (Close != null) //pending or live close, so return
                    {
                        LogTrades.WriteLine("Attempt to close trade whilst trade pending or live, exiting");
                        return;
                    }

                    var s = USE_SIMULATED_EXCHANGE ? "SIMULATED EXCHANGE" : "LIVE EXCHANGE";
                    LogTrades.WriteLine($"**************************************************************************************");
                    LogTrades.WriteLine($"Closing {Entry.SymbolName} {Entry.TradeReference} at {DateTime.UtcNow}");

                    Order.ExchangeMethodScript = CLOSE_ORDER_SCRIPT;
                    Entry.ExchangeMethodScript = CLOSE_ORDER_SCRIPT;

                    if (Stop != null && Stop.ItemId != null)
                    {
                        Entry.ExchangeMethodIndex = 0; //bypass the stop removal on Binance
                    }

                    SetUpdateRequest(Order);//, TradeUpdateRequest.UPDATE);
                    SetUpdateRequest(Entry);//, TradeUpdateRequest.UPDATE);

                    //enter close on system here, it is required for closing order regardless of flip

                    Close = Order.Clone();
                    Close.ItemId = Guid.NewGuid().ToString(); //set the client-assigned Id (could wait for the binance-assigned one instead) but this way, can query status even if no confirmation yet received
                    Close.Action = TradeAction.CLOSE;
                    Close.Timestamp = GetTimeStamp();
                    Close.Price = currentDataItem != null ? currentDataItem.Close : 0; //should never be zero, but won't take entry price in case entry has been lost for some reason
                    Close.Result = -MainObj.GetPriceDiff(Entry.Price, currentDataItem.Close, Order.Position) * 100; //setting result here in case close times out but does actually close on exchange, in which case there is still a record here of the result
                    SetUpdateRequest(Close);//, TradeUpdateRequest.UPDATE); //update adds if there is no Id set

                    StartEntryOrClose_Synchronised(); //will remove stop and floor

                    //at this point, order will be null and all exchange method scripts will be null. Close will have been added
                    if (allowFlip)
                    {
                        if (Stop != null)
                        {
                            if (Stop.Type == TradeType.FLIP_ORDER)
                            {
                                DoFlip_Synchronised();
                            }
                        }
                    }

                    MainObj.DrawCurrentDispatcher();
                    return;
                }

                LogTrades.WriteLine("Closing order either backtesting or ENTERED_OFF_EXCHANGE");
                CloseTradeOnSystem_synchronised(price); //close the trade on this system

            }
        }

        protected virtual void DoFlip_Synchronised() { }

        private bool CanDeleteTrade_synchronised(_Trade trade, _Trade entry)
        {
            //entry is required for non-stop delete, so it is passed in as it may be the Entry property (live trade) or derived from UI interraction
            bool canDelete = true;
            switch (trade.Action)
            {
                case TradeAction.STOP:
                case TradeAction.CEILING:
                case TradeAction.FLOOR:
                    //can delete if not entered (stop not on exchange) or if closed
                    canDelete = true;
                    //canDelete = trade.ItemId == null || 
                    //            trade.ItemStatus == TradeItemStatus.CLOSED || 
                    //            trade.GlobalStatus == TradeGlobalStatus.STOPPED ||
                    //            //trade.ExchangeMethodInterrupted == true || 
                    //            (trade.ExchangeMethodScript == null && trade.ExchangeStatus == TradeExchangeStatus.ENTERED_OFF_EXCHANGE);

                    break;

                case TradeAction.ORDER:
                case TradeAction.ENTRY:
                case TradeAction.CLOSE:
                    //can only delete if trade not live
                    canDelete = (trade.ItemStatus == TradeItemStatus.OPEN || trade.GlobalStatus == TradeGlobalStatus.STOPPED) &&
                                (entry == null || entry.GlobalStatus == TradeGlobalStatus.STOPPED);


                    
                    break;
            }
            return canDelete;
        }
        #endregion

        #region Utility methods



        private bool CheckForTimeout_Synchronised(_Trade trade)
        {
            double timeoutSeconds = USE_SIMULATED_EXCHANGE ? 1000 : TIMEOUT_SECONDS;

            bool ret = DateTime.UtcNow.Subtract(trade.Timestamp).TotalSeconds > timeoutSeconds;
            if (ret)
            {
                //LogTrades.WriteLine($"Timeout for {trade.SymbolName} {trade.Action}, at {DateTime.UtcNow:G}"); //just fills the log

                //if this is part of a QTB trade, the Entry will have the trade reference of the stub trades. Remove these.
                //Note, the stub trades can also timeout, but only the base trade will be executing.
                if (Entry.StubCallbackTradeReference != null)
                {
                    var stubTrades = DataContext.Trades.Where(x => x.TradeReference == Entry.StubCallbackTradeReference);
                    foreach (var _trade in stubTrades)
                    {
                        SetUpdateRequest(_trade, TradeUpdateRequest.FORCE_DELETE);
                    }
                }

                //DequeueOrder_synchronised();

                
            }
            if (ret)
            {
                trade.Timestamp = DateTime.UtcNow; //or it will restart every loop until the issue is resolved
                SetUpdateRequest(trade);//, TradeUpdateRequest.UPDATE);
            }
            return ret;
        }

        internal DateTime GetTimeStamp()
        {
            if (MainObj.IsBacktesting)
            {
                return currentDataItem.Timestamp;
            }
            return DateTime.UtcNow; //because currentdataitem is always at 59 secs so cannot be used for timeouts, etc.
        }

        private Order GetCurrentTradeIndicator(_Trade trade)
        {
            if (trade == null)
            {
                return null;
            }
            switch (trade.Type)
            {
                case TradeType.HMA_BREAK_ORDER:
                    return hmaPriceBreakOrderIndicator;
                case TradeType.LINE_BREAK_ORDER:
                    return priceBreakOrderIndicator;
                //case TradeType.MACD_LINE_BREAK_ORDER:
                //    return macdOrderIndicator;
                // case TradeType.AUTO_ORDER:
                //     return autoOrderIndicator;
                default:
                    return priceBreakOrderIndicator;
            }
        }

    

        public double GetHigh(_Trade trade)
        {
            return !TradeEngine.USE_SIMULATED_EXCHANGE
                ? currentDataItem.Close
                : trade.Position == TradePosition.LONG ? currentDataItem.High : currentDataItem.Low;
        }

        public double GetLow(_Trade trade)
        {
            return !TradeEngine.USE_SIMULATED_EXCHANGE
                ? currentDataItem.Close
                : trade.Position == TradePosition.LONG ? currentDataItem.Low : currentDataItem.High;
        }




        #endregion

        #region Draw

        public void UpdateCurrentTradeSetReference(DataContext db)
        {
            var referenceActive = db.Trades.Any(x => x.TradeReference == currentTradeSetReference && x.GlobalStatus == TradeGlobalStatus.ACTIVE);
            if (!referenceActive)
            {
                var trades = db.Trades.Where(x => x.SymbolName == symbolName &&
                                                            x.Action == TradeAction.ORDER &&
                                                            x.GlobalStatus == TradeGlobalStatus.ACTIVE).ToList();
                //look for any running order, this will be the default one
                var order = trades.FirstOrDefault(x => x.ItemStatus == TradeItemStatus.CLOSED);
                if (order == null)
                {
                    //no running order, so look for any waiting order
                    order = trades.FirstOrDefault(x => x.ItemStatus == TradeItemStatus.OPEN);
                }
                currentTradeSetReference = order == null ? null : order.TradeReference;
            }
        }

        public override void Draw()
        {
            //return;
            int startFrame = Chart.ScrollBars;
            int numFrames = Chart.ScaleBars;

            InitDraw();

            canvas = Chart.canvas;

            //not bothering to draw all of these
            //List<double> items_ema100 = hma_100?.GetResults(startFrame, numFrames, TimeFrame);

            #region set the scaling to exactly fill the panel

            Low = Chart.Low;
            High = Chart.High;

            SetScaling();

            #endregion

            double _yOffset = double.MaxValue;

#pragma warning disable CS8321 //disable warning for unused method
            void DrawEMAColored(List<double> items, int lineWidth, DrawingContext dc)
            {
                if (items == null || items.Count == 0)
                {
                    return;
                }

                //need the actual start frame in the visible timeframe, which is available from GetFrameCount()
                int frame = startFrame * MainObj.TimeFrame;

                for (int i = 0; i < items.Count - 1; i++) // emaLarge.GetResult(startFrame, numFrames))
                {
                    double startX = i;
                    double endX = i + 1;
                    double startY = items[i];
                    double endY = items[i + 1];

                    //get a suitable color
                    bool _isAbove60 = MovingAverage.IsAbove(ema_5, ema_10, frame, TimeFrame);

                    //SolidColorBrush color = _isAbove60 && _isRising60 ? colorUp : colorDown;
                    SolidColorBrush color = _isAbove60 ? Brushes.Blue : Brushes.Red;

                    //this method basically replicates PanelBase.DrawLineOverlayChart, which is in visible time scale.
                    frame += TimeFrame;

                     DrawLineSegment(startX, endX, ref startY, ref endY, lineWidth, color, dc);

                    //need to offset the drawing image down by the amount of emtpy space at the top 
                    //would also need to do this for left edge, probably, but this starts drawing from x == 0
                    _yOffset = _yOffset > startY ? startY : _yOffset;
                    _yOffset = _yOffset > endY ? endY : _yOffset;
                }

            }

#pragma warning disable CS8321
            void DrawEMA(List<double> items, int lineWidth, SolidColorBrush color, DrawingContext dc)
            {
                if (items == null || items.Count == 0)
                {
                    return;
                }

                //need the actual start frame in the visible timeframe, which is available from GetFrameCount()
                int frame = startFrame * TimeFrame;             

                for (int i = 0; i < items.Count-1; i++) // emaLarge.GetResult(startFrame, numFrames))
                {
                    double startX = i;
                    double endX = i + 1;
                    double startY = items[i];
                    double endY = items[i + 1];

                    frame += TimeFrame;

                    DrawLineSegment(startX, endX, ref startY, ref endY, lineWidth, color, dc);
                    
                    //need to offset the drawing image down by the amount of emtpy space at the top 
                    //would also need to do this for left edge, probably, but this starts drawing from x == 0
                    //there does not seem to be any way to get the clipping region, this is just null in drawingImage.Source
                    _yOffset = _yOffset > startY ? startY : _yOffset;
                    _yOffset = _yOffset > endY ? endY : _yOffset;
                }
            }

            try
            {
                //todo - move to constructor
                var drawingImage = new Image();
                DrawingGroup drawingGroup = new DrawingGroup();
                using (DrawingContext dc = drawingGroup.Open())
                {
                    //DrawEMA(items_ema100, 2, Brushes.Thistle, dc);

                    drawingImage.Source = new DrawingImage(drawingGroup);
                    drawingImage.IsHitTestVisible = false; //without this, this will block mouse capture from the underlying chart
                    //canvas.Background = MainObj.invertChart ? Brushes.AntiqueWhite : Brushes.White;
                    //canvas.Background.Freeze();
                    Canvas.SetTop(drawingImage, !MainObj.invertChart ? _yOffset+YOffset : _yOffset-YOffset);
                    canvas.Children.Add(drawingImage);

                }
                //DrawEMAColored(items_ema5, 1);
            }
            catch { }
            //DrawEMA(items_ema200, 2, Brushes.LightSkyBlue);
        }

        #endregion
    
    
        internal void ForceEntry(_Trade trade)
        {
            InitForDataContext(() =>
            {
                var order = DataContext.Trades.Where(x => x.Id == trade.Id).First();
                EnterTradeLive_synchronised(order, (decimal)order.Price);
            });

            MainObj.DrawCurrentDispatcher();
        }
    }
}