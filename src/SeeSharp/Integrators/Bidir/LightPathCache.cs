using SeeSharp.Core;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Sampling;
using SeeSharp.Core.Shading;
using SeeSharp.Core.Shading.Emitters;
using SeeSharp.Integrators.Common;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace SeeSharp.Integrators.Bidir {
    /// <summary>
    /// Samples a given number of light paths via random walks through a scene.
    /// The paths are stored in a <see cref="PathCache"/>
    /// </summary>
    public class LightPathCache {
        // Parameters
        public int NumPaths;
        public int MaxDepth;
        public uint BaseSeed = 0xC030114u;

        // Scene specific data
        public Scene scene;

        // Outputs
        public PathCache pathCache;
        public int[] endpoints;

        public virtual (Emitter, float, float) SelectLight(float primary) {
            float scaled = scene.Emitters.Count * primary;
            int idx = Math.Clamp((int)scaled, 0, scene.Emitters.Count - 1);
            var emitter = scene.Emitters[idx];
            return (emitter, 1.0f / scene.Emitters.Count, scaled - idx);
        }

        public virtual float SelectLightPmf(Emitter em) {
            if (em == null) { // background
                return BackgroundProbability;
            } else {
                return 1.0f / scene.Emitters.Count * (1 - BackgroundProbability);
            }
        }

        public virtual float BackgroundProbability => 1 / (1.0f + scene.Emitters.Count);

        /// <summary>
        /// Resets the path cache and populates it with a new set of light paths.
        /// </summary>
        /// <param name="iter">Index of the current iteration, used to seed the random number generator.</param>
        public void TraceAllPaths(uint iter, OnHitCallback onHit) {
            if (pathCache == null)
                pathCache = new PathCache(MaxDepth * NumPaths);
            else
                pathCache.Clear();

            endpoints = new int[NumPaths];

            Parallel.For(0, NumPaths, idx => {
                var seed = RNG.HashSeed(BaseSeed, (uint)idx, iter);
                var rng = new RNG(seed);
                endpoints[idx] = TraceLightPath(rng, onHit);
            });
        }

        public delegate void OnHitCallback(ref PathVertex newVertex, PathVertex ancestor, Vector3 nextDirection);

        class NotifyingCachedWalk : CachedRandomWalk {
            public OnHitCallback callback;
            public NotifyingCachedWalk(Scene scene, RNG rng, int maxDepth, PathCache cache) 
                : base(scene, rng, maxDepth, cache) {
            }

            protected override ColorRGB OnHit(Ray ray, SurfacePoint hit, float pdfFromAncestor, float pdfToAncestor, 
                                              ColorRGB throughput, int depth, float toAncestorJacobian, Vector3 nextDirection) {
                var ancestor = cache[lastId];
                var weight = base.OnHit(ray, hit, pdfFromAncestor, pdfToAncestor, throughput, depth, toAncestorJacobian, nextDirection);
                callback(ref cache[lastId], ancestor, nextDirection);
                return weight;
            }
        }

        /// <summary>
        /// Called for each light path, used to populate the path cache.
        /// </summary>
        /// <returns>
        /// The index of the last vertex along the path.
        /// </returns>
        public virtual int TraceLightPath(RNG rng, OnHitCallback onHit) {
            // Select an emitter or the background
            float lightSelPrimary = rng.NextFloat();
            if (lightSelPrimary > BackgroundProbability) { // Sample from an emitter in the scene
                // Remap the primary sample
                lightSelPrimary = (lightSelPrimary - BackgroundProbability) / (1 - BackgroundProbability);
                return TraceEmitterPath(rng, lightSelPrimary, onHit);
            } else { // Sample from the background
                return TraceBackgroundPath(rng, onHit);
            }
        }

        public delegate void ProcessVertex(PathVertex vertex, PathVertex ancestor, Vector3 dirToAncestor);

        /// <summary>
        /// Utility function that iterates over a light path, starting on the end point, excluding the point on the light itself.
        /// </summary>
        public void ForEachVertex(int endpoint, ProcessVertex func) {
            if (endpoint < 0) return;

            int vertexId = endpoint;
            while (pathCache[vertexId].ancestorId != -1) { // iterate over all vertices that have an ancestor
                var vertex = pathCache[vertexId];
                var ancestor = pathCache[vertex.ancestorId];
                var dirToAncestor = ancestor.point.position - vertex.point.position;

                func(vertex, ancestor, dirToAncestor);

                vertexId = vertex.ancestorId;
            }
        }

        int TraceEmitterPath(RNG rng, float lightSelPrimary, OnHitCallback onHit) {
            var (emitter, selectProb, _) = SelectLight(lightSelPrimary);
            selectProb *= 1 - BackgroundProbability;

            // Sample a ray from the emitter
            var primaryPos = rng.NextFloat2D();
            var primaryDir = rng.NextFloat2D();
            var emitterSample = emitter.SampleRay(primaryPos, primaryDir); ;

            // Account for the light selection probability in the MIS weights
            emitterSample.pdf *= selectProb;

            // Perform a random walk through the scene, storing all vertices along the path
            var walker = new NotifyingCachedWalk(scene, rng, MaxDepth, pathCache);
            walker.callback = onHit;
            walker.StartFromEmitter(emitterSample, emitterSample.weight / selectProb);
            return walker.lastId;
        }

        int TraceBackgroundPath(RNG rng, OnHitCallback onHit) {
            // Sample a ray from the background towards the scene
            var primaryPos = rng.NextFloat2D();
            var primaryDir = rng.NextFloat2D();
            var (ray, weight, pdf) = scene.Background.SampleRay(primaryPos, primaryDir);

            // Account for the light selection probability
            pdf *= BackgroundProbability;
            weight /= BackgroundProbability;

            // Perform a random walk through the scene, storing all vertices along the path
            var walker = new NotifyingCachedWalk(scene, rng, MaxDepth, pathCache);
            walker.callback = onHit;
            walker.StartFromBackground(ray, weight, pdf);
            return walker.lastId;
        }
    }
}
