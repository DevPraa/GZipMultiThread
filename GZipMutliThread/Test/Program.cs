using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Test
{
    class Program
    {

        static void Main(string[] args)
        {
            GZipUtils.GZipUtils Zipper = new GZipUtils.GZipUtils();
            Zipper.CountThreads = (uint)Environment.ProcessorCount;
            Zipper.SizePart = (uint)Math.Pow(2, 20);
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            Zipper.Compress(@"F:\07. Тип string.mp4", @"F:\07. Тип string.GZP");
            Zipper.Decompress(@"F:\07. Тип string.GZP", @"F:\07. Тип string1.mp4");
            stopwatch.Stop();
            Console.WriteLine($"Time : {stopwatch.Elapsed}");



            Console.WriteLine("Press a key");
            Console.ReadKey();

        }

        
    }
}
