using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace BenchmarkTool
{
    public class BenchMarkExecutor
    {
        private readonly List<string> _messageTemplates;
        private readonly object[] _messageArgs;

        public List<string> MessageTemplates => _messageTemplates;

        public BenchMarkExecutor(int messageSize, int messageArgCount, bool useMessageTemplate)
        {
            if (messageArgCount == 0)
            {
                _messageTemplates = new List<string>(new[] { new string('X', messageSize) });
                _messageArgs = Array.Empty<object>();
            }
            else 
            {
                _messageTemplates = new List<string>();
                StringBuilder sb = new StringBuilder(messageSize);
                int argInterval = messageSize / messageArgCount;

                _messageArgs = new object[messageArgCount];
                for (int i = 0; i < _messageArgs.Length; ++i)
                {
                    if (i == 1)
                        _messageArgs[i] = 42;
                    //else if (i == 2)
                    //    _messageArgs[i] = StringComparison.InvariantCulture;
                    //else if (i == 3)
                    //    _messageArgs[i] = new { Id = 123, Name = "Tester", Age = 21, Culture = StringComparison.InvariantCulture };
                    else
                        _messageArgs[i] = i;
                }

                for (int i = 0; i < 200; ++i)
                {
                    int paramNumber = 0;
                    for (int j = 0; j < messageSize; ++j)
                    {
                        if ((j + i) % argInterval == 0 && paramNumber < messageArgCount)
                        {
                            sb.Append("{");
                            if (useMessageTemplate)
                            {
                                for (int k = 0; k < 24 - paramNumber; ++k)
                                    sb.Append((char)('A' + paramNumber + k));
                            }
                            else
                            {
                                sb.Append(paramNumber.ToString());
                            }
                            sb.Append("}");
                            ++paramNumber;
                        }
                        else
                        {
                            sb.Append('X');
                        }
                    }
                    _messageTemplates.Add(sb.ToString());
                    sb.Length = 0;
                }
            }
        }

        public void ExecuteTest(string testName, int threadCount, int messageCount, Action<string, object[]> logMethod, Action flushMethod)
        {
            var currentProcess = Process.GetCurrentProcess();
            if (Environment.ProcessorCount > 1)
            {
                if (threadCount <= 1)
                    currentProcess.PriorityClass = ProcessPriorityClass.High;
                else
                    currentProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
            }

#if NETCOREAPP2_0 || NETCOREAPP2_1 || NETCOREAPP2_2
            long threadAllocationTotal = 0;
#endif

            Action<object> threadAction = (state) =>
            {
                int threadMessageCount = (int)state;
#if NETCOREAPP2_0 || NETCOREAPP2_1 || NETCOREAPP2_2
                long allocatedBytesForCurrentThread = GC.GetAllocatedBytesForCurrentThread();
#endif
                for (int i = 0; i < threadMessageCount; i += _messageTemplates.Count)
                {
                    for (int j = 0; j < _messageTemplates.Count; ++j)
                    {
                        logMethod(_messageTemplates[j], _messageArgs);
                    }
                }
#if NETCOREAPP2_0 || NETCOREAPP2_1 || NETCOREAPP2_2
                System.Threading.Interlocked.Add(ref threadAllocationTotal, GC.GetAllocatedBytesForCurrentThread() - allocatedBytesForCurrentThread);
#endif
            };

            int warmUpCount = messageCount > 100000 * 2 ? 100000 : messageCount / 10;
            Console.WriteLine(string.Format("Executing warmup run... (.NET={0}, Platform={1}bit)", FileVersionInfo.GetVersionInfo(typeof(int).Assembly.Location).ProductVersion, IntPtr.Size * 8));
            RunTest(threadAction, 1, warmUpCount / _messageTemplates.Count);  // Warmup run

            GC.Collect(2, GCCollectionMode.Forced, true);

#if NETCOREAPP2_0 || NETCOREAPP2_1 || NETCOREAPP2_2
#else
            AppDomain.MonitoringIsEnabled = true;
#endif

            System.Threading.Thread.Sleep(2000); // Allow .NET runtime to do its background thing, before we start

            Console.WriteLine("Executing performance test...");
            Console.WriteLine("");
            Console.WriteLine("| Test Name        | Messages   | Size | Args | Threads |");
            Console.WriteLine("|------------------|------------|------|------|---------|");
            Console.WriteLine("| {0,-16} | {1,10:N0} | {2,4} | {3,4} | {4,7} |", testName, messageCount, _messageTemplates[0].Length, _messageArgs.Length, threadCount);
            Console.WriteLine("");

            Stopwatch stopWatch = new Stopwatch();

            int gc2count = GC.CollectionCount(2);
            int gc1count = GC.CollectionCount(1);
            int gc0count = GC.CollectionCount(0);
#if NETCOREAPP2_0 || NETCOREAPP2_1 || NETCOREAPP2_2
            threadAllocationTotal = 0;
#else
            long allocatedBytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
#endif

            TimeSpan cpuTimeBefore = currentProcess.TotalProcessorTime;

            int countPerThread = (int)((messageCount - 1) / (double)threadCount);
            int actualMessageCount = countPerThread * threadCount;

            stopWatch.Start();

            RunTest(threadAction, threadCount, countPerThread);  // Real performance run
            flushMethod();

            stopWatch.Stop();

            TimeSpan cpuTimeAfter = currentProcess.TotalProcessorTime;
#if NETCOREAPP2_0 || NETCOREAPP2_1 || NETCOREAPP2_2
            long deltaAllocatedBytes = threadAllocationTotal;
#else
            long deltaAllocatedBytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocatedBytes;
#endif

            // Show report message.
            var throughput = actualMessageCount / ((double)stopWatch.ElapsedTicks / Stopwatch.Frequency);
            Console.WriteLine("");
            Console.WriteLine("| Test Name        | Time (ms) | Msgs/sec  | GC2 | GC1 | GC0 | CPU (ms) | Alloc (MB) |");
            Console.WriteLine("|------------------|-----------|-----------|-----|-----|-----|----------|------------|");
            Console.WriteLine(
                string.Format("| {0,-16} | {1,9:N0} | {2,9:N0} | {3,3} | {4,3} | {5,3} | {6,8:N0} | {7,10:N1} |",
                testName,
                stopWatch.ElapsedMilliseconds,
                (long)throughput,
                GC.CollectionCount(2) - gc2count,
                GC.CollectionCount(1) - gc1count,
                GC.CollectionCount(0) - gc0count,
                (int)(cpuTimeAfter - cpuTimeBefore).TotalMilliseconds,
                deltaAllocatedBytes / 1024.0 / 1024.0));

            if (stopWatch.ElapsedMilliseconds < 5000)
                Console.WriteLine("!!! Test completed too quickly, to give useful numbers !!!");

            if (!Stopwatch.IsHighResolution)
                Console.WriteLine("!!! Stopwatch.IsHighResolution = False !!!");

#if DEBUG
            Console.WriteLine("!!! Using DEBUG build !!!");
#endif
        }

        private static void RunTest(Action<object> threadAction, int threadCount, object state)
        {
            try
            {
                if (threadCount <= 1)
                {
                    threadAction(state); // Do the testing without spinning up tasks
                }
                else
                {
                    // Create and start producer tasks.
                    var producers = new Task[threadCount];
                    for (var producerIndex = 0; producerIndex < threadCount; producerIndex++)
                    {
                        producers[producerIndex] = Task.Factory.StartNew(threadAction, state, TaskCreationOptions.LongRunning);
                    }

                    // Wait for producing complete.
                    Task.WaitAll(producers);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
