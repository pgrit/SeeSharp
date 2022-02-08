using SeeSharp.Common;
using System;
using System.Threading;

namespace SeeSharp.IntegrationTests;

static class ConsoleUtils {
    public static void TestProgressBar() {
        SeeSharp.Common.ProgressBar bar = new(10);
        bar.Start(100);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 100; ++i) {
            Thread.Sleep(10);
            bar.ReportDone(1);

            if (i == 2)
                Console.WriteLine("Hi there!");

            if (i == 4)
                Console.Write("gimme your attention!");
        }
        Console.WriteLine($"actual time: {timer.ElapsedMilliseconds}");
    }

    public static void TestLogger() {
        Logger.Log("This is an info thing");
        Logger.Log("And now a WARNING", Verbosity.Warning);
        Logger.Log("And now a error message!!!!", Verbosity.Error);

        Logger.Verbosity = Verbosity.Warning;
        Logger.Log("This is an info thing");
        Logger.Log("And now a WARNING", Verbosity.Warning);
        Logger.Log("And now a error message!!!!", Verbosity.Error);

        Logger.Verbosity = Verbosity.Error;
        Logger.Log("This is an info thing");
        Logger.Log("And now a WARNING", Verbosity.Warning);
        Logger.Log("And now a error message!!!!", Verbosity.Error);
    }
}