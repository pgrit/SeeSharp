using System.Collections.Generic;

namespace SeeSharp.Validation {
    class Program {
        static void Main(string[] args) {
            var allTests = new List<ValidationSceneFactory>() {
                // new Validate_DirectIllum(),
                // new Validate_SingleBounce(),
                // new Validate_SingleBounceGlossy(),
                // new Validate_MultiLight(),
                // new Validate_GlossyLight(),
                // new Validate_Environment(),

                // new Validate_CornellBox(),
                new Validate_ModernHall(),
                // new Validate_HomeOffice(),
                // new Validate_BananaRange(),
            };

            int benchmarkRuns = 3;

            foreach (var test in allTests) {
                // Validator.Validate(test);
                var timings = Validator.Benchmark(test, benchmarkRuns);
                System.Console.Write("Average Timings: ");
                foreach (long t in timings) {
                    System.Console.Write($"{t}ms, ");
                }
                System.Console.Write("\b\b\n");
            }
        }
    }
}
