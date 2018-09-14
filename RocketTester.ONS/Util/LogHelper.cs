﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace RocketTester.ONS.Util
{
    public class LogHelper
    {
        static object lockHelper = new object();
        public static void Log(string message)
        {
            lock (lockHelper)
            {
                using (StreamWriter writer = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "/tracker.txt", true))
                {
                    writer.WriteLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + message);
                }
            }
        }
    }
}