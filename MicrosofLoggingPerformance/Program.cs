using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog.Layouts;
using Serilog;
using Serilog.Formatting;

namespace MicrosofLoggingPerformance
{
    class Program
    {
        static void Main(string[] args)
        {
            bool asyncLogging = false;
            bool useMessageTemplate = true;
            bool jsonLogging = false;
            int threadCount = 1;
            int messageCount = asyncLogging ? 5000000 : 5000000;
            int messageSize = 30;
            int messageArgCount = 2;

            NLog.Time.TimeSource.Current = new NLog.Time.AccurateUtcTimeSource();

            var fileTarget = new NLog.Targets.FileTarget
            {
                Name = "FileTarget",
                FileName = System.IO.Path.Combine(@"C:\Temp\MicrosoftPerformance\", asyncLogging ? "NLogAsync.txt" : "NLog.txt"),
                KeepFileOpen = true,
                ConcurrentWrites = false,
                AutoFlush = false,
                OpenFileFlushTimeout = 1,
            };

            if (jsonLogging)
            {
                fileTarget.Layout = new JsonLayout()
                {
                    IncludeEventProperties = true,
                    IncludeScopeProperties = true,
                    SuppressSpaces = true,
                    Attributes = { 
                        new JsonAttribute("@t", "${date:format=o}"),
                        new JsonAttribute("mt", "${message:raw=true}"),
                        new JsonAttribute("SourceContext", "${logger}"),
                    }
                };
            }

            var asyncFileTarget = new NLog.Targets.Wrappers.AsyncTargetWrapper(fileTarget)
            {
                TimeToSleepBetweenBatches = 0,
                OverflowAction = NLog.Targets.Wrappers.AsyncTargetWrapperOverflowAction.Block,
                BatchSize = 500
            };

            var benchmarkTool = new BenchmarkTool.BenchMarkExecutor(messageSize, messageArgCount, useMessageTemplate);

            var nlogConfig = new NLog.Config.LoggingConfiguration();
            if (!asyncLogging)
            {
                nlogConfig.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, fileTarget);
            }
            else
            {
                nlogConfig.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, asyncFileTarget);
            }
            NLog.LogManager.Configuration = nlogConfig;

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
                nlogMethod = LoggerMessageDefineTwoArg(nLogger, messageTemplate, jsonLogging);
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
            benchmarkTool.ExecuteTest("NLog" + (jsonLogging ? " Json" : "") + (asyncLogging ? " Async" : ""), threadCount, messageCount, nlogMethod, nlogFlushMethod);

            Console.WriteLine();

            ITextFormatter serilogFormatter = jsonLogging ?
                new Serilog.Formatting.Compact.CompactJsonFormatter() :
                new Serilog.Formatting.Display.MessageTemplateTextFormatter("{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");

            var serilogConfig = new LoggerConfiguration().MinimumLevel.Debug();
            if (jsonLogging)
                serilogConfig.Enrich.FromLogContext();

            if (!asyncLogging)
            {
                serilogConfig.WriteTo.File(serilogFormatter, @"C:\Temp\MicrosoftPerformance\Serilog.txt", buffered: true, flushToDiskInterval: TimeSpan.FromMilliseconds(1000));
                Log.Logger = serilogConfig.CreateLogger();
            }
            else
            {
                serilogConfig.WriteTo.Async(a => a.File(serilogFormatter, @"C:\Temp\MicrosoftPerformance\SerilogAsync.txt", buffered: true, flushToDiskInterval: TimeSpan.FromMilliseconds(1000)), blockWhenFull: true);
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
                serilogMethod = LoggerMessageDefineTwoArg(serilogLogger, messageTemplate, jsonLogging);
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
            benchmarkTool.ExecuteTest("Serilog" + (jsonLogging ? " Json" : "") + (asyncLogging ? " Async" : ""), threadCount, messageCount, serilogMethod, serilogFlushMethod);

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

        private static Action<string, object[]> LoggerMessageDefineTwoArg(ILogger<Program> logger, string messageTemplate, bool scopeLogging)
        {
            if (scopeLogging)
            {
                var scopeProperties = new[] { new System.Collections.Generic.KeyValuePair<string, object>("Planet", "Earth"), new System.Collections.Generic.KeyValuePair<string, object>("Galaxy", "Milkyway") };
                var loggerTemplate = LoggerMessage.Define<object, object>(LogLevel.Information, default(EventId), messageTemplate);
                Action<string, object[]> nlogMethod = (messageFormat, messageArgs) =>
                {
                    using (logger.BeginScope(scopeProperties))
                        loggerTemplate(logger, messageArgs[0], messageArgs[1], null);
                };
                return nlogMethod;
            }
            else
            {
                var loggerTemplate = LoggerMessage.Define<object, object>(LogLevel.Information, default(EventId), messageTemplate);
                Action<string, object[]> nlogMethod = (messageFormat, messageArgs) =>
                {
                    loggerTemplate(logger, messageArgs[0], messageArgs[1], null);
                };
                return nlogMethod;
            }
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
