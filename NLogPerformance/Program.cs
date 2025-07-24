using System;

namespace NLogPerformance
{
    static class Program
    {
        private static string _loggerName = "JsonLogger";
        private static int _messageCount = 10000000;
        private static int _threadCount = 1;
        private static int _messageSize = 30;
        private static int _messageArgCount = 0;

        static void Main(string[] args)
        {
            var usage = "Usage: LoggingPerformance.exe [LoggerName] [MessageCount] [ThreadCount] [MessageSize] [MessageArgCount]";
            if ((args.Length > 0))
            {
                if (string.IsNullOrEmpty(args[0]))
                {
                    Console.WriteLine(usage);
                    throw new ArgumentException("Invalid first argument! Logger-name as first application argument.");
                }
                _loggerName = args[0];
            }

            if ((args.Length > 1) && (!int.TryParse(args[1], out _messageCount)) || (_messageCount < 1))
            {
                Console.WriteLine(usage);
                throw new ArgumentException("Invalid second argument! Message-count as second application argument.");
            }
            if ((args.Length > 2) && (!int.TryParse(args[2], out _threadCount)) || (_threadCount < 1))
            {
                Console.WriteLine(usage);
                throw new ArgumentException("Invalid third argument! Thread-count as third application argument.");
            }
            if ((args.Length > 3) && (!int.TryParse(args[3], out _messageSize)) || (_messageSize < 1))
            {
                Console.WriteLine(usage);
                throw new ArgumentException("Invalid fourth argument! Message-size as fourth application argument.");
            }
            if ((args.Length > 4) && (!int.TryParse(args[5], out _messageArgCount)) || (_messageArgCount > 100))
            {
                Console.WriteLine(usage);
                throw new ArgumentException("Invalid sixth argument! Message-Argument-Count as sixth application argument.");
            }

            var logger = NLog.LogManager.GetLogger(_loggerName);
            if (!logger.IsInfoEnabled)
            {
                Console.WriteLine(usage);
                throw new ArgumentException(string.Format("Logger Name {0} doesn't match any logging rules", _loggerName));
            }
            Action<string, object[]> logMethod = (messageFormat, messageArgs) => 
            {
                logger.Info(messageFormat, messageArgs);
            };
            Action flushMethod = () =>
            {
                NLog.LogManager.Flush();
            };

            var benchmarkTool = new BenchmarkTool.BenchMarkExecutor(_messageSize, _messageArgCount, false);
            benchmarkTool.ExecuteTest(_loggerName, _threadCount, _messageCount, logMethod, flushMethod);

            if (args == null || args.Length == 0)
            {
                // Wait for user stop action.
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}