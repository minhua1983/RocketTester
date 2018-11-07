using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Configuration;

namespace RocketTester.ONS
{
    public class DebugUtil
    {
        static string _AliyunOnsIsDebugEnabled = ConfigurationManager.AppSettings["AliyunOnsIsDebugEnabled"] ?? "0";
        static object lockHelper = new object();

        public static void Debug(string message)
        {
            if (_AliyunOnsIsDebugEnabled == "1")
            {
                //#if DEBUG
                lock (lockHelper)
                {
                    using (StreamWriter writer = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "/ons.txt", true))
                    {
                        writer.WriteLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + message);
                    }
                }
                //#endif
            }
        }
    }
}
