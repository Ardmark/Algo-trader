using AlgoTrader.Shared.Logging;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AlgoTrader.Live.ViewModels
{
    public class BatchResultsViewModel
    {
        MainViewModel mainViewModel;
        //TradeExecutionSystem executionSystem;
        Grid grid;
        int row = 0;

        public BatchResultsViewModel(MainViewModel mainViewModel, Grid grid)
        {
            this.mainViewModel = mainViewModel;
            this.grid = grid;
        }
        public void Header (string heading)
        {
            int numChars = heading.Count();
            StringBuilder s = new StringBuilder();
            for (int i=0;i<numChars;i++)
            {
                s.Append("-");
            }
            LogTrades.WriteLine(string.Format("\n{0}", s));
            LogTrades.WriteLine(heading);
            LogTrades.WriteLine(s.ToString());

            ColumnDefinition c1 = new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Auto)
            };
            ColumnDefinition c2 = new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Auto)
            };
            ColumnDefinition c3 = new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Auto)
            };
            grid.ColumnDefinitions.Add(c1);
            grid.ColumnDefinitions.Add(c2);
            grid.ColumnDefinitions.Add(c3);



            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Auto)
            });


            TextBox text = new TextBox
            {
                Text = heading,
                Margin = new Thickness(0, 0, 0, 20),
                FontSize = 20
            };

            grid.Children.Add(text);
            Grid.SetColumn(text, 0);
            Grid.SetColumnSpan(text, 2);
            Grid.SetRow(text, row);

            row++;
        }
        public void AddEntry(string name, double result)
        {
            LogTrades.WriteLine(string.Format("{0:0.00}%   {1}", result, name)); //note, % WITHIN braces treats 0.5 as 50%, etc.

            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Auto)
            });

            TextBox text = new TextBox
            {
                Text = result.ToString("0.00") + "%",
                Foreground = result < 0 ? Brushes.Red : Brushes.Blue,
                BorderBrush = Brushes.Transparent,
                Margin = new Thickness(6, 2, 6, 2),
                MaxWidth = 150
            };

            grid.Children.Add(text);
            Grid.SetColumn(text, 0);
            Grid.SetRow(text, row);

            TextBox text1 = new TextBox
            {
                Text = name,
                BorderBrush = Brushes.Transparent,
                Margin = new Thickness(6, 2, 6, 2)
            };

            grid.Children.Add(text1);
            Grid.SetColumn(text1, 1);
            Grid.SetRow(text1, row);

            row++;


        }
    }
}

