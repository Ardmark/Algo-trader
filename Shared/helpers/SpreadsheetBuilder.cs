using AlgoTrader.Shared;
using AlgoTrader.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;


namespace AlgoTrader.Shared
{
    /*
     * NOTES
     * THIS IS SLOW
     * When only getting a couple of fields, it is instant; 12, it is about a third the time; 22, takes ages
     * The solution is to build a single row each time an entity is added / changed, store in a single property, and use just that to build
     *  the full spreadsheet. Should be instant, but will require additions to the code.
     *  Can derive each DTO from a base class with just this property, and it won't need to be set in the mobile DTO
     *  Could also implement an interface on each controller for this behaviour
     * */
    public static class SpreadsheetBuilder
    {





        internal static string DateTimeCell(DateTime date)
        {
            string d = $"{date.Year.ToString()}-{date.Month.ToString("D2")}-{date.Day.ToString("D2")}T{date.Hour.ToString("D2")}:{date.Minute.ToString("D2")}:{date.Second.ToString("D2")}";
            string s = "";
            s += $@"<table:table-cell office:value-type=""date"" office:date-value=""{d}"" calcext:value-type=""date"">";
            s += "<text:p>";
            s += date.ToString("G");
            s += "</text:p>";
            s += "</table:table-cell>";
            s += "\n\t\t";
            return s;
        }
      
        internal static string StringCell(string s)
        {
            return $@"<table:table-cell office:value-type=""string"" calcext:value-type=""string""><text:p>{s}</text:p></table:table-cell>";
        }
        
        

        internal static string NumberCell(double d)
        {
            string s = "";
            s += $@"<table:table-cell office:value-type=""float"" office:value=""{d}""  calcext:value-type=""float"">";
            s += "<text:p>";
            s += d.ToString();
            s += "</text:p>";
            s += "</table:table-cell>";
            s += "\n\t\t";
            return s;

        }

        internal static string BuildSpreadsheet(List<_Trade> trades)
        {
            string s = @"<?xml version=""1.0"" encoding=""UTF-8""?><office:document xmlns:meta=""urn:oasis:names:tc:opendocument:xmlns:meta:1.0"" xmlns:office=""urn:oasis:names:tc:opendocument:xmlns:office:1.0"" xmlns:fo=""urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"" xmlns:ooo=""http://openoffice.org/2004/office"" xmlns:xlink=""http://www.w3.org/1999/xlink"" xmlns:style=""urn:oasis:names:tc:opendocument:xmlns:style:1.0"" xmlns:config=""urn:oasis:names:tc:opendocument:xmlns:config:1.0"" xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns:text=""urn:oasis:names:tc:opendocument:xmlns:text:1.0"" xmlns:draw=""urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"" xmlns:dr3d=""urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0"" xmlns:svg=""urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0"" xmlns:chart=""urn:oasis:names:tc:opendocument:xmlns:chart:1.0"" xmlns:rpt=""http://openoffice.org/2005/report"" xmlns:table=""urn:oasis:names:tc:opendocument:xmlns:table:1.0"" xmlns:number=""urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0"" xmlns:ooow=""http://openoffice.org/2004/writer"" xmlns:oooc=""http://openoffice.org/2004/calc"" xmlns:of=""urn:oasis:names:tc:opendocument:xmlns:of:1.2"" xmlns:tableooo=""http://openoffice.org/2009/table"" xmlns:calcext=""urn:org:documentfoundation:names:experimental:calc:xmlns:calcext:1.0"" xmlns:drawooo=""http://openoffice.org/2010/draw"" xmlns:loext=""urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0"" xmlns:field=""urn:openoffice:names:experimental:ooo-ms-interop:xmlns:field:1.0"" xmlns:math=""http://www.w3.org/1998/Math/MathML"" xmlns:form=""urn:oasis:names:tc:opendocument:xmlns:form:1.0"" xmlns:script=""urn:oasis:names:tc:opendocument:xmlns:script:1.0"" xmlns:dom=""http://www.w3.org/2001/xml-events"" xmlns:xforms=""http://www.w3.org/2002/xforms"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:formx=""urn:openoffice:names:experimental:ooxml-odf-interop:xmlns:form:1.0"" xmlns:xhtml=""http://www.w3.org/1999/xhtml"" xmlns:grddl=""http://www.w3.org/2003/g/data-view#"" xmlns:css3t=""http://www.w3.org/TR/css3-text/"" xmlns:presentation=""urn:oasis:names:tc:opendocument:xmlns:presentation:1.0"" office:version=""1.3"" office:mimetype=""application/vnd.oasis.opendocument.spreadsheet""> <office:meta><meta:creation-date>2021-07-08T11:13:11.575000000</meta:creation-date><dc:date>2021-07-08T11:14:55.649000000</dc:date><meta:editing-duration>PT1M44S</meta:editing-duration><meta:editing-cycles>1</meta:editing-cycles><meta:generator>LibreOffice/7.1.1.2$Windows_X86_64 LibreOffice_project/fe0b08f4af1bacafe4c7ecc87ce55bb426164676</meta:generator><meta:document-statistic meta:table-count=""1"" meta:cell-count=""4"" meta:object-count=""0""/></office:meta><office:body>  <office:spreadsheet>   <table:table table:name=""Sheet1"">    <table:table-column />    <table:table-column />
";

            s += @"<table:table-row>";
            s += "\n\t\t";
            s += StringCell("Date");
            s += StringCell("Result");
            s += @"</table:table-row>";
            s += "\n";

            foreach (var trade in trades.Where(x => x.Action==TradeAction.ENTRY).OrderBy(x =>x.Timestamp))
            {
                s += GetSpreadsheetRow(trade.Timestamp, trade.Result); //result is on the entry as well as the close
            }

            s += "</table:table><table:named-expressions /></office:spreadsheet></office:body></office:document>";
            return s;
        }

        internal static string GetSpreadsheetRow(DateTime dateTime, double result)
        {
            string rowData = "";
            rowData += @"<table:table-row>";
            rowData += "\n\t\t";
            rowData += DateTimeCell(dateTime);
            rowData += NumberCell(result);
            rowData += @"</table:table-row>";
            rowData += "\n";
            return rowData;
        }


    }

        

}
 