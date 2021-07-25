using SeeSharp.Geometry;
using SeeSharp.Sampling;
using SimpleImageIO;
using SeeSharp.Shading.Emitters;
using SeeSharp.Integrators.Common;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using TinyEmbree;

namespace SeeSharp.Integrators.Bidir {
    /// <summary>
    /// Samples a given number of light paths via random walks through a scene.
    /// The paths are stored in a <see cref="Common.PathCache"/>
    /// </summary>
    public class LightPathCache {
        /// <summary>
        /// The number of paths that should be traced in each iteration
        /// </summary>
        public int NumPaths;

        /// <summary>
        /// The maximum length of each path
        /// </summary>
        public int MaxDepth;

        /// <summary>
        /// Seed that is hashed with the iteration and path index to generate a random number sequence
        /// for each light path.
        /// </summary>
        public uint BaseSeed = 0xC030114u;

        /// <summary>
        /// The scene that is being rendered
        /// </summary>
        public Scene Scene { get; init; }

        /// <summary>
        /// The generated light paths in the current iteration
        /// </summary>
        public PathCache PathCache { get; set; }

        public virtual (Emitter, float) SelectLight(float primary) {
            if (primary < BackgroundProbability) {
                return (null, BackgroundProbability);
            } else {
                // Remap the primary sample and select an emitter in the scene
                primary = (primary - BackgroundProbability) / (1 - BackgroundProbability);
                float scaled = Scene.Emitters.Count * primary;
                int idx = Math.Clamp((int)scaled, 0, Scene.Emitters.Count - 1);
                var emitter = Scene.Emitters[idx];
                return (emitter, (1 - BackgroundProbability) / Scene.Emitters.Count);
            }
        }

        public virtual float SelectLightPmf(Emitter em) {
            if (em == null) { // background
                return BackgroundProbability;
            } else {
                return (1 - BackgroundProbability) / Scene.Emitters.Count;
            }
        }

        public virtual float BackgroundProbability
        => Scene.Background != null ? 1 / (1.0f + Scene.Emitters.Count) : 0;

        public virtual EmitterSample SampleEmitter(RNG rng, Emitter emitter) {
            var primaryPos = rng.NextFloat2D();
            var primaryDir = rng.NextFloat2D();
            return emitter.SampleRay(primaryPos, primaryDir);
        }

        public virtual float ComputeEmitterPdf(Emitter emitter, SurfacePoint point, Vector3 lightToSurface,
                                               float reversePdfJacobian) {
            float pdfEmit = emitter.PdfRay(point, lightToSurface);
            pdfEmit *= reversePdfJacobian;
            pdfEmit *= SelectLightPmf(emitter);
            return pdfEmit;
        }

        public virtual float ComputeBackgroundPdf(Vector3 from, Vector3 lightToSurface) {
            float pdfEmit = Scene.Background.RayPdf(from, lightToSurface);
            pdfEmit *= SelectLightPmf(null);
            return pdfEmit;
        }

        public virtual (Ray, RgbColor, float) SampleBackground(RNG rng) {
            // Sample a ray from the background towards the scene
            var primaryPos = rng.NextFloat2D();
            var primaryDir = rng.NextFloat2D();
            return Scene.Background.SampleRay(primaryPos, primaryDir);
        }

        /// <summary>
        /// Resets the path cache and populates it with a new set of light paths.
        /// </summary>
        /// <param name="iter">Index of the current iteration, used to seed the random number generator.</param>
        public void TraceAllPaths(uint iter, NextEventPdfCallback nextEventPdfCallback) {
            if (PathCache == null)
                PathCache = new PathCache(NumPaths, MaxDepth);
            else if (NumPaths != PathCache.NumPaths) {
                // The size of the path cache needs to change -> simply create a new one
                PathCache = new PathCache(NumPaths, MaxDepth);
            } else {
                PathCache.Clear();
            }

            Parallel.For(0, NumPaths, idx => {
                var rng = new RNG(BaseSeed, (uint)idx, iter);
                TraceLightPath(rng, nextEventPdfCallback, idx);
            });
        }

