using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog.Layouts;
using Serilog;
using Serilog.Formatting;
using ZLogger;
using ZLogger.Providers;

namespace MicrosofLoggingPerformance
{
    class Program
    {
        static void Main(string[] args)
        {
            bool asyncLogging = true;
            bool useMessageTemplate = true;
            bool jsonLogging = false;
            int threadCount = 2;
            int messageCount = jsonLogging ? 5000000 : 5000000;
            int messageSize = 30;
            int messageArgCount = 2;

            const string BasePath = @"C:\Temp\MicrosoftPerformance\";

            NLog.Time.TimeSource.Current = new NLog.Time.AccurateUtcTimeSource();

            var fileTarget = new NLog.Targets.FileTarget
            {
                Name = "FileTarget",
                FileName = System.IO.Path.Combine(BasePath, asyncLogging ? "NLogAsync.txt" : "NLog.txt"),
                KeepFileOpen = true,
                AutoFlush = false,
                OpenFileFlushTimeout = 1,
            };

            if (jsonLogging)
            {
                fileTarget.Layout = new JsonLayout()
                {
                    Attributes = {
                        new JsonAttribute("@t", "${date:format=o}"),
                        new JsonAttribute("mt", "${message:raw=true}"),
                        new JsonAttribute("SourceContext", "${logger}"),
                        new JsonAttribute("Props", new JsonLayout() { IncludeEventProperties = true, IncludeScopeProperties = true}) {Encode  = false },
                    }
                };
            }

            var asyncFileTarget = new NLog.Targets.Wrappers.AsyncTargetWrapper(fileTarget)
            {
                TimeToSleepBetweenBatches = 0,
                OverflowAction = NLog.Targets.Wrappers.AsyncTargetWrapperOverflowAction.Block,
            };

            var benchmarkTool = new BenchmarkTool.BenchMarkExecutor(messageSize, messageArgCount, useMessageTemplate);
            var messageTemplate = benchmarkTool.MessageTemplates[0];

            if (asyncLogging)
            {
                Action<ZLoggerFileOptions> zLoggerOptions = (opt) =>
                {
                    opt.FullMode = BackgroundBufferFullMode.Block;
                    opt.IncludeScopes = true;
                    if (jsonLogging)
                    {
                        opt.UseJsonFormatter(fmt =>
                        {
                            fmt.UseUtcTimestamp = true;
                            fmt.IncludeProperties |= IncludeProperties.ScopeKeyValues;
                        });
                    }
                    else
                    {
                        opt.UsePlainTextFormatter(fmt =>
                        {
                            fmt.SetPrefixFormatter($"{0}|{1}|", (in MessageTemplate template, in LogInfo info) => template.Format(info.Timestamp, info.LogLevel));
                            fmt.SetSuffixFormatter($" ({0})", (in MessageTemplate template, in LogInfo info) => template.Format(info.Category));
                            fmt.SetExceptionFormatter((writer, ex) => Utf8StringInterpolation.Utf8String.Format(writer, $"{ex.Message}"));
                        });
                    }
                };
                var zloggerProvider = new ServiceCollection().AddLogging(cfg => cfg.AddZLoggerFile(System.IO.Path.Combine(BasePath, "ZLoggerAsync.txt"), zLoggerOptions)).BuildServiceProvider();
                var zLogger = zloggerProvider.GetService<ILogger<Program>>();
                Action<string, object[]> zlogggerMethod = GenerateLoggerMethod(jsonLogging, messageArgCount, messageTemplate, zLogger);
                Action zlogggerFlush = () =>
                {
                    zloggerProvider.Dispose();
                };
                benchmarkTool.ExecuteTest("ZLogger" + (jsonLogging ? " Json" : "") + (asyncLogging ? " Async" : ""), threadCount, messageCount, zlogggerMethod, zlogggerFlush);

                Console.WriteLine();
            }

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
            Action<string, object[]> nlogMethod = GenerateLoggerMethod(jsonLogging, messageArgCount, messageTemplate, nLogger);
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
                serilogConfig.WriteTo.File(serilogFormatter, System.IO.Path.Combine(BasePath, "Serilog.txt"), buffered: true, flushToDiskInterval: TimeSpan.FromMilliseconds(1000), fileSizeLimitBytes: null);
                Log.Logger = serilogConfig.CreateLogger();
            }
            else
            {
                serilogConfig.WriteTo.Async(a => a.File(serilogFormatter, System.IO.Path.Combine(BasePath, "SerilogAsync.txt"), buffered: true, flushToDiskInterval: TimeSpan.FromMilliseconds(1000), fileSizeLimitBytes: null), blockWhenFull: true);
                Log.Logger = serilogConfig.CreateLogger();
            }

