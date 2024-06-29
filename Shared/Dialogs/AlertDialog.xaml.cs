using System;
using System.Windows;

namespace AlgoTrader.Shared.Windows
{/*
  * Alert dialog
  * Non-modal, unlike MessageBox
  * NOTE - MessageBox can be safely called from any menu-driven function, that will not block ProcessData which still carries on in the background
  * However, this class MUST be used when called by the ProcessData-driven thread(s) i.e. confirmation, error alert etc.
  * Otherwise, if using MessageBox, the dialog will block the trade engine.
  * Additionally, if MessageBox is called outside of Application.Current.Dispatcher, it won't run on the UI thread so the dialog can be hidden behind the main window (alt-tab), 
  *  - but it will still block the calling thread and disable data processing until dismissed
  * To prevent this, if a modal is required, the MessageBox wrapper here should be called instead, to put it on the UI thread thus forcing user input
  * (this saves setting this up a modal dialog window here separately)
  * 
  * For consistency, all MessageBox calls have been replaced with AlertDialog, non-modal, not required from within UI context as that does not 
  *  block ProcessData-driven threads, but this is just more consistent throughout
  * DO NOT USE CALLBACKS IN ANY ProcessData-driven thread - the data context will have been disposed and all properties nulled when the callback is executed
  * */
    public partial class AlertDialog : Window
    {
        public AlertDialog(string message, string title, Action callback, double x, double y, bool hideCancel = false)
        {
            InitializeComponent();
            Loaded += new RoutedEventHandler(AlertDialog_Loaded);
            text.Text = message;
            Title = title;
            this.callback = callback;
            btnCancel.Visibility = hideCancel ? Visibility.Hidden : Visibility.Visible;
            //WindowStartupLocation = WindowStartupLocation.Manual;
            //location = new Point(x, y);
        }

        void AlertDialog_Loaded(object sender, RoutedEventArgs e)
        {
            //var pos = PointToScreen(Mouse.GetPosition(Application.Current.MainWindow));
            //Debug.WriteLine(pos);
            //var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)location.X, (int)location.Y));
            //Left = pos.X + screen.Bounds.X;
            //Top = pos.Y + screen.Bounds.Y;
        }

        static string currentTitle = null;
        static string currentMessage = null;
        static Action currentCallback = null;
        static AlertDialog currentDialog = null;
        Action callback;

        public static void ShowCallback(string message, string title, Action callback, double x = 0, double y = 0, bool hideCancel = false)
        {
            //to prevent showing multiple identical message boxes, one on top of the other, e.g. for a UI move event which is not allowed,
            //check for variation of data. This would not happen with modal dialog.
            if (currentMessage == message && currentTitle == title && currentCallback == callback)
            {
                if (currentDialog != null)
                {
                    try
                    {
                        // currentDialog.BringIntoView();
                        currentDialog.WindowState = WindowState.Minimized;
                        currentDialog.Show();
                        currentDialog.WindowState = WindowState.Normal;
                    }
                    catch
                    {
                        currentDialog = null;
                    }
                    return;
                    //currentDialog.Close();
                }
            }

            currentMessage = message;
            currentTitle = title;
            currentCallback = callback;

            Application.Current.Dispatcher.Invoke(() =>
            {
                currentDialog = new AlertDialog(message, title, callback, x, y, hideCancel);
                currentDialog.Show();
            });
        }
        public static void Show(string message)
        {
            ShowCallback(message, "", null);
        }
        public static void Show(string message, string title)
        {
            ShowCallback(message, title, null, 0, 0, true);
        }
        public static void Show(string message, string title, bool hideCancel)
        {
            ShowCallback(message, title, null, 0, 0, hideCancel);
        }
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Close();
            if (callback != null)
            {
                callback();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            currentMessage = currentTitle = null;
            currentCallback = null;
            currentDialog = null;
        }

    }
}