        public delegate float NextEventPdfCallback(PathVertex origin, PathVertex primary, Vector3 nextDirection);

        class NotifyingCachedWalk : CachedRandomWalk {
            public NextEventPdfCallback callback;
            public NotifyingCachedWalk(Scene scene, RNG rng, int maxDepth, PathCache cache, int pathIdx)
                : base(scene, rng, maxDepth, cache, pathIdx) {
            }

            protected override RgbColor OnHit(Ray ray, SurfacePoint hit, float pdfFromAncestor,
                                              RgbColor throughput, int depth, float toAncestorJacobian) {
                // Call the base first, so the vertex gets created
                Debug.Assert(pdfFromAncestor > 0);
                var weight = base.OnHit(ray, hit, pdfFromAncestor, throughput, depth, toAncestorJacobian);

                // The next event pdf is computed once the path has three vertices
                if (depth == 2 && callback != null) {
                    ref var vertex = ref cache[pathIdx, lastId];
                    var primary = cache[pathIdx, vertex.AncestorId];
                    var origin = cache[pathIdx, primary.AncestorId];
                    vertex.PdfNextEventAncestor = callback(origin, primary, ray.Direction);
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
        public virtual int TraceLightPath(RNG rng, NextEventPdfCallback nextEvtCallback, int idx) {
            // Select an emitter or the background
            var (emitter, prob) = SelectLight(rng.NextFloat());
            if (emitter != null)
                return TraceEmitterPath(rng, emitter, prob, nextEvtCallback, idx);
            else
                return TraceBackgroundPath(rng, prob, nextEvtCallback, idx);
        }

        /// <summary>
        /// Callback that is invoked for each vertex along a path
        /// </summary>
        /// <param name="pathIdx">Index of the full path in the cache</param>
        /// <param name="vertex">Reference to the vertex</param>
        /// <param name="ancestor">Reference to the vertex's ancestor</param>
        /// <param name="dirToAncestor">Normalized direction from the vertex to the ancestor</param>
        public delegate void ProcessVertex(int pathIdx, in PathVertex vertex, in PathVertex ancestor,
            Vector3 dirToAncestor);

        /// <summary>
        /// Utility function that iterates over a light path, starting on the end point, excluding the point on the light itself.
        /// </summary>
        public void ForEachVertex(int index, ProcessVertex func) {
            int n = PathCache?.Length(index) ?? 0;
            for (int i = 1; i < n; ++i) {
                var ancestor = PathCache[index, i-1];
                var vertex = PathCache[index, i];
                var dirToAncestor = Vector3.Normalize(ancestor.Point.Position - vertex.Point.Position);
                func(index, vertex, ancestor, dirToAncestor);
            }
        }

        int TraceEmitterPath(RNG rng, Emitter emitter, float selectProb,
                             NextEventPdfCallback nextEventPdfCallback, int idx) {
            var emitterSample = SampleEmitter(rng, emitter);

            // Account for the light selection probability in the MIS weights
            emitterSample.Pdf *= selectProb;

            // Perform a random walk through the scene, storing all vertices along the path
            var walker = new NotifyingCachedWalk(Scene, rng, MaxDepth, PathCache, idx);
            walker.callback = nextEventPdfCallback;
            walker.StartFromEmitter(emitterSample, emitterSample.Weight / selectProb);
            return walker.lastId;
        }

        int TraceBackgroundPath(RNG rng, float selectProb, NextEventPdfCallback nextEventPdfCallback, int idx) {
            var (ray, weight, pdf) = SampleBackground(rng);

            // Account for the light selection probability
            pdf *= selectProb;
            weight /= selectProb;

            if (pdf == 0) // Avoid NaNs
                return -1;

            Debug.Assert(float.IsFinite(weight.Average));

            // Perform a random walk through the scene, storing all vertices along the path
            var walker = new NotifyingCachedWalk(Scene, rng, MaxDepth, PathCache, idx);
            walker.callback = nextEventPdfCallback;
            walker.StartFromBackground(ray, weight, pdf);
            return walker.lastId;
        }
    }
}
