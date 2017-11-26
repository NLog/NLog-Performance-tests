using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace NLogPerformance
{
    static class Program
    {
        private static string _loggerName = "Logger";
        private static int _messageCount = 10000000;
        private static int _threadCount = 1;
        private static int _messageSize = 16;
        private static int _messageArgCount = 0;
        private static int _loggerCount = 1;

        static void Main(string[] args)
        {
            var usage = "Usage: LoggingPerformance.exe [LoggerName] [MessageCount] [ThreadCount] [MessageSize] [LoggerCount] [MessageArgCount]";
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
            if ((args.Length > 4) && (!int.TryParse(args[4], out _loggerCount)) || (_loggerCount < 1))
            {
                Console.WriteLine(usage);
                throw new ArgumentException("Invalid fifth argument! Logger-count as fifth application argument.");
            }
            if ((args.Length > 5) && (!int.TryParse(args[5], out _messageArgCount)) || (_messageArgCount > 100))
            {
                Console.WriteLine(usage);
                throw new ArgumentException("Invalid sixth argument! Message-Argument-Count as sixth application argument.");
            }

            var logger = LogManager.GetLogger(_loggerName);
            if (!logger.IsInfoEnabled)
            {
                Console.WriteLine(usage);
                throw new ArgumentException(string.Format("Logger Name {0} doesn't match any logging rules", _loggerName));
            }
            List<Logger> loggers = new List<Logger>();
            loggers.Add(logger);
            for (int i = 1; i < _loggerCount; ++i)
            {
                logger = LogManager.GetLogger(string.Format("{0}-{1}", logger.Name, i));
                if (!logger.IsInfoEnabled)
                {
                    Console.WriteLine(usage);
                    throw new ArgumentException(string.Format("Logger Name {0} doesn't match any logging rules", logger.Name));
                }
                loggers.Add(logger);
            }

            List<string> logMessages = new List<string>();
            List<object> messageArgList = new List<object>();
            if (_messageArgCount > 0)
            {
                StringBuilder sb = new StringBuilder(_messageSize);
                int argInterval = _messageSize / _messageArgCount;

                for (int i = 0; i < 2000; ++i)
                {
                    int paramNumber = 0;
                    for (int j = 0; j < _messageSize; ++j)
                    {
                        if ((j + i) % argInterval == 0 && paramNumber < _messageArgCount)
                        {
                            sb.Append("{");
                            sb.Append(paramNumber.ToString());
                            //for (int k = 0; k < 25; ++k)
                            //    sb.Append((char)('A' + ((j + i + 1) / argInterval)));
                            sb.Append("}");
                            if (logMessages.Count == 0)
                                messageArgList.Add(new string(new[] { (char)('A' + paramNumber) }));
                            ++paramNumber;
                        }
                        else
                        {
                            sb.Append('X');
                        }
                    }
                    logMessages.Add(sb.ToString());
                    sb.Length = 0;
                }
            }
            else
            {
                StringBuilder sb = new StringBuilder(_messageSize);
                for (int i = 0; i < _messageSize; ++i)
                    sb.Append('X');
                logMessages.Add(sb.ToString());
            }

            var messageStrings = logMessages.ToArray();
            var messageArgs = messageArgList.Count == 0 ? null : messageArgList.ToArray();

            Action<int, int, string, object[]> nlogAction = (loggerIndex, level, msg, param) =>
            {
                NLogAction(loggers[loggerIndex], level, msg, param);
            };

            Action<int> nLogThread = (messageCount) =>
            {
                RunThreadTest(nlogAction, messageStrings, messageArgs, messageCount, loggers.Count);
            };

            Console.WriteLine(string.Format("Executing warmup run... (.NET={0}, Platform={1}bit)", FileVersionInfo.GetVersionInfo(typeof(int).Assembly.Location).ProductVersion, IntPtr.Size * 8));
            RunTest(() => { nLogThread(100000); }, 1);  // Warmup run

            var currentProcess = Process.GetCurrentProcess();
            if (_threadCount <= 1)
                currentProcess.PriorityClass = ProcessPriorityClass.High;
            else
                currentProcess.PriorityClass = ProcessPriorityClass.AboveNormal;

            GC.Collect(2, GCCollectionMode.Forced, true);

            AppDomain.MonitoringIsEnabled = true;

            System.Threading.Thread.Sleep(2000); // Allow .NET runtime to do its background thing, before we start

            Console.WriteLine("Executing performance test...");
            Console.WriteLine("");
            Console.WriteLine("| Logger Name      | Messages   | Size | Args | Threads | Loggers |");
            Console.WriteLine("|------------------|------------|------|------|---------|---------|");
            Console.WriteLine("| {0,-16} | {1,10:N0} | {2,4} | {3,4} | {4,7} | {5,7} |", _loggerName, _messageCount, _messageSize, _messageArgCount, _threadCount, _loggerCount);
            Console.WriteLine("");

            int gc2count = GC.CollectionCount(2);
            int gc1count = GC.CollectionCount(1);
            int gc0count = GC.CollectionCount(0);
            long allocatedBytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            TimeSpan cpuTimeBefore = currentProcess.TotalProcessorTime;

            int countPerThread = (int)((_messageCount - 1) / (double)_threadCount);
            int actualMessageCount = countPerThread * _threadCount;
            RunTest(() => { nLogThread(countPerThread); }, _threadCount);  // Real performance run

            stopWatch.Stop();

            TimeSpan cpuTimeAfter = currentProcess.TotalProcessorTime;
            long deltaAllocatedBytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocatedBytes;

            // Show report message.
            var throughput = actualMessageCount / ((double)stopWatch.ElapsedTicks / Stopwatch.Frequency);
            Console.WriteLine("");
            Console.WriteLine("| Test Name  | Time (ms) | Msgs/sec  | GC2 | GC1 | GC0 | CPU (ms) | Alloc (MB) |");
            Console.WriteLine("|------------|-----------|-----------|-----|-----|-----|----------|------------|");
            Console.WriteLine(
                string.Format("| My Test    | {0,9:N0} | {1,9:N0} | {2,3} | {3,3} | {4,3} | {5,8:N0} | {6,10:N1} |",
                stopWatch.ElapsedMilliseconds,
                (long)throughput,
                GC.CollectionCount(2) - gc2count,
                GC.CollectionCount(1) - gc1count,
                GC.CollectionCount(0) - gc0count,
                (int)(cpuTimeAfter - cpuTimeBefore).TotalMilliseconds,
                deltaAllocatedBytes / 1024.0 / 1024.0));

            Console.WriteLine("");

            if (stopWatch.ElapsedMilliseconds < 5000)
                Console.WriteLine("!!! Test completed too quickly, to give useful numbers !!!");

            if (!Stopwatch.IsHighResolution)
                Console.WriteLine("!!! Stopwatch.IsHighResolution = False !!!");
#if DEBUG
            Console.WriteLine("!!! Using DEBUG build !!!");
#endif
            if (args == null || args.Length == 0)
            {
                // Wait for user stop action.
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        private static void NLogAction(NLog.Logger logger, int level, string messageTemplate, object[] args)
        {
            logger.Log(NLog.LogLevel.FromOrdinal(level), messageTemplate, args);
        }

        private static void RunThreadTest(Action<int, int, string, object[]> logAction, string[] logMessages, object[] args, int messageCount, int loggerCount)
        {
            Random seed = new Random((int)DateTime.UtcNow.Ticks);
            if (logMessages.Length > 1)
            {
                for (int i = 0; i < messageCount; ++i)
                {
                    var logIndex = seed.Next(logMessages.Length);
                    var logLevel = seed.Next(5 + 1);
                    var loggerIndex = loggerCount > 1 ? seed.Next(loggerCount) : 0;
                    logAction(loggerIndex, logLevel, logMessages[logIndex], args);
                }
            }
            else
            {
                for (int i = 0; i < messageCount; ++i)
                {
                    var loggerIndex = loggerCount > 1 ? seed.Next(loggerCount) : 0;
                    logAction(loggerIndex, 3, logMessages[0], args);
                }
            }
        }

        private static void RunTest(Action threadAction, int threadCount)
        {
            try
            {
                Action<object> producer = state =>
                {
                    threadAction();
                };

                if (threadCount <= 1)
                {
                    producer(null); // Do the testing without spinning up tasks
                }
                else
                {
                    // Create and start producer tasks.
                    var producers = new Task[threadCount];
                    for (var producerIndex = 0; producerIndex < threadCount; producerIndex++)
                    {
                        producers[producerIndex] = Task.Factory.StartNew(producer, producerIndex, TaskCreationOptions.LongRunning);
                    }

                    // Wait for producing complete.
                    Task.WaitAll(producers);
                }

                LogManager.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}