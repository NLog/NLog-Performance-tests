using System;
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

            Stopwatch sw = Stopwatch.StartNew();
            var count = 10000000;
            TestLoggerHierarchy(count);
            sw.Stop();
            Console.WriteLine("{2:N} messages. Time taken: {0:N}ms. {1:N} / sec", sw.Elapsed.TotalMilliseconds, 
                ((double)count / sw.Elapsed.TotalMilliseconds)*1000, count);
            Console.ReadKey();
        }



        private static void TestLoggerHierarchy(int count)
        {
            for (var i = 0; i < 1; i++)
            {
                //{

                //    string folderName = @"c:\temp\Log";
                //    string jobId = System.Guid.NewGuid().ToString();
                //    jobId = jobId + "\\MIPS";
                //    string pathString = System.IO.Path.Combine(folderName, jobId);
                //    System.IO.Directory.CreateDirectory(pathString);

                //    string fileName = "Mipslogger.log";
                //    pathString = System.IO.Path.Combine(pathString, fileName);
                //    using (System.IO.FileStream fs = System.IO.File.Create(pathString))
                //    {

                //    }
                //    using (System.IO.StreamWriter file =
                //   new System.IO.StreamWriter(pathString))
                //    {
                //        for (var line = 0; line < (1000000 + (i * 100)); line++)
                //          for (var line = 0; line < (1000000 + (1 * 100)); line++)
                //        {
                //            file.WriteLine("MIP : " + line);
                //        }
                //    }
                //}



                {
                    string jobId = System.Guid.NewGuid().ToString();
                    NLog.GlobalDiagnosticsContext.Set("jobId", jobId);

                    var mipLogger = LogManager.GetLogger("mipLogger");

                    mipLogger.Info("MIP Started");

                    for (var line = 0; line < (count + (i * 100)); line++)
                    {
                          mipLogger.Info("MIP : {0}" + line);
                         // mipLogger.Info(new LogEventInfo(LogLevel.Info, "mipLogger", "MIP : " + line));
                    }
                    LogManager.Flush();
                    mipLogger.Info("MIP Finished");
                }

            }
        }

    }
}
