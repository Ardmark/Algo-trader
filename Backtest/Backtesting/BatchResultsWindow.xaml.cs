using AlgoTrader.Backtest.Backtesting;
using AlgoTrader.Live.ViewModels;
using System.Windows;

namespace AlgoTrader.Backtest.Windows
{
    public partial class BatchResultsWindow : Window
    {
        public BatchResultsViewModel batchResultsViewModel;
        public MainViewModel mainViewModel;
        public BatchResultsWindow(MainViewModel mainViewModel)
        {
            this.mainViewModel = mainViewModel;
            InitializeComponent();

            batchResultsViewModel = new BatchResultsViewModel(mainViewModel, MainGrid);
            DataContext = batchResultsViewModel;
        }
    }
}
