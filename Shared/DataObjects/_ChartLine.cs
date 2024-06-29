
using AlgoTrader.Shared.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Windows;

namespace AlgoTrader.Shared
{
    public partial class _ChartLine : UIDrawingBase //as opposed to line, which is a WPF object
    {
        public LineType Type { get; set; }
        public LineStyle Style { get; set; }
        public double Thickness { get; set; } = 2;

        //bounds
        public DateTime StartTimeStamp { get; set; }
        public DateTime EndTimeStamp { get; set; }
        public double StartPrice { get; set; }
        public double EndPrice { get; set; }

        public override void CopyTo(UIDrawingBase uiDrawingBase)
        {
            var chartLine = (_ChartLine)uiDrawingBase;

            chartLine.Type = Type;
            chartLine.Style = Style;
            chartLine.Thickness = Thickness;
            chartLine.StartTimeStamp = StartTimeStamp;
            chartLine.EndTimeStamp = EndTimeStamp;
            chartLine.StartPrice = StartPrice;
            chartLine.EndPrice = EndPrice;
        }

    }
}
