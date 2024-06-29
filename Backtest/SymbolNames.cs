using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;

namespace AlgoTrader.Shared
{
    public static class SymbolNames
    {
        public const string FIAT_ASSET = "USDT";


        public const string LIST_1 = "Main symbols";
        public const string LIST_2 = "ETF up";
        public const string LIST_3 = "ETF down";
        public const string LIST_4 = "Secondary symbols";
        public const string LIST_5 = "Miscelaneous";

        //all the symbol names to retrieve and trade with (separate classes for both projects)
        public static List<(string baseAsset, string quoteAsset, string currency, int tickSize, string listName)> SYMBOL_NAMES = new List<(string, string, string, int, string)>
        {

          //big ones at the top for threading efficiency
          //("BTC",       "USDT",     LIST_1),
          ("XBT", "USD", "XBt", 0, LIST_1) //note that tick size will differ with different symbols
        };


        public static List<string> SYMBOLS_TO_EXCLUDE = new List<string>
        {
            //"INS",
        };
        
        //  From the API docs:
        //    base asset refers to the asset that is the quantity of a symbol. For the symbol BTCUSDT, BTC would be the base asset.
        //    quote asset refers to the asset that is the price of a symbol. For the symbol BTCUSDT, USDT would be the quote asset.
        
        public static IEnumerable<string> GetSymbolNames()
        {
            foreach (var s in SYMBOL_NAMES)
            {
                yield return s.baseAsset + s.quoteAsset;
            }
        }

        public static string GetCurrencyForSymbol(string symbolName)
        {
            var v = SYMBOL_NAMES.Where(x => x.baseAsset + x.quoteAsset == symbolName).Select(x => x.currency).FirstOrDefault();
            return v != null ? v : null;
        }
        public static string GetQuoteAssetForSymbol(string symbolName)
        {
            var v = SYMBOL_NAMES.Where(x => x.baseAsset + x.quoteAsset == symbolName).Select(x => x.quoteAsset).FirstOrDefault();
            return v != null ? v : null;
        }
        public static string GetBaseAssetForSymbol(string symbolName)
        {
            var v = SYMBOL_NAMES.Where(x => x.baseAsset + x.quoteAsset == symbolName).Select(x => x.baseAsset).FirstOrDefault();
            return v != null ? v : null;
        }
        public static string GetQuoteAssetForBaseAsset(string baseAsset) //e.g. for STEEM, will return USDT if that is the trading pair
        {
            var v = SYMBOL_NAMES.Where(x => x.baseAsset == baseAsset).Select(x => x.baseAsset).FirstOrDefault();
            return v != null ? v : null;
        }
        public static int GetTickSizeForSymbol(string symbolName) //e.g. for STEEM, will return USDT if that is the trading pair
        {
            var v = SYMBOL_NAMES.Where(x => x.baseAsset + x.quoteAsset == symbolName).Select(x => x.tickSize).FirstOrDefault();
            return v;
        }

        public static (string, string) GetAssetsForSymbol(string symbolName)
        {
            try
            {
                var v = SYMBOL_NAMES.Where(x => x.baseAsset + x.quoteAsset == symbolName).First();
                return (v.baseAsset, v.quoteAsset);
            }
            catch
            {
                return (null, null);
            }
        }

        public static bool AssetListed(string symbolName)
        {
            return SYMBOL_NAMES.Where(x => x.baseAsset + x.quoteAsset == symbolName).Any();
        }
    }
}


