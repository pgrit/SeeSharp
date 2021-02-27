using SeeSharp.Sampling;
using System;
using System.Diagnostics;
using System.Numerics;

namespace SeeSharp.Benchmark {
    public class VectorBench {
        public static void BenchComputeBasisVectors(int numTrials) {
            Random rng = new(1337);
            Vector3 NextVector() => new (
                (float) rng.NextDouble(),
                (float) rng.NextDouble(),
                (float) rng.NextDouble());

            Vector3 avg = Vector3.Zero;
            
            Stopwatch stop = Stopwatch.StartNew();
            for (int i = 0; i < numTrials; ++i) {
                Vector3 tan, binorm;
                SampleWarp.ComputeBasisVectors(NextVector(), out tan, out binorm);
                avg += (tan + binorm) / numTrials * 0.5f;
            }
            Console.WriteLine($"Computing {numTrials} basis vectors took {stop.ElapsedMilliseconds}ms - {avg.Length()}");
        }
    }
}