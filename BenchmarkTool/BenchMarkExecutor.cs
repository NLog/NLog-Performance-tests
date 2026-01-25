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

            long totalOverheadTicks = 0;
            long totalSleepTimeMs = 0;

            int threadModulus = ((int)(10000.0 / threadCount / _messageTemplates.Count)) * _messageTemplates.Count;

            Action<object> threadAction = (state) =>
            {
                int threadMessageCount = (int)state;
#if NETCOREAPP2_0 || NETCOREAPP2_1 || NETCOREAPP2_2
                long allocatedBytesForCurrentThread = GC.GetAllocatedBytesForCurrentThread();
#endif
                long overheadTicks = 0;
                long sleepTimeMs = 0;

                for (int i = 0; i < threadMessageCount; i += _messageTemplates.Count)
                {
                    var startTime = Stopwatch.GetTimestamp();
                    for (int j = 0; j < _messageTemplates.Count; ++j)
                    {
                        logMethod(_messageTemplates[j], _messageArgs);
                    }
                    overheadTicks += (Stopwatch.GetTimestamp() - startTime);
                    if ((i % threadModulus) == 0)
                    {
                        System.Threading.Thread.Sleep(1);  // Throttle to allow background threads to keep up (avoid testing buffer overflow performance)
                        sleepTimeMs += 1;
                    }
                }
#if NETCOREAPP2_0 || NETCOREAPP2_1 || NETCOREAPP2_2
                System.Threading.Interlocked.Add(ref threadAllocationTotal, GC.GetAllocatedBytesForCurrentThread() - allocatedBytesForCurrentThread);
#endif
                System.Threading.Interlocked.Add(ref totalOverheadTicks, overheadTicks);
                System.Threading.Interlocked.Add(ref totalSleepTimeMs, sleepTimeMs);
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

            totalOverheadTicks = 0;
            totalSleepTimeMs = 0;

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

            var elapsedTime = stopWatch.Elapsed;
            elapsedTime = elapsedTime.Add(TimeSpan.FromMilliseconds(-totalSleepTimeMs / (double)threadCount));

            TimeSpan cpuTimeAfter = currentProcess.TotalProcessorTime;
#if NETCOREAPP2_0 || NETCOREAPP2_1 || NETCOREAPP2_2
            long deltaAllocatedBytes = threadAllocationTotal;
#else
            long deltaAllocatedBytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocatedBytes;
#endif

            // Show report message.
            var throughput = actualMessageCount / elapsedTime.TotalSeconds;
            Console.WriteLine("");
            Console.WriteLine("| Test Name        | Time (ms) | Msgs/sec  | GC2 | GC1 | GC0 | CPU (ms) | Overhead | Alloc (MB) |");
            Console.WriteLine("|------------------|-----------|-----------|-----|-----|-----|----------|----------|------------|");
            Console.WriteLine(
                string.Format("| {0,-16} | {1,9:N0} | {2,9:N0} | {3,3} | {4,3} | {5,3} | {6,8:N0} | {7,8:N0} | {8,10:N1} |",
                testName,
                elapsedTime.TotalMilliseconds,
                (long)throughput,
                GC.CollectionCount(2) - gc2count,
                GC.CollectionCount(1) - gc1count,
                GC.CollectionCount(0) - gc0count,
                (int)(cpuTimeAfter - cpuTimeBefore).TotalMilliseconds,
                TimeSpan.FromTicks((long)(totalOverheadTicks * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency))).TotalMilliseconds,
                deltaAllocatedBytes / 1024.0 / 1024.0));

            if (elapsedTime.TotalMilliseconds < 5000)
                Console.WriteLine("!!! Test completed too quickly, to give useful numbers !!!");

            if (!Stopwatch.IsHighResolution)
                Console.WriteLine("!!! Stopwatch.IsHighResolution = False !!!");

            if (Environment.Is64BitOperatingSystem && IntPtr.Size != 8)
                Console.WriteLine("!!! Not running 64 bit !!!");

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
