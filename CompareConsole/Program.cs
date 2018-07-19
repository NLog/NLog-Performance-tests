using System;
using System.Diagnostics;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Serilog;
using Serilog.Events;

namespace PerformanceTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("starting");
            var count = 1_000_000;
            StartNLog(count);
            StartSerilog(count);
            StartNLog(count);
            StartSerilog(count);
            Console.WriteLine("Press any key");
            Console.ReadLine();
        }

        private static void StartSerilog(int count)
        {
            var fileName = $"serilog-{DateTime.Now.Ticks}.log";
            var log = new LoggerConfiguration()
                .WriteTo.File(fileName, buffered: true, shared: false,
                    flushToDiskInterval: TimeSpan.FromMilliseconds(1000))
                .CreateLogger();

            Log.Logger = log;

            Console.WriteLine("start Serilog");

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < count; i++)
            {
              //  log.Write(LogEventLevel.Information, "message {0}", i);
                log.Write(LogEventLevel.Information, "message {A}", i);
            }

            stopwatch.Stop();

            Console.WriteLine("Serilog done. {0} sec, {1:N0} item/sec", stopwatch.Elapsed.TotalSeconds, count / stopwatch.Elapsed.TotalSeconds);
        }

        private static void StartNLog(int count)
        {
            var fileName = $"nlog-{DateTime.Now.Ticks}.log";
            var config = new LoggingConfiguration();
            var fileTarget = new FileTarget("file1")
            {
                FileName = fileName,
                KeepFileOpen = true, //400 -> 650K/s
                Layout = "${longdate} [${level}] ${message}",
                CleanupFileName = false,
            };
            var target = new BufferingTargetWrapper(fileTarget)
            {
                BufferSize = 1000,
                SlidingTimeout = false,
                FlushTimeout = 1000,
                Name = "BufferedTarget"

            };

            config.AddTarget("file", target);
            config.AddRuleForAllLevels(target);
            LogManager.Configuration = config;

            Console.WriteLine("start NLog");

            var logger = LogManager.GetLogger("logger1");
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < count; i++)
            {
               // logger.Info("message {0}", i);
                logger.Info("message {A}", i);
            }
            LogManager.Flush(TimeSpan.FromMinutes(5));
            stopwatch.Stop();

            Console.WriteLine("NLog done. {0} sec, {1:N0} item/sec", stopwatch.Elapsed.TotalSeconds, count / stopwatch.Elapsed.TotalSeconds);
        }
    }
}
