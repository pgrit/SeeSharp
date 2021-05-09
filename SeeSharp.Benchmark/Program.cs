using System.Numerics;

namespace SeeSharp.Benchmark {
    class Program {
        static void Main(string[] args) {
            VectorBench.BenchComputeBasisVectors(10000000);
        }
    }
}
