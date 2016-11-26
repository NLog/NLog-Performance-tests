using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace NLogPerformance
{
    static class Program
    {
        private static int _messageCount = 10000000;
        private static int _threadCount = 1;
        private static int _messageSize = 16;
        private static int _loggerCount = 1;
        
        static void Main(string[] args)
        {
            var usage = "Usage: LoggingPerformance.exe [MessageCount] [ThreadCount] [MessageSize] [LoggerCount] [WaitForUserInteraction (true/false)]";
            if ((args.Length > 0) && (!int.TryParse(args[0], out _messageCount)) || (_messageCount < 1))
            {
                Console.WriteLine(usage);
                throw new ArgumentException("Invalid first argument! Message-count as first application argument.");
            }
            if ((args.Length > 1) && (!int.TryParse(args[1], out _threadCount)) || (_threadCount < 1))
            {
                Console.WriteLine(usage);
                throw new ArgumentException("Invalid second argument! Thread-count as second application argument.");
            }
            if ((args.Length > 2) && (!int.TryParse(args[2], out _messageSize)) || (_messageSize < 1))
            {
                Console.WriteLine(usage);
                throw new ArgumentException("Invalid third argument! Message-size as third application argument.");
            }
            if ((args.Length > 3) && (!int.TryParse(args[3], out _messageSize)) || (_loggerCount < 1))
            {
                Console.WriteLine(usage);
                throw new ArgumentException("Invalid fourth argument! Logger-count as fourth application argument.");
            }
            var waitForUserInteraction = true;
            if (args.Length > 4 && !(bool.TryParse(args[4], out waitForUserInteraction)))
            {
                Console.WriteLine(usage);
                throw new ArgumentException("Invalid 5th argument! waitForUserInteraction - true or false.");
            }

            Console.WriteLine("Start test with:");
            Console.WriteLine(" - {0} Messages (Size={1})", _messageCount, _messageSize);
            Console.WriteLine(" - {0} Threads", _threadCount);
            Console.WriteLine(" - {0} Loggers", _loggerCount);
            Console.WriteLine("");

            int loggerPerThread = Math.Max(_loggerCount / _threadCount, 1);
            int countPerThread = _messageCount / _threadCount / loggerPerThread;
            int actualMessageCount = countPerThread * _threadCount * loggerPerThread;

            var logger = LogManager.GetLogger("logger");

            StringBuilder sb = new StringBuilder(_messageSize);
            for (int i = 0; i < _messageSize; ++i)
                sb.Append('X');
            string logMessage = sb.ToString();

            Console.WriteLine("Executing warmup run...");
            RunTest(logger, logMessage, 1, 100000, 1);  // Warmup run

            var currentProcess = Process.GetCurrentProcess();

            GC.Collect(2, GCCollectionMode.Forced, true);
            int gc2count = GC.CollectionCount(2);
            int gc1count = GC.CollectionCount(1);
            int gc0count = GC.CollectionCount(0);

            Console.WriteLine("Executing performance test...");
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            TimeSpan cpuTimeBefore = currentProcess.TotalProcessorTime;

            RunTest(logger, logMessage, _threadCount, countPerThread, loggerPerThread);  // Real performance run

            stopWatch.Stop();

            TimeSpan cpuTimeAfter = currentProcess.TotalProcessorTime;
            long peakMemory = currentProcess.PeakWorkingSet64;

            // Show report message.
            Console.WriteLine("Written {0} values. Memory Usage={1:G3} MBytes", _messageCount, (double)GC.GetTotalMemory(false) / 1024.0 / 1024.0);
            var throughput = actualMessageCount / ((double)stopWatch.ElapsedTicks / Stopwatch.Frequency);
            Console.WriteLine("");
            Console.WriteLine("| Test Name  | Time (ms) | Msgs/sec  | GC2 | GC1 | GC0 | CPU (ms) | Mem (MB) |");
            Console.WriteLine("|------------|-----------|-----------|-----|-----|-----|----------|----------|");
            Console.WriteLine(
                string.Format("| My Test    | {0,9} | {1,9} | {2,3} | {3,3} | {4,3} | {5,8} | {6,8:G3} |",
                stopWatch.ElapsedMilliseconds,
                (long)throughput,
                GC.CollectionCount(2) - gc2count,
                GC.CollectionCount(1) - gc1count,
                GC.CollectionCount(0) - gc0count,
                (int)(cpuTimeAfter - cpuTimeBefore).TotalMilliseconds,
                peakMemory / 1024.0 / 1024.0));

            Console.WriteLine("");

            if (stopWatch.ElapsedMilliseconds < 5000)
                Console.WriteLine("!!! Test completed too quickly, to give useful numbers !!!");

            if (!Stopwatch.IsHighResolution)
                Console.WriteLine("!!! Stopwatch.IsHighResolution = False !!!");
#if DEBUG
            Console.WriteLine("!!! Using DEBUG build !!!");
#endif

            if (waitForUserInteraction)
            {
                // Wait for user stop action.
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        private static void RunTest(Logger logger, string logMessage, int threadCount, int messageCount, int loggerCount)
        {
            try
            {
                Action<object> producer = state =>
                {
                    Logger[] loggerArray = loggerCount <= 1 ? new Logger[] { logger } : new Logger[loggerCount];
                    if (loggerArray.Length > 1)
                    {
                        for (int i = 0; i < loggerArray.Length; ++i)
                            loggerArray[i] = LogManager.GetLogger(string.Format("Logger-{0}-{1}", System.Threading.Thread.CurrentThread.ManagedThreadId, i));
                    }

                    for (var i = 0; i < messageCount; i++)
                    {
                        for (int j = 0; j < loggerArray.Length; ++j)
                            loggerArray[j].Info(logMessage);
                    }
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