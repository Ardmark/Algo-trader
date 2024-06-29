using AlgoTrader.Shared.ViewModels;
using AlgoTrader.Shared.Enums;
using System.Windows;


//while this is used by 2 projects, it is very difficult to put in Shared due to the MainViewModel reference
//View model is in shared project, it is just easier to put it there
namespace AlgoTrader.Shared.Windows
{
    public partial class ResultsWindow : Window
    {
        public ResultsViewModel resultsViewModel;

        public ResultsWindow()
        {
            InitializeComponent();
            resultsViewModel = new ResultsViewModel();
            DataContext = resultsViewModel;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            resultsViewModel.Populate(MainGrid);
        }
    }
}
