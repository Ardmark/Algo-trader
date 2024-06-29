using System;
using System.IO;
using System.Linq;

namespace AlgoTrader.Shared.Logging
{
    public static class LogBase
    {
        public static void ClearLog(string filename)
        {

            try
            {
                using (FileStream fs = new FileStream(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "../../Logs", filename), FileMode.Truncate))
                {
                    fs.SetLength(0);
                }
            }
#pragma warning disable CS0168
            catch (Exception e) 
            { 
            }
        }

        public static string WriteLine(string text, string filename, int maxLen = 1048576)
        {
            text += "\n";

            try
            {
                using (FileStream fs = new FileStream(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "../../Logs", filename), FileMode.OpenOrCreate))
                {
                    var len = fs.Length;

                    //get text as bytes
                    if (text.Length > maxLen)
                    {
                        text = text.Substring(0, maxLen);
                    }
                    byte[] bytes = System.Text.Encoding.ASCII.GetBytes(text);

                    int textLen = bytes.Length;

                    if (len + textLen >= maxLen)
                    {
                        //truncate existing to < maxLen
                        int skip = (int)(len - maxLen + textLen); //how many bytes to skip
                        if (skip > len) skip = (int)len;
                        byte[] current = new byte[maxLen];
                        fs.Seek(skip, SeekOrigin.Begin); //read from offset until end
                        fs.Read(current, 0, (int)(len - skip));
                        bytes.CopyTo(current, len - skip); //append new data
                        fs.Seek(0, SeekOrigin.Begin); //return to start of file
                        fs.SetLength(maxLen);
                        fs.Write(current, 0, current.Length);//write
                        return System.Text.Encoding.Default.GetString(current);
                    }
                    else
                    {
                        //create a new file each time, with the new data appended
                        byte[] current = new byte[textLen + len];
                        fs.Seek(0, SeekOrigin.Begin);
                        fs.Read(current, 0, (int)(len));
                        bytes.CopyTo(current, len);
                        fs.Seek(0, SeekOrigin.Begin); //return to start of file
                        fs.Write(current, 0, current.Length);//write
                        return System.Text.Encoding.Default.GetString(current);
                    }
                }
            }
            catch (Exception e) { 
                return "Logfile error " + e.ToString(); 
            }
        }

        internal static void StartSession()
        {
            var text = ($"==============================================================================\nStarting session at {DateTime.Now}");
            LogConnection.WriteLine(text);
        }
    }

    //static classes can only derive from object, so not using a base class
    public static class LogConnection
    {
        public static void WriteLine(string text)
        {
            LogBase.WriteLine(text, "LogConnection.txt");
            LogTrades.WriteLine(text);
        }

        internal static void WriteLineTimestamp(string text)
        {
            LogBase.WriteLine(string.Format("{0}: {1}", DateTime.Now.ToString(), text), "LogConnection.txt");
            LogTrades.WriteLine(text);
        }
    }

    public static class LogExceptions
    {
        public static void WriteLine(string text)
        {
            LogBase.WriteLine(text, "LogExceptions");
            LogTrades.WriteLine(text);
        }
    }

    public static class LogTrades
    {
        public static void WriteLine(string text)
        {
            var s = LogBase.WriteLine(text, "LogTrades.txt");
            MainObj.mainViewModel.DisplayLog(s);
        }

        public static void WriteLineOrder(string text)
        {
            var orderLog = MainObj.BACKTEST ? "LogOrdersBacktest.txt" : "LogOrders.txt";
            LogBase.WriteLine(text, orderLog);
            LogTrades.WriteLine(text);
        }
    }

    public static class LogBackups
    {
        public static void WriteLine(string text)
        {
            LogBase.WriteLine(text, "LogBackups.txt");
            LogTrades.WriteLine(text);

        }
    }
}
