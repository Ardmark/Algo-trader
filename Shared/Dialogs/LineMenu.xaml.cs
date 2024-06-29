using System;
using System.Windows;

namespace AlgoTrader.Shared.Windows
{
    /// <summary>
    /// Interaction logic for OrderMenu.xaml
    /// </summary>
    public partial class LineMenu : Window
    {
        readonly PopupMenuImpl lineMenuImpl;

        public LineMenu(Point point, Window mainWindow, params Action[] actions)
        {
            InitializeComponent();
            lineMenuImpl = new PopupMenuImpl(point, mainWindow, actions);
            DataContext = lineMenuImpl.DataContext;
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            lineMenuImpl.SetPosition(e, this);
        }
    }
}
