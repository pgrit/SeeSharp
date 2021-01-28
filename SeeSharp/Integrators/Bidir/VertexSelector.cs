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
            int vertIdx = rng.NextInt(1, cache.Length(pathIdx));
            return (pathIdx, vertIdx);
        }

        public int Count => numVertices;

        void Prepare() {
            // Count the number of vertices we allow connecting to (aka the ones not on the emitter)
            for (int i = 0; i < cache.NumPaths; ++i) {
                numVertices += cache.Length(i) - 1;
            }
        }

        PathCache cache;
        int numVertices;
    }
}
