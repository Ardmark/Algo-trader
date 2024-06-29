using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace AlgoTrader.Shared.Windows
{
    /// <summary>
    /// Interaction logic for OrderMenu.xaml
    /// </summary>
    public partial class LineMenuLeftClick : Window
    {

        readonly PopupMenuImpl tradeMenuImpl;

        //public bool AllowClose
        //{
        //    get
        //    {
        //        return tradeMenuImpl.AllowClose;
        //    }
        //    set
        //    {
        //        tradeMenuImpl.AllowClose = value;
        //    }
        //}

        public LineMenuLeftClick(Point point, Window mainWindow, params Action[] actions)
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
    }
}
