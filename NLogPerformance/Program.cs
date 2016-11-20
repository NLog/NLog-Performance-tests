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

        static void Main(string[] args)
        {
            if ((args.Length >= 1) && (!int.TryParse(args[0], out _messageCount)) || (_messageCount < 1))
            {
                Console.WriteLine("Usage: LoggingPerformance.exe [MessageCount] [ThreadCount] [MessageSize]");
                throw new ArgumentException("Invalid first argument! Message-count as first application argument.");
            }
            if ((args.Length >= 2) && (!int.TryParse(args[1], out _threadCount)) || (_threadCount < 1))
            {
                Console.WriteLine("Usage: LoggingPerformance.exe [MessageCount] [ThreadCount] [MessageSize]");
                throw new ArgumentException("Invalid second argument! Thread-count as second application argument.");
            }
            if ((args.Length >= 3) && (!int.TryParse(args[2], out _messageSize)) || (_messageSize < 1))
            {
                Console.WriteLine("Usage: LoggingPerformance.exe [MessageCount] [ThreadCount] [MessageSize]");
                throw new ArgumentException("Invalid third argument! Message-size as third application argument.");
            }

            Console.WriteLine("Stopwatch.IsHighResolution = {0}", Stopwatch.IsHighResolution);
            Console.WriteLine("Start test with {0} loggers writing {1:N0} messages with size={2}", _threadCount, _messageCount, _messageSize);

            int countPerThread = _messageCount / _threadCount;

            var logger = LogManager.GetLogger("logger");

            StringBuilder sb = new StringBuilder(_messageSize);
            for (int i = 0; i < _messageSize; ++i)
                sb.Append('X');
            string logMessage = sb.ToString();

            Console.WriteLine("Executing warmup run...");
            RunTest(logger, logMessage, 1, 100000);  // Warmup run

            GC.Collect(2, GCCollectionMode.Forced, true);
            int gc2count = GC.CollectionCount(2);
            int gc1count = GC.CollectionCount(1);
            int gc0count = GC.CollectionCount(0);

            Console.WriteLine("Executing performance test...");
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            RunTest(logger, logMessage, _threadCount, countPerThread);  // Real performance run

            stopWatch.Stop();

            // Show report message.
            var throughput = _messageCount / ((double)stopWatch.ElapsedTicks / Stopwatch.Frequency);
            Console.WriteLine("Written {0} values in {1}ms (throughput = {2:F3} Msgs/sec)", _messageCount, stopWatch.ElapsedMilliseconds, throughput);
            Console.WriteLine("GC2 {0}", GC.CollectionCount(2) - gc2count);
            Console.WriteLine("GC1 {0}", GC.CollectionCount(1) - gc1count);
            Console.WriteLine("GC0 {0}", GC.CollectionCount(0) - gc0count);
            Console.WriteLine("Mem {0:G3} MByte", (double)GC.GetTotalMemory(false) / 1024.0 / 1024.0);

            if (stopWatch.ElapsedMilliseconds < 5000)
                Console.WriteLine("!!! Test completed too quickly, to give useful numbers !!!");
#if DEBUG
            Console.WriteLine("!!! Using DEBUG build !!!");
#endif
            // Wait for user stop action.
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static void RunTest(Logger logger, string logMessage, int threadCount, int messageCount)
        {
            try
            {
                Action<object> producer = state =>
                {
                    for (var i = 0; i < messageCount; i++)
                    {
                        // Different loggers will be used here for testing...
                        logger.Info(logMessage);
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