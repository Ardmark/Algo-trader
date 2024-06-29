using AlgoTrader.Shared;
using System.Windows;

namespace AlgoTrader
{
    public partial class App : Application
    {

        //entry point is in MainWindow.xaml.cs, this class is only used to pump the Application_Exit event which closes the node.js connector
        public App()
        {
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            MainObj.Exit();
        }
    }
}
