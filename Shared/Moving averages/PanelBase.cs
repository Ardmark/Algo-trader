using Bitmex_trader.DataObjects;
using Bitmex_trader.UI;
using System.Windows.Controls;

namespace Bitmex_trader
{
    /*
     * Base class for all panels
     * */
    public abstract class PanelBase
    {
        public Canvas canvas;
        public MainViewModel mainViewModel;

        public int CurrentTimeInterval { get; set; } = 1;

        public PanelBase(MainViewModel mainViewModel, Canvas canvas)
        {
            this.canvas = canvas;
            this.mainViewModel = mainViewModel;
        }

        //ItemAdded - called when an item is added to the DB, this can be used for example with a moving average
        public virtual void ItemAdded(BitmexDataItem item) { }

        //ItemUpdated - not currently implemented - this would be for live changes to the current minute's data
        public virtual void ItemUpdated(BitmexDataItem item) { }

        public abstract void Draw();

    }
}
