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
    /// The paths are stored in a <see cref="Common.PathCache"/>
    /// </summary>
    public class LightPathCache {
        // Parameters
        public int NumPaths;
        public int MaxDepth;
        public uint BaseSeed = 0xC030114u;

        // Scene specific data
        public Scene Scene;

        // Outputs
        public PathCache PathCache;
        public int[] Endpoints;

        public virtual (Emitter, float, float) SelectLight(float primary) {
            float scaled = Scene.Emitters.Count * primary;
            int idx = Math.Clamp((int)scaled, 0, Scene.Emitters.Count - 1);
            var emitter = Scene.Emitters[idx];
            return (emitter, 1.0f / Scene.Emitters.Count, scaled - idx);
        }

        public virtual float SelectLightPmf(Emitter em) {
            if (em == null) { // background
                return BackgroundProbability;
            } else {
                return 1.0f / Scene.Emitters.Count * (1 - BackgroundProbability);
            }
        }

        public virtual float BackgroundProbability
            => Scene.Background != null ? 1 / (1.0f + Scene.Emitters.Count) : 0;

        /// <summary>
        /// Resets the path cache and populates it with a new set of light paths.
        /// </summary>
        /// <param name="iter">Index of the current iteration, used to seed the random number generator.</param>
        public void TraceAllPaths(uint iter, NextEventPdfCallback nextEventPdfCallback) {
            if (PathCache == null)
                PathCache = new PathCache(MaxDepth * NumPaths);
            else
                PathCache.Clear();

            Endpoints = new int[NumPaths];

            Parallel.For(0, NumPaths, idx => {
                var seed = RNG.HashSeed(BaseSeed, (uint)idx, iter);
                var rng = new RNG(seed);
                Endpoints[idx] = TraceLightPath(rng, nextEventPdfCallback);
            });
        }

        public delegate float NextEventPdfCallback(PathVertex origin, PathVertex primary, Vector3 nextDirection);

        class NotifyingCachedWalk : CachedRandomWalk {
            public NextEventPdfCallback callback;
            public NotifyingCachedWalk(Scene scene, RNG rng, int maxDepth, PathCache cache)
                : base(scene, rng, maxDepth, cache) {
            }

            protected override ColorRGB OnHit(Ray ray, SurfacePoint hit, float pdfFromAncestor,
                                              ColorRGB throughput, int depth, float toAncestorJacobian) {
                // Call the base first, so the vertex gets created
                var weight = base.OnHit(ray, hit, pdfFromAncestor, throughput, depth, toAncestorJacobian);

                // The next event pdf is computed once the path has three vertices
                if (depth == 2 && callback != null) {
                    var vertex = cache[lastId];
                    var primary = cache[vertex.AncestorId];
                    var origin = cache[primary.AncestorId];
                    vertex.PdfReverseAncestor += callback(origin, primary, ray.Direction);
                }

                return weight;
            }
        }

        /// <summary>
        /// Called for each light path, used to populate the path cache.
        /// </summary>
        /// <returns>
        /// The index of the last vertex along the path.
        /// </returns>
        public virtual int TraceLightPath(RNG rng, NextEventPdfCallback onHit) {
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
            while (PathCache[vertexId].AncestorId != -1) { // iterate over all vertices that have an ancestor
                var vertex = PathCache[vertexId];
                var ancestor = PathCache[vertex.AncestorId];
                var dirToAncestor = ancestor.Point.Position - vertex.Point.Position;

                func(vertex, ancestor, dirToAncestor);

                vertexId = vertex.AncestorId;
            }
        }

        int TraceEmitterPath(RNG rng, float lightSelPrimary, NextEventPdfCallback nextEventPdfCallback) {
            var (emitter, selectProb, _) = SelectLight(lightSelPrimary);
            selectProb *= 1 - BackgroundProbability;

            // Sample a ray from the emitter
            var primaryPos = rng.NextFloat2D();
            var primaryDir = rng.NextFloat2D();
            var emitterSample = emitter.SampleRay(primaryPos, primaryDir); ;

            // Account for the light selection probability in the MIS weights
            emitterSample.pdf *= selectProb;

            // Perform a random walk through the scene, storing all vertices along the path
            var walker = new NotifyingCachedWalk(Scene, rng, MaxDepth, PathCache);
            walker.callback = nextEventPdfCallback;
            walker.StartFromEmitter(emitterSample, emitterSample.weight / selectProb);
            return walker.lastId;
        }

        int TraceBackgroundPath(RNG rng, NextEventPdfCallback nextEventPdfCallback) {
            // Sample a ray from the background towards the scene
            var primaryPos = rng.NextFloat2D();
            var primaryDir = rng.NextFloat2D();
            var (ray, weight, pdf) = Scene.Background.SampleRay(primaryPos, primaryDir);

            // Account for the light selection probability
            pdf *= BackgroundProbability;
            weight /= BackgroundProbability;

            // Perform a random walk through the scene, storing all vertices along the path
            var walker = new NotifyingCachedWalk(Scene, rng, MaxDepth, PathCache);
            walker.callback = nextEventPdfCallback;
            walker.StartFromBackground(ray, weight, pdf);
            return walker.lastId;
        }
    }
}
