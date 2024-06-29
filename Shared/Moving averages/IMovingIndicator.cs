using Bitmex_trader.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bitmex_trader.Indicators
{
    //this interface forces implementation of the two critical callback functions
    public interface IMovingIndicator
    {
        double CalculateMovingAverage(in List<DataItem> values);

    }
}
