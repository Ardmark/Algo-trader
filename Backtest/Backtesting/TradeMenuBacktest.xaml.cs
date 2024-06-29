using AlgoTrader.Shared.Windows;
using AlgoTrader.Windows;
using System;
using System.Windows;

namespace AlgoTrader.Backtest.Windows
{
    /// <summary>
    /// Interaction logic for OrderMenu.xaml
    /// </summary>
    public partial class TradeMenuBacktest : Window
    {
        TradeMenuImpl tradeMenuImpl;

        public TradeMenuBacktest(Point point, MainWindow mainWindow, params Action[] actions)
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
