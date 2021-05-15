using System.Numerics;
using System.Threading;

namespace SeeSharp.Benchmark {
    class Program {
        static void Main(string[] args) {
            GenericMaterial_Sampling.QuickTest();
            GenericMaterial_Sampling.Benchmark();

            // Make sure tev has time to receive the last image
            Thread.Sleep(200);

            //VectorBench.BenchComputeBasisVectors(10000000);
        }
    }
}
