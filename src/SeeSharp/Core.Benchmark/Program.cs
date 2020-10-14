using System;
using SeeSharp.Core.Datastructs;

namespace SeeSharp.Core.Benchmark {
    class Program {
        static void Main(string[] args) {
            NearestNeighborBench<NearestNeighborTree>.Benchmark_10_Nearest(10, false);
        }
    }
}
