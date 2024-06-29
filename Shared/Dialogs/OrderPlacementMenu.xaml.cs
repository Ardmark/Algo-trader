using System;
using System.Windows;

namespace AlgoTrader.Shared.Windows
{
    /// <summary>
    /// Interaction logic for OrderMenu.xaml
    /// </summary>
    public partial class OrderPlacementMenu : Window
    {
        PopupMenuImpl tradeMenuImpl;

        public OrderPlacementMenu(Point point, Window mainWindow, params Action[] actions)
        {
            InitializeComponent();
            tradeMenuImpl = new PopupMenuImpl(point, mainWindow, actions);           
            DataContext = tradeMenuImpl.DataContext;
        }       

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            tradeMenuImpl.SetPosition(e, this);     
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {

        }
    }
}
