using AlgoTrader.Shared.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AlgoTrader.Shared
{
    public static class List
    {
        public static T GetPanelByType<T>(this List<T> panels, Type panelType)
        {
            var ret = panels.Where(x => x.GetType().BaseType == panelType).FirstOrDefault();
            if (ret != null)
            {
                return ret;
            }
            return panels.Where(x => x.GetType() == panelType).FirstOrDefault();
        }

        //see notes in UIDrawingBase about why the Trade is attached to the Line, and not vice-versa
        public static _ChartLine GetLineForTrade(this List<_ChartLine> lines, _Trade trade)
        {
            //in backtesting, the line trade does not reference the order for some reason, so have switched to Id comparison instead
            return trade != null
                        ? lines.Where(x => x.TradeId == trade.Id).FirstOrDefault()
                        : null;
        }

        public static _Trade GetTradeForTradeReference(this List<_Trade> trades, TradeAction action, string tradeReference)
        {
            return trades.FirstOrDefault(x =>
                            x.Action == action &&
                            x.TradeReference == tradeReference);
        }

        public static string GetEnumDisplayName(this Enum enumType)
        {
            return enumType.GetType().GetMember(enumType.ToString())
                           .First()
                           .GetCustomAttribute<DisplayAttribute>()
                           .Name;
        }
    }
}
