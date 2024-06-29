using System.Windows;

namespace AlgoTrader.Shared.Windows
{
    public partial class SetOrderAtPriceDialog : Window
    {
        public SetOrderAtPriceDialog(string initialValue, string title, double x, double y)
        {
            InitializeComponent();
            Loaded += new RoutedEventHandler(PromptDialog_Loaded);
            text.Text = initialValue;
            Title = title;
            //WindowStartupLocation = WindowStartupLocation.Manual;
            //location = new Point(x, y);
        }

        void PromptDialog_Loaded(object sender, RoutedEventArgs e)
        {
            text.Focus();
            //var pos = PointToScreen(Mouse.GetPosition(Application.Current.MainWindow));
            //Debug.WriteLine(pos);
            //var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)location.X, (int)location.Y));
            //Left = pos.X + screen.Bounds.X;
            //Top = pos.Y + screen.Bounds.Y;
        }

        public static string Prompt(string initialValue, string title, double x, double y)
        {
            SetOrderAtPriceDialog inst = new SetOrderAtPriceDialog(initialValue, title, x, y);
            inst.ShowDialog();
            if (inst.DialogResult == true)
            {
                return inst.ResponseText;
            }
            return null;
        }

        public string ResponseText
        {
            get
            {
                return text.Text;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
    }
}
