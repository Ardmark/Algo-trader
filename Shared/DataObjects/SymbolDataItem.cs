using AlgoTrader.Shared.Enums;
using AlgoTrader.Shared.ExchangeConnection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;

namespace AlgoTrader.Shared
{
    public class _SymbolDataItem
    {
        [Key]
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public double Open { get; set; }
        public double Close { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        //public ExchangeId ExchangeId { get; set; }
        public bool Interrupted { get; set; }

        #region Init
        public _SymbolDataItem() { }

        public _SymbolDataItem(ExchangeConnector.FeedDataItem item)
        {
            High = Low = Open = Close = item.price;
            Timestamp = item.timestamp;
        }

        public _SymbolDataItem(SqlDataReader reader, string symbolName)
        {
            var values = new Object[reader.FieldCount];
            reader.GetValues(values);
            Id = (int)reader["Id"];
            Timestamp = (DateTime)reader["Timestamp"];
            Open = (double)reader["Open"];
            Close = (double)reader["Close"];
            High = (double)reader["High"];
            Low = (double)reader["Low"];
            //ExchangeId = (ExchangeId)reader["ExchangeId"];
            Interrupted = (bool)reader["Interrupted"];
        }

        internal _SymbolDataItem Clone()
        {
            return new _SymbolDataItem
            {
                Open = Open,
                Close = Close,
                High = High,
                Low = Low,
                Timestamp = Timestamp,
                //SymbolName = SymbolName,
                //ExchangeId = ExchangeId,
                Interrupted = Interrupted
            };
        }

        internal void CopyTo(_SymbolDataItem ret)
        {
            ret.Open = Open;
            ret.Close = Close;
            ret.High = High;
            ret.Low = Low;
            ret.Timestamp = Timestamp;
            //ret.ExchangeId = ExchangeId;
            ret.Interrupted = Interrupted;
        }
        #endregion

        #region Update
        internal void Update(string symbolName, _SymbolDataItem item, bool first)
        {
            if (High < item.High)
            {
                High = item.High;
            }
            if (Low > item.Low)
            {
                Low = item.Low;
            }
            if (first)
            {
                High = Low = Open = item.Close;
            }
            Close = item.Close;
            Timestamp = item.Timestamp;

        }

        internal void Update(ExchangeConnector.FeedDataItem item)
        {
            if (High < item.price)
            {
                High = item.price;
            }
            if (Low > item.price)
            {
                Low = item.price;
            }

            Close = item.price;
            Timestamp = item.timestamp;
        }
        #endregion

        #region Get Max / Min values
        //the Close of the current data item will come directly from the exchange, but there may have been a higher high between this and the prior sent data item

        internal double GetMaxValue(TradePosition position)
        {
            return position == TradePosition.LONG ? High > Close ? High : Close : Low < Close ? Low : Close;
        }

        internal double GetMinValue(TradePosition position)
        {
            return position == TradePosition.LONG ? Low < Close ? Low : Close : High > Close ? High : Close;
        } 
        #endregion

        #region Database operations
        internal static void CreateTableCommand(string symbolName, SqlConnection conn)
        {
            //the param after the FLOAT specifies this is a double
            var query = $"create table {symbolName} ([Id] INT IDENTITY(1, 1) NOT NULL, [Timestamp] DATETIME NOT NULL, [Open] FLOAT(53) NOT NULL, [Close] FLOAT(53) NOT NULL,[High] FLOAT(53) NOT NULL, [Low] FLOAT(53) NOT NULL, [Interrupted] BIT NOT NULL)";
            using (SqlCommand command = new SqlCommand(query, conn))
            {
                command.ExecuteNonQuery();
            }
        }
        internal void ExecuteInsertCommand(string symbolName, SqlConnection conn)
        {
            var query = $"INSERT INTO[dbo].[{symbolName}]([Timestamp],[Open],[Close],[High],[Low],[Interrupted]) VALUES(@Timestamp, @Open, @Close, @High, @Low, @Interrupted)";

            using (SqlCommand command = new SqlCommand(query, conn))
            {
                command.Parameters.AddWithValue("@Timestamp", Timestamp);
                command.Parameters.AddWithValue("@Open", Open);
                command.Parameters.AddWithValue("@Close", Close);
                command.Parameters.AddWithValue("@High", High);
                command.Parameters.AddWithValue("@Low", Low);
                command.Parameters.AddWithValue("@Interrupted", Interrupted);
                int result = command.ExecuteNonQuery();
                // Check Error
                if (result < 0)
                {
                    Debug.WriteLine("Error inserting data into Database!");
                }
            }
        }

        #endregion


        internal void SetTimestamp()
        {
            Timestamp = new DateTime(
                Timestamp.Year,
                Timestamp.Month,
                Timestamp.Day,
                Timestamp.Hour,
                Timestamp.Minute,
                0
                );
        }
        //not overloading the operator as that means all == and != will be overloaded, so cannot compare reference values
        public bool CompareWith(_SymbolDataItem b)
        {
            if (this == null || b == null) return false;
            return High == b.High && Low == b.Low && Open == b.Open && Close == b.Close;
        }



    }

}
