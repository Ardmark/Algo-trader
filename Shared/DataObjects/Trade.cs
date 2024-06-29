using AlgoTrader.Shared.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlgoTrader.Shared
{ 
    public class _Trade
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string TradeReference { get; set; } //shared GUID for all trades in this order group (order, entry, stop etc.)
        public string SymbolName { get; set; } //the specific name of the trade type, i.e. 'Crossover market', 'Stochastic crossing' etc.
        public TradeItemStatus ItemStatus { get; set; }
        public TradeGlobalStatus GlobalStatus { get; set; }

        //trade data
        public TradeType Type { get; set; } //price break, line break etc
        public TradeAction Action { get; set; }
        public TradeStop Stop { get; set; }
        public TradeExchangeStatus ExchangeStatus { get; set; } //entered on or off exchange
        public DateTime Timestamp { get; set; }
        public double ActionValue { get; set; } //trail value
        public int TimeFrame { get; set; }

        #region properties which are unused or use default value
        public ExchangeId ExchangeId { get; set; }
        public double Leverage { get; set; } = 1;
        public TradeExecutionSystem ExecutionSystem { get; set; } //manual or auto
        public TradePosition Position { get; set; } //long or short
        #endregion
        
        //quantiy, price and result
        public double Quantity { get; set; } //note, decimal has only 2 decimal places in db, so will truncate anything < 0.01 to zero
        public double Price { get; set; } //whatever the price is at the time of the trade, this will likely not be the minute close or open value
        public double Result { get; set; }
        
        //exchange event handler data
        public string ItemId { get; set; } //unique id for this trade, as opposed to the TradeReference which is shared across all trades used in this order.Note: Binance will allow re-use of an Id providing no orders are open and using that Id, but it is better to assign a unique Id each time anyway
        public long TradeIdExchange { get; set; } //unimplemented - this is returned by exchange, can use either ItemId or this to query Binance orders
        public int ExchangeMethodIndex { get; set; }
        public string ExchangeMethodScript { get; set; }

        public bool IsQuoteToBasePurchase { get; set; }
        public string StubCallbackTradeReference { get; set; }

        public double LiquidationPrice { get; set; }

        #region Drawing (one - to - many, unused)
        //adding this seems to change the entire db structure, renaming dbo.ChartLines to dbo.DrawingBases....
        //but this is pointless anyway, as a trade can only be attached to a single ui drawing object anyway
        //public ICollection<DrawingBase> Drawings { get; set; }
        #endregion

        //don't need this in db
        //[System.ComponentModel.DataAnnotations.Schema.NotMapped]
        //public TradeUpdateRequest UpdateRequest { get; set; }


        public static string GetNewTradeId()
        {
            return Guid.NewGuid().ToString().Replace("-", ""); //used as a canvas name, it cannot have hyphens
        }

        //note, clone and copy do not transfer the ExchangeMethodScript or ExchangeMethodIndex
        internal _Trade Clone()
        {
            return new _Trade
            {
                //Id = Id, //do not clone the Id, since that is unique between trades and is EF-assigned
                Id = 0, //marks as a newly created trade
                TradeReference = TradeReference,
                Type = Type,
                Position = Position,
                Action = Action,
                Stop = Stop,
                ExecutionSystem = ExecutionSystem,
                Timestamp = Timestamp,
                ActionValue = ActionValue,
                TimeFrame = TimeFrame,
                Price = Price,
                Result = Result,
                SymbolName = SymbolName,
                ExchangeId = ExchangeId,
                ExchangeStatus = ExchangeStatus,
                Quantity = Quantity,
                ExchangeMethodScript = ExchangeMethodScript,
                ExchangeMethodIndex = ExchangeMethodIndex,
                StubCallbackTradeReference = StubCallbackTradeReference,
                IsQuoteToBasePurchase = IsQuoteToBasePurchase,
                TradeIdExchange = TradeIdExchange,
                ItemId = ItemId,
                LiquidationPrice = LiquidationPrice,
                Leverage = Leverage,


                //although these are defaults, set them explicitly anyway
                ItemStatus = TradeItemStatus.OPEN,
                GlobalStatus = TradeGlobalStatus.ACTIVE,
            };
        }
        internal void CopyTo(_Trade trade)
        {
            //Id = stop.Id; as for Clone()
            trade.TradeReference = TradeReference;
            trade.Type = Type;
            trade.Position = Position;
            trade.Action = Action;
            trade.Stop = Stop;
            trade.ExecutionSystem = ExecutionSystem;
            trade.Timestamp = Timestamp;
            trade.ActionValue = ActionValue;
            trade.TimeFrame = TimeFrame;
            trade.Price = Price;
            trade.Result = Result;
            trade.SymbolName = SymbolName;
            trade.ExchangeId = ExchangeId;
            trade.ExchangeStatus = ExchangeStatus;
            trade.ItemStatus = ItemStatus;
            trade.GlobalStatus = GlobalStatus;
            trade.Quantity = Quantity;
            trade.ExchangeMethodScript = ExchangeMethodScript;
            trade.ExchangeMethodIndex = ExchangeMethodIndex;
            trade.StubCallbackTradeReference = StubCallbackTradeReference;
            trade.IsQuoteToBasePurchase = IsQuoteToBasePurchase;
            trade.TradeIdExchange = TradeIdExchange;
            trade.ItemId = ItemId;   
            trade.LiquidationPrice = LiquidationPrice;
            trade.Leverage = Leverage;
        }

        //public static TradeExecutionSystem GetExecutionSystem(object obj)
        //{
        //    try
        //    {
        //        if (int.TryParse((string)obj, out int i))
        //        {
        //            switch (i)
        //            {
        //                case 1: return TradeExecutionSystem.MANUAL;
        //                case 2: return TradeExecutionSystem.AUTO;
        //            }
        //        }
        //    }
        //    catch
        //    {

        //    }
        //    return TradeExecutionSystem.MANUAL;
        //}


    }
}
