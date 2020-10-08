using SeeSharp.Core.Sampling;
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
            int pathIdx = rng.NextInt(0, cache.NumPaths);
            int vertIdx = rng.NextInt(0, cache.Length(pathIdx));
            return (pathIdx, vertIdx);
        }//=> indices[rng.NextInt(0, indices.Count)];

        public int Count => indices.Count;

        void Prepare() {

        }

        PathCache cache;
        List<int> indices;
    }
}
