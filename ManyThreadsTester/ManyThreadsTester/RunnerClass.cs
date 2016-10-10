using System;

namespace NLogPerfTest
{
    public class RunnerClass
    {
        public static void Main1()
        {
            CpuUsage cpu = new CpuUsage();
            double usage = cpu.GetUsage();
            Console.WriteLine(usage.ToString());
        }
    }
}
