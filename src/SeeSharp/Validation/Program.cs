using System.Collections.Generic;

namespace SeeSharp.Validation {
    class Program {
        static void Main(string[] args) {
            var allTests = new List<ValidationSceneFactory>() {
                new Validate_DirectIllum(),
                new Validate_SingleBounce(),
                new Validate_SingleBounceGlossy(),
                new Validate_MultiLight(),
                new Validate_GlossyLight(),
                new Validate_Environment(),

                // new Validate_RoughGlassesIndirect(),
                // new Validate_CornellBox(),
                // new Validate_ModernHall(),
                // new Validate_HomeOffice(),
                // new Validate_BananaRange(),
            };

            int benchmarkRuns = 1;
            List<List<long>> allTimings = new();
            foreach (var test in allTests) {
                var timings = Validator.Benchmark(test, benchmarkRuns);
                allTimings.Add(timings);
            }

            System.Console.Write("Average Timings: \n");
            foreach (var timings in allTimings) {
                foreach (long t in timings) {
                    System.Console.Write($"{t}ms, ");
                }
                System.Console.Write("\b \b\b \b\n");
            }
        }
    }
}
