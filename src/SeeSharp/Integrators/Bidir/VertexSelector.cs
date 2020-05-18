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

        public int Select(RNG rng) => indices[rng.NextInt(0, indices.Count)];

        public int Count => indices.Count;

        void Prepare() {
            indices = new List<int>(cache.Count);
            for (int i = 0; i < cache.Count; ++i) {
                if (cache[i].ancestorId != -1)
                    indices.Add(i);
            }
        }

        PathCache cache;
        List<int> indices;
    }
}
