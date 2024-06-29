using AlgoTrader.Shared.ViewModels;
using System;
using System.Windows;

namespace AlgoTrader.Shared.Windows
{
    public class TradeMenuImpl
    {
        Point mousePos;
        Window MainWindow;
        public PopupMenuViewModel DataContext { get; set; }

        public TradeMenuImpl(Point point, Window MainWindow, params Action[] actions)
        {
            point.X += 10;
            point.Y += 10;
            mousePos = point;
            this.MainWindow = MainWindow;
            DataContext = new PopupMenuViewModel(actions);
        }

        public void SetPosition(EventArgs e, Window window)
        {
            if (MainWindow.WindowState == WindowState.Maximized)
            {
                var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)window.Left, (int)window.Top));
                window.Left = mousePos.X + screen.Bounds.X;
                window.Top = mousePos.Y + screen.Bounds.Y;
            }
            else
            {
                window.Left = mousePos.X + MainWindow.Left;
                window.Top = mousePos.Y + MainWindow.Top;
            }
        }
    }
}