            var serilogProvider = new ServiceCollection().AddLogging(cfg => cfg.AddSerilog()).BuildServiceProvider();
            var serilogLogger = serilogProvider.GetService<ILogger<Program>>();
            Action<string, object[]> serilogMethod = GenerateLoggerMethod(jsonLogging, messageArgCount, messageTemplate, serilogLogger);
            Action serilogFlushMethod = () =>
            {
                Log.CloseAndFlush();
                serilogProvider.Dispose();
            };
            benchmarkTool.ExecuteTest("Serilog" + (jsonLogging ? " Json" : "") + (asyncLogging ? " Async" : ""), threadCount, messageCount, serilogMethod, serilogFlushMethod);

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private static Action<string, object[]> GenerateLoggerMethod(bool jsonLogging, int messageArgCount, string messageTemplate, ILogger<Program> logger)
        {
            Action<string, object[]> loggerMethod = null;
            if (messageArgCount == 0)
            {
                loggerMethod = LoggerMessageDefineEmpty(logger, messageTemplate);
            }
            else if (messageArgCount == 1)
            {
                loggerMethod = LoggerMessageDefineOneArg(logger, messageTemplate);
            }
            else if (messageArgCount == 2)
            {
                loggerMethod = LoggerMessageDefineTwoArg(logger, messageTemplate, jsonLogging);
            }
            else if (messageArgCount == 3)
            {
                loggerMethod = LoggerMessageDefineThreeArg(logger, messageTemplate);
            }
            else
            {
                loggerMethod = (messageFormat, messageArgs) => {
                    logger.LogInformation(messageFormat, messageArgs);
                };
            }

            return loggerMethod;
        }

        private static Action<string, object[]> LoggerMessageDefineEmpty(ILogger<Program> logger, string messageTemplate)
        {
            var loggerTemplate = LoggerMessage.Define(LogLevel.Information, default(EventId), messageTemplate);
            Action<string, object[]> logMethod = (messageFormat, messageArgs) =>
            {
                loggerTemplate(logger, null);
            };
            return logMethod;
        }

        private static Action<string, object[]> LoggerMessageDefineOneArg(ILogger<Program> logger, string messageTemplate)
        {
            var loggerTemplate = LoggerMessage.Define<object>(LogLevel.Information, default(EventId), messageTemplate);
            Action<string, object[]> logMethod = (messageFormat, messageArgs) =>
            {
                loggerTemplate(logger, messageArgs[0], null);
            };
            return logMethod;
        }

        private static Action<string, object[]> LoggerMessageDefineTwoArg(ILogger<Program> logger, string messageTemplate, bool scopeLogging)
        {
            if (scopeLogging)
            {
                var scopeProperties = new[] { new System.Collections.Generic.KeyValuePair<string, object>("Planet", "Earth"), new System.Collections.Generic.KeyValuePair<string, object>("Galaxy", "Milkyway") };
                var loggerTemplate = LoggerMessage.Define<object, object>(LogLevel.Information, default(EventId), messageTemplate);
                Action<string, object[]> logMethod = (messageFormat, messageArgs) =>
                {
                    using (logger.BeginScope(scopeProperties))
                    {
                        loggerTemplate(logger, messageArgs[0], messageArgs[1], null);
                    }
                };
                return logMethod;
            }
            else
            {
                var loggerTemplate = LoggerMessage.Define<object, object>(LogLevel.Information, default(EventId), messageTemplate);
                Action<string, object[]> logMethod = (messageFormat, messageArgs) =>
                {
                    loggerTemplate(logger, messageArgs[0], messageArgs[1], null);
                };
                return logMethod;
            }
        }

        private static Action<string, object[]> LoggerMessageDefineThreeArg(ILogger<Program> logger, string messageTemplate)
        {
            var loggerTemplate = LoggerMessage.Define<object, object, object>(LogLevel.Information, default(EventId), messageTemplate);
            Action<string, object[]> logMethod = (messageFormat, messageArgs) =>
            {
                loggerTemplate(logger, messageArgs[0], messageArgs[1], messageArgs[2], null);
            };
            return logMethod;
        }
    }
}
