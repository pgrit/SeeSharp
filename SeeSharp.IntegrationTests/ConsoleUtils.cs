using SeeSharp.Common;
using System;
using System.Threading;

namespace SeeSharp.IntegrationTests {
    static class ConsoleUtils {
        public static void TestProgressBar() {
            SeeSharp.Common.ProgressBar bar = new(100, 10);
            for (int i = 0; i < 100; ++i) {
                // Thread.Sleep(500);
                bar.ReportDone(1, 0.5f);

                if (i == 2)
                    Console.WriteLine("Hi there!");

                if (i == 4)
                    Console.Write("gimme your attention!");
            }
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
}