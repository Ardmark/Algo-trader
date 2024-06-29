using AlgoTrader.Shared.Enums;
using System.Collections.Generic;
using System.Windows.Input;

namespace AlgoTrader.Shared.Interfaces
{
    public interface IMainViewModel
    {
        void SetItemPrice(_SymbolDataItem item);
        void ExceptionAlert(string shortMsg, string longMsg);
        void PopupMenuForLine(object sender, MouseButtonEventArgs e);
        void PopupMenuForOrder(object sender, MouseButtonEventArgs e);
        void PopupMenuForTrade(object sender, MouseButtonEventArgs e);
        void PopupMenuForOrderPlacement(object sender, MouseButtonEventArgs e);
        void CurrentSeconds(double seconds);
        void SetProperty<T>(string propName, T value);
        void TradeStatusChanged(List<_Trade> activeTrades, List<_Trade> waitingTrades, string symbolName, string price);
        void DisplayLog(string s);
        void DisplayTradeSummary(string s);
        void KeyUp(Key key);
        void KeyDown(Key key);
        //void ScrollToEnd();
        void SetTimeframeMenuState(int timeFrame);
        List<DisplayPanel> InitCallback(TradeEngine tradeEngine, Chart chart);
        //void InitMenu();
        void SetInitialTime(int timeFrame);
        void SetConcurrentOrdersMenuState(int concurrentOrders);
        void SetLeverageMenuState(int leverage);
    }
}
