#if BACKTEST
using AlgoTrader.Live.ViewModels;
#elif ARCHIVE
using AlgoTrader.Archive.ViewModels;
#elif LIVE
using AlgoTrader.Live.ViewModels;
#endif

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using AlgoTrader.Shared;
using System.Windows.Input;

namespace AlgoTrader.Windows
{
    public partial class MainWindow : Window//, IMainWindow
    {
        public MainViewModel mainViewModel;
        public static MainWindow instance;
        //static DispatcherTimer alertTimer;
        //const string ALERT_SOUND = "alarm-clock-ringing-01.mp3";

        public MainWindow()
        {
            instance = this;
            InitializeComponent();
            mainViewModel = new MainViewModel(this);
            DataContext = mainViewModel;
        }

        bool firstActivation = true;
        private async void Window_Activated(object sender, EventArgs e)
        {
            //all events fire during initialisation, including MouseWheel which changes magnification from starting default.
            //This causes the scroll position to appear differently to when ScrollToEnd is called, just a minor point but a little annoying
            // - this resets it.
            if (firstActivation)
            {
                var secondaryScreenLeft = SystemParameters.PrimaryScreenWidth - SystemParameters.VirtualScreenWidth;

                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = secondaryScreenLeft;
                Top = 0;
                Loaded += Window_Loaded;

                firstActivation = false;
                SetupConstants();

                await Task.Run(
                    () =>
                    {
                        MainObj.Setup(PanelsContainer, Chart, TimeBar, Indicator1);
                        mainViewModel.SetWindowTitle();
                    });

            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var senderWindow = sender as Window;
            senderWindow.WindowState = WindowState.Maximized;
        }

        private void DataWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!MainObj.BACKTEST)
            {
                //this is modal, but will only block the UI thread
                if (MessageBox.Show("Close application", "Are you sure?", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }

        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            mainViewModel.RemovePopupWindow();

            var newWidth = e.NewSize.Width;
            var newHeight = e.NewSize.Height;

            MainGrid.Width = newWidth - 16; //the actual window is 16 pixels smaller than the size reported here, poss. scrollbar width??
                                            //     MainGrid.Height = newHeight; //do not set, or it messes up auto-scaling of interal elements
            MainObj.DrawImmediate();
        }
        private void Window_Moved(object sender, EventArgs e)
        {
            mainViewModel.RemovePopupWindow();
        }
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            mainViewModel.KeyDown(e);
        }
        private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            mainViewModel.KeyUp(e);
        }
        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            mainViewModel.MouseLeave();
        }
        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            //Keyboard.Focus(this);
        }
        private void Window_MouseUp(object sender, System.Windows.Input.MouseEventArgs e)
        {
            mainViewModel.OnMouseUp();
        }

    }
}
