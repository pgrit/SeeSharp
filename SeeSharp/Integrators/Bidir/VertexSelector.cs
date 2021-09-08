using SeeSharp.Sampling;
using SeeSharp.Integrators.Common;
using System.Collections.Generic;

namespace SeeSharp.Integrators.Bidir {
    /// <summary>
    /// Helper class to select random vertices from a path vertex cache.
    /// Ignores the first vertices of all light paths (the ones on the lights).
    /// </summary>
    public class VertexSelector {
        public VertexSelector(PathCache cache) {
            this.cache = cache;
            Prepare();
        }

        public (int, int) Select(RNG rng) {
            int idx = rng.NextInt(0, Count);
            var (pathIdx, vertIdx) = vertices[idx];
            return (pathIdx, vertIdx);
        }

        public int Count => vertices.Count;

        void Prepare() {
            vertices.Clear();
            for (int pathIdx = 0; pathIdx < cache.NumPaths; ++pathIdx) {
                int num = cache.Length(pathIdx);
                for (int vertIdx = 1; vertIdx < num; ++vertIdx) {
                    vertices.Add((pathIdx, vertIdx));
                }
            }
        }

        PathCache cache;
        List<(int, int)> vertices = new();
    }
}
