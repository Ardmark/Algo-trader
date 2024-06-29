using System;

namespace AlgoTrader.Shared
{
    public static class PriceFormatter
    {
        public static string Format(double price)
        {
          //  return price.ToString("G6"); //returns scientific notation            
            const int MIN_SIG_FIGS = 4; //will be one more for < 0
            const int MIN_DEC_PLACES = 2;
            int numZeros = (int)Math.Floor(Math.Log10(Math.Abs(price))); //get number of zeros before first digit, will be negative for price > 0
            int decPlaces = numZeros < MIN_SIG_FIGS
                                ? MIN_SIG_FIGS - numZeros < MIN_DEC_PLACES 
                                    ? MIN_DEC_PLACES
                                    : MIN_SIG_FIGS - numZeros 
                                : MIN_DEC_PLACES; //dec. places: set to MIN_SIG_FIGS + number of zeros, unless numZeros greater than sig figs then no decimal places
            var s = price.ToString($"F{decPlaces}");

            //price could be 0.000234000000001 so string will have zeros at end, very confusing
            //need to parse the string to remove these trailing zeros
            int i = s.Length - 1;
            for (; i>-1; i--)
            {
                if (s.Substring(i, 1) != "0")
                {
                    break;
                }
            }

            return s.Substring(0, i+1);
        }
    }
}
