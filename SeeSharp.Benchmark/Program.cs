using System.Numerics;
using System.Threading;

namespace SeeSharp.Benchmark {
    class Program {
        static void Main(string[] args) {
            GenericMaterial_Sampling.QuickTest();
            GenericMaterial_Sampling.BenchPerformance();
            GenericMaterial_Sampling.Benchmark();

            //VectorBench.BenchComputeBasisVectors(10000000);
        }
    }
}
