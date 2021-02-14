using System.Numerics;
using SeeSharp.Datastructs;

namespace SeeSharp.Benchmark {
    class Program {
        static void Main(string[] args) {
            NearestNeighborBench<NearestNeighborTree>.Benchmark_10_Nearest(1, true, new NearestNeighborTree());
        }
    }
}
