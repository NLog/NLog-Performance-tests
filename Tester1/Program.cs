﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace PerformanceTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var count = 1000000;

            //gdc test (now disabled)
            string jobId = System.Guid.NewGuid().ToString();
            NLog.GlobalDiagnosticsContext.Set("jobId", jobId);

            //getting logger not part of the test
            var logger = LogManager.GetLogger("logger");
            Console.WriteLine("start test with {0:N0} messages", count);

            Stopwatch sw = Stopwatch.StartNew();
            var paralllel = 1;
            for (var i = 0; i < paralllel; i++)
            {
                WriteMessages(logger, count);
            }
            sw.Stop();

            Console.WriteLine("{2:N} messages. Time taken: {0:N}ms. {1:N} / sec", sw.Elapsed.TotalMilliseconds,
                ((double)count / sw.Elapsed.TotalMilliseconds) * 1000, count);
            Console.ReadKey();
        }

        private static void WriteMessages(Logger logger, int count)
        {
            logger.Info("Log Started");

            for (var line = 0; line < count; line++)
            {
                logger.Debug("Line : " + line);
                // mipLogger.Info(new LogEventInfo(LogLevel.Info, "mipLogger", "MIP : " + line));
            }
            LogManager.Flush();
            logger.Info("Log Finished");
        }
    }
}