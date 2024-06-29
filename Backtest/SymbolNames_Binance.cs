/*
using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;

namespace AlgoTrader.Shared
{
    public static class SymbolNames
    {
        public const string FIAT_ASSET = "USDT";


        public const string LIST_1 = "Base symbols";
        public const string LIST_2 = "Primary";
        public const string LIST_3 = "Secondary";
        //public const string LIST_4 = "Secondary symbols";
        //public const string LIST_5 = "Miscelaneous";


        //all the symbol names to retrieve and trade with (separate classes for both projects)
        public static List<(string baseAsset, string quoteAsset, string currency, int tickSize, string listName)> SYMBOL_NAMES = new List<(string, string, string, int, string)>
        {
          //big ones at the top for threading efficiency
          ("BTC",    "USDT",    null,   0,     LIST_1),    //no longer trading to this as a quote asset, no point since all BTC pairs trade as some fiat asset as well
          ("BNB",    "USDT",    null,   0,     LIST_1),   //need this since always need to keep a BNB balance

          ("ADA",    "USDT",    null,   0,     LIST_2),
          ("ATOM",   "USDT",    null,   0,     LIST_2),
          ("CKB",    "USDT",    null,   0,     LIST_2),
          ("DOGE",   "USDT",    null,   0,     LIST_2),
          ("ETC",    "USDT",    null,   0,     LIST_2),
          ("ETH",    "USDT",    null,   0,     LIST_2),         
          ("FIL",    "USDT",    null,   0,     LIST_2),    //defi
          ("LUNA",   "USDT",    null,   0,     LIST_2),    //layer 1 / layer 2 / pow
          ("MATIC",  "USDT",    null,   0,     LIST_2),    //pow
          ("SOL",    "USDT",    null,   0,     LIST_2),    //pos
          ("TRX",    "USDT",    null,   0,     LIST_2),    //defi
          ("UNI",    "USDT",    null,   0,     LIST_2),    //infrastructure
          ("XEC",    "USDT",    null,   0,     LIST_2),    //defi
          ("XLM",    "USDT",    null,   0,     LIST_2),    //infrastructure
          ("XRP",    "USDT",    null,   0,     LIST_2),    //pow
          ("XTZ",    "USDT",    null,   0,     LIST_2),    //pos

          //("RVN",    "USDT",    null,   0,     LIST_2),    //pos
          //("STORJ",  "USDT",    null,   0,     LIST_3),    //enterprise software
          //("WAVES",  "USDT",    null,   0,     LIST_2),    //pos / pow
          //("ACA",    "USDT",    null,   0,     LIST_3),
          //("ANKR",   "USDT",    null,   0,     LIST_3),    
          //("BAT",    "USDT",    null,   0,     LIST_3),
          //("BNX",    "USDT",    null,   0,     LIST_3),
          //("EOS",    "USDT",    null,   0,     LIST_3),
          //("FLUX",   "USDT",    null,   0,     LIST_3),    //pos / pow
          //("HOT",    "USDT",    null,   0,     LIST_3),    //infrastructure
          //("KNC",    "USDT",    null,   0,     LIST_3),    //bnb chain
          //("PUNDIX", "USDT",    null,   0,     LIST_3),    //storage
          //("QNT",    "USDT",    null,   0,     LIST_3),    //infrastructure
          //("QTUM",   "USDT",    null,   0,     LIST_3),    //storage
          //("REEF",   "USDT",    null,   0,     LIST_3),    //layer 1 / layer
          //("RLC",    "USDT",    null,   0,     LIST_3),    //layer 1 / layer 2 / pos
          //("SYS",    "USDT",    null,   0,     LIST_3),    //pow
          //("VGX",    "USDT",    null,   0,     LIST_3),    //storage
          //("ZEN",    "USDT",    null,   0,     LIST_3),    //pow
          //("ZRX",    "USDT",    null,   0,     LIST_3),    //defi
          
           ////etf up
          //("BTCUP",     "USDT",     LIST_4),
          //("BTCDOWN",   "USDT",     LIST_4),
          //("BNBUP",     "USDT",     LIST_2),
          //("ADAUP",     "USDT",     LIST_2),
          //("BNBUP",     "USDT",     LIST_2),
          //("DOTUP",     "USDT",     LIST_2),
          //("ETHUP",     "USDT",     LIST_2),
          //("LINKUP",    "USDT",     LIST_2),
          //("TRXUP",     "USDT",     LIST_2),
          //("XRPUP",     "USDT",     LIST_2),

          ////etf down
          //("ADADOWN",   "USDT",     LIST_3),
          //("BNBDOWN",   "USDT",     LIST_3),
          //("DOTDOWN",   "USDT",     LIST_3),
          //("ETHDOWN",   "USDT",     LIST_3),
          //("LINKDOWN",  "USDT",     LIST_3),
          //("TRXDOWN",   "USDT",     LIST_3),
          //("XRPDOWN",   "USDT",     LIST_3),

        };
        

        public static List<string> SYMBOLS_TO_EXCLUDE = new List<string>
        {
            //"INS",
        };
        
        // From the API docs:
        // base asset refers to the asset that is the quantity of a symbol. For the symbol BTCUSDT, BTC would be the base asset.
        // quote asset refers to the asset that is the price of a symbol. For the symbol BTCUSDT, USDT would be the quote asset.
        
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

*/

