using System;
using System.Windows;

namespace AlgoTrader.Shared.Windows
{
    /// <summary>
    /// Interaction logic for OrderMenu.xaml
    /// </summary>
    public partial class OrderMenu : Window
    {
        readonly TradeMenuImpl tradeMenuImpl;

        public OrderMenu(Point point, Window mainWindow, params Action[] actions)
        {
            InitializeComponent();
            tradeMenuImpl = new TradeMenuImpl(point, mainWindow, actions);
            DataContext = tradeMenuImpl.DataContext;
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            tradeMenuImpl.SetPosition(e, this);
        }
    }
}
