using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using NLog.Extensions.Logging;

namespace MicrosofLoggingPerformance
{
    class Program
    {
        static void Main(string[] args)
        {
            bool asyncLogging = true;
            bool useMessageTemplate = true;
            int threadCount = 4;
            int messageCount = asyncLogging ? 5000000 : 5000000;
            int messageSize = 30;
            int messageArgCount = 2;

            var fileTarget = new NLog.Targets.FileTarget
            {
                Name = "FileTarget",
                FileName = @"C:\Temp\MicrosoftPerformance\NLog.txt",
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
                nlogConfig.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, fileTarget);
                NLog.LogManager.Configuration = nlogConfig;
            }
            else
            {
                var nlogConfig = new NLog.Config.LoggingConfiguration();
                nlogConfig.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, asyncFileTarget);
                NLog.LogManager.Configuration = nlogConfig;
            }

            var nlogProvider = new ServiceCollection().AddLogging(cfg => cfg.AddNLog()).BuildServiceProvider();
            var nLogger = nlogProvider.GetService<ILogger<Program>>();
            var messageTemplate = benchmarkTool.MessageTemplates[0];
            Action<string, object[]> nlogMethod = null;
            if (messageArgCount == 0)
            {
                nlogMethod = LoggerMessageDefineEmpty(nLogger, messageTemplate);
            }
            else if (messageArgCount == 1)
            {
                nlogMethod = LoggerMessageDefineOneArg(nLogger, messageTemplate);
            }
            else if (messageArgCount == 2)
            {
                nlogMethod = LoggerMessageDefineTwoArg(nLogger, messageTemplate);
            }
            else if (messageArgCount == 3)
            {
                nlogMethod = LoggerMessageDefineThreeArg(nLogger, messageTemplate);
            }
            else
            {
                nlogMethod = (messageFormat, messageArgs) => { nLogger.LogInformation(messageFormat, messageArgs); };
            }
            Action nlogFlushMethod = () =>
            {
                NLog.LogManager.Shutdown();
                nlogProvider.Dispose();
            };
            benchmarkTool.ExecuteTest(asyncLogging ? "NLog Async" : "NLog", threadCount, messageCount, nlogMethod, nlogFlushMethod);

            if (!asyncLogging)
            {
                var serilogConfig = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(@"C:\Temp\MicrosoftPerformance\Serilog.txt", buffered: true, flushToDiskInterval: TimeSpan.FromMilliseconds(1000));
                Log.Logger = serilogConfig.CreateLogger();
            }
            else
            {
                var serilogConfig = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Async(a => a.File(@"C:\Temp\MicrosoftPerformance\SerilogAsync.txt", buffered: true, flushToDiskInterval: TimeSpan.FromMilliseconds(1000)), blockWhenFull: true);
                Log.Logger = serilogConfig.CreateLogger();
            }

            var serilogProvider = new ServiceCollection().AddLogging(cfg => cfg.AddSerilog()).BuildServiceProvider();
            var serilogLogger = serilogProvider.GetService<ILogger<Program>>();
            Action<string, object[]> serilogMethod = null;
            if (messageArgCount == 0)
            {
                serilogMethod = LoggerMessageDefineEmpty(serilogLogger, messageTemplate);
            }
            else if (messageArgCount == 1)
            {
                serilogMethod = LoggerMessageDefineOneArg(serilogLogger, messageTemplate);
            }
            else if (messageArgCount == 2)
            {
                serilogMethod = LoggerMessageDefineTwoArg(serilogLogger, messageTemplate);
            }
            else if (messageArgCount == 3)
            {
                serilogMethod = LoggerMessageDefineThreeArg(serilogLogger, messageTemplate);
            }
            else
            {
                serilogMethod = (messageFormat, messageArgs) => { serilogLogger.LogInformation(messageFormat, messageArgs); };
            }
            Action serilogFlushMethod = () =>
            {
                Log.CloseAndFlush();
                serilogProvider.Dispose();
            };
            benchmarkTool.ExecuteTest(asyncLogging ? "Serilog Async" : "Serilog", threadCount, messageCount, serilogMethod, serilogFlushMethod);

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private static Action<string, object[]> LoggerMessageDefineEmpty(ILogger<Program> logger, string messageTemplate)
        {
            var loggerTemplate = LoggerMessage.Define(LogLevel.Information, default(EventId), messageTemplate);
            Action<string, object[]> nlogMethod = (messageFormat, messageArgs) =>
            {
                loggerTemplate(logger, null);
            };
            return nlogMethod;
        }

        private static Action<string, object[]> LoggerMessageDefineOneArg(ILogger<Program> logger, string messageTemplate)
        {
            var loggerTemplate = LoggerMessage.Define<object>(LogLevel.Information, default(EventId), messageTemplate);
            Action<string, object[]> nlogMethod = (messageFormat, messageArgs) =>
            {
                loggerTemplate(logger, messageArgs[0], null);
            };
            return nlogMethod;
        }

        private static Action<string, object[]> LoggerMessageDefineTwoArg(ILogger<Program> logger, string messageTemplate)
        {
            //var scopeProperties = new[] { new System.Collections.Generic.KeyValuePair<string, object>("Planet", "Earth"), new System.Collections.Generic.KeyValuePair<string, object>("Galaxy", "Milkyway") };
            //var loggerTemplate = LoggerMessage.Define<object, object>(LogLevel.Trace, default(EventId), messageTemplate);
            //Action<string, object[]> nlogMethod = (messageFormat, messageArgs) =>
            //{
            //    using (logger.BeginScope(scopeProperties))
            //        loggerTemplate(logger, messageArgs[0], messageArgs[1], null);
            //};
            //return nlogMethod;

            var loggerTemplate = LoggerMessage.Define<object, object>(LogLevel.Information, default(EventId), messageTemplate);
            Action<string, object[]> nlogMethod = (messageFormat, messageArgs) =>
            {
                loggerTemplate(logger, messageArgs[0], messageArgs[1], null);
            };
            return nlogMethod;
        }

        private static Action<string, object[]> LoggerMessageDefineThreeArg(ILogger<Program> logger, string messageTemplate)
        {
            var loggerTemplate = LoggerMessage.Define<object, object, object>(LogLevel.Information, default(EventId), messageTemplate);
            Action<string, object[]> nlogMethod = (messageFormat, messageArgs) =>
            {
                loggerTemplate(logger, messageArgs[0], messageArgs[1], messageArgs[2], null);
            };
            return nlogMethod;
        }
    }
}
