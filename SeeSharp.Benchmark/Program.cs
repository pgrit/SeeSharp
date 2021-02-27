using System.Numerics;
using SeeSharp.Datastructs;

namespace SeeSharp.Benchmark {
    class Program {
        static void Main(string[] args) {
            VectorBench.BenchComputeBasisVectors(10000000);
            // NearestNeighborBench<NearestNeighborTree>.Benchmark_10_Nearest(1, true, new NearestNeighborTree());
        }
    }
}
