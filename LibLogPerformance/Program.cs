using System;
using LibLogPerformance.Logging;
using Serilog;

namespace LibLogPerformance
{
    class Program
    {
        static void Main(string[] args)
        {
            bool asyncLogging = false;
            bool useMessageTemplate = false;
            int threadCount = 1;
            int messageCount = asyncLogging ? 5000000 : 5000000;
            int messageSize = 30;
            int messageArgCount = 2;

            var fileTarget = new NLog.Targets.FileTarget
            {
                Name = "FileTarget",
                FileName = @"C:\Temp\LibLogPerformance\NLog.txt",
                KeepFileOpen = true,
                ConcurrentWrites = false,
                AutoFlush = false,
                OpenFileFlushTimeout = 1,
            };

            var asyncFileTarget = new NLog.Targets.Wrappers.AsyncTargetWrapper(fileTarget)
            {
                TimeToSleepBetweenBatches = 0,
                OverflowAction = NLog.Targets.Wrappers.AsyncTargetWrapperOverflowAction.Block,
                BatchSize = 500
            };

            var benchmarkTool = new BenchmarkTool.BenchMarkExecutor(messageSize, messageArgCount, useMessageTemplate);
            if (!asyncLogging)
            {
                var nlogConfig = new NLog.Config.LoggingConfiguration();
                nlogConfig.AddRuleForAllLevels(fileTarget);
                NLog.LogManager.Configuration = nlogConfig;
            }
            else
            {
                var nlogConfig = new NLog.Config.LoggingConfiguration();
                nlogConfig.AddRuleForAllLevels(asyncFileTarget);
                NLog.LogManager.Configuration = nlogConfig;
            }

            LogProvider.SetCurrentLogProvider(new Logging.LogProviders.NLogLogProvider());
            var nLogger = LogProvider.GetCurrentClassLogger();
            Action<string, object[]> nlogMethod = (messageFormat, messageArgs) =>
            {
                nLogger.Info(messageFormat, messageArgs);
            };
            Action nlogFlushMethod = () =>
            {
                NLog.LogManager.Shutdown();
            };
            benchmarkTool.ExecuteTest(asyncLogging ? "NLog Async" : "NLog", threadCount, messageCount, nlogMethod, nlogFlushMethod);

            if (!asyncLogging)
            {
                var serilogConfig = new LoggerConfiguration()
                    .WriteTo.File(@"C:\Temp\LibLogPerformance\Serilog.txt", buffered: true, flushToDiskInterval: TimeSpan.FromMilliseconds(1000));
                Log.Logger = serilogConfig.CreateLogger();
            }
            else
            {
                var serilogConfig = new LoggerConfiguration()
                    .WriteTo.Async(a => a.File(@"C:\Temp\LibLogPerformance\SerilogAsync.txt", buffered: true, flushToDiskInterval: TimeSpan.FromMilliseconds(1000)), blockWhenFull: true);
                Log.Logger = serilogConfig.CreateLogger();
            }

            LogProvider.SetCurrentLogProvider(new Logging.LogProviders.SerilogLogProvider());
            var seriLogger = LogProvider.GetCurrentClassLogger();
            Action<string, object[]> serilogMethod = (messageFormat, messageArgs) =>
            {
                seriLogger.Info(messageFormat, messageArgs);
            };
            Action serilogFlushMethod = () =>
            {
                Log.CloseAndFlush();
            };
            benchmarkTool.ExecuteTest(asyncLogging ? "Serilog Async" : "Serilog", threadCount, messageCount, serilogMethod, serilogFlushMethod);

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}
