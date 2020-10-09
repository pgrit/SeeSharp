using System.Numerics;
using System.Threading.Tasks;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Sampling;
using SeeSharp.Core.Shading;

namespace SeeSharp.Integrators.Wavefront {
    public struct BsdfQuery {
        public Vector3 OutDir;
        public Vector3 InDir;
        public SurfacePoint Hit;
        public bool IsOnLightSubpath;
        public RNG Rng;
        public bool IsActive;

        public static void Evaluate(BsdfQuery[] queries, ColorRGB[] results) {
            Parallel.For(0, queries.Length, i => {
                if (!queries[i].IsActive) return;
                results[i] = queries[i].Hit.Material.EvaluateWithCosine(queries[i].Hit, queries[i].OutDir,
                    queries[i].InDir, queries[i].IsOnLightSubpath);
            });
        }

        public static void ComputePdfs(BsdfQuery[] queries, float[] forward, float[] reverse = null) {
            Parallel.For(0, queries.Length, i => {
                if (!queries[i].IsActive) return;

                var (fwd, rev) = queries[i].Hit.Material.Pdf(queries[i].Hit, queries[i].OutDir,
                    queries[i].InDir, queries[i].IsOnLightSubpath);

                forward[i] = fwd;
                if (reverse != null)
                    reverse[i] = rev;
            });
        }

        public static void Sample(BsdfQuery[] queries, ColorRGB[] weights, float[] pdfsForward,
                                  Vector3[] directions, float[] pdfsReverse = null) {
            Parallel.For(0, queries.Length, i => {
                if (!queries[i].IsActive) return;

                var sample = queries[i].Hit.Material.Sample(queries[i].Hit, queries[i].OutDir,
                    queries[i].IsOnLightSubpath, queries[i].Rng.NextFloat2D());

                weights[i] = sample.weight;
                pdfsForward[i] = sample.pdf;
                directions[i] = sample.direction;
                if (pdfsReverse != null)
                    pdfsReverse[i] = sample.pdfReverse;
            });
        }
    }
}