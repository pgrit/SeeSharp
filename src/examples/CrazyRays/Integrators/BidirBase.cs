using GroundWrapper;
using System.Threading.Tasks;

namespace Integrators {
    public abstract class BidirBase : Integrator {
        public int NumIterations = 2;
        public int NumLightPaths = 0;
        public int MaxDepth = 10;

        public uint BaseSeedLight = 0xC030114u;
        public uint BaseSeedCamera = 0x13C0FEFEu;

        /// <summary>
        /// Called for each light path, used to populate the path cache.
        /// </summary>
        /// <returns>
        /// The index of the last vertex along the path.
        /// </returns>
        public abstract int TraceLightPath(Scene scene, RNG rng, PathCache pathCache, uint pathIndex);

        /// <summary>
        /// Called once per iteration after the light paths have been traced.
        /// Use this to create acceleration structures etc.
        /// </summary>
        public abstract void ProcessPathCache(Scene scene, PathCache pathCache, int[] endpoints);

        /// <summary>
        /// Called once for each pixel per iteration. Expected to perform some sort of path tracing,
        /// possibly connecting vertices with those from the light path cache.
        /// </summary>
        /// <returns>The estimated pixel value.</returns>
        public abstract ColorRGB EstimatePixelValue(Scene scene, PathCache pathCache, int[] endpoints, 
            Vector2 filmPosition, Ray primaryRay, RNG rng);

        protected delegate void ProcessVertex(PathVertex vertex, PathVertex ancestor, Vector3 dirToAncestor);

        /// <summary>
        /// Utility function that iterates over a light path, starting on the end point, excluding the point on the light itself.
        /// </summary>
        protected void ForEachVertex(PathCache pathCache, int endpoint, ProcessVertex func) {
            int vertexId = endpoint;
            while (pathCache[vertexId].ancestorId != -1) { // iterate over all vertices that have an ancestor
                var vertex = pathCache[vertexId];
                var ancestor = pathCache[vertex.ancestorId];
                var dirToAncestor = ancestor.point.position - vertex.point.position;

                func(vertex, ancestor, dirToAncestor);

                vertexId = vertex.ancestorId;
            }
        }

        protected virtual Emitter SelectEmitterForBidir(Scene scene, RNG rng) {
            return scene.Emitters[0]; // TODO proper selection
        }

        protected virtual Emitter SelectEmitterForNextEvent(Scene scene, RNG rng, Ray ray, Hit hit) {
            return scene.Emitters[0]; // TODO proper selection
        }

        public override void Render(Scene scene) {
            if (NumLightPaths <= 0) {
                NumLightPaths = scene.frameBuffer.width * scene.frameBuffer.height;
            }

            PathCache pathCache = new PathCache(NumLightPaths * MaxDepth);
            int[] endpointIds = new int[NumLightPaths];

            for (uint iter = 0; iter < NumIterations; ++iter) {
                TraceAllLightPaths(scene, pathCache, iter, endpointIds);
                ProcessPathCache(scene, pathCache, endpointIds);
                TraceAllCameraPaths(scene, pathCache, endpointIds, iter);
                pathCache.Clear();
            }
        }

        private void TraceAllLightPaths(Scene scene, PathCache pathCache, uint iter, int[] endpointIds) {
            Parallel.For(0, NumLightPaths, idx => {
                var seed = RNG.HashSeed(BaseSeedLight, (uint)idx, (uint)iter);
                var rng = new RNG(seed);
                endpointIds[idx] = TraceLightPath(scene, rng, pathCache, (uint)idx);
            });
        }

        private void TraceAllCameraPaths(Scene scene, PathCache pathCache, int[] endpoints, uint iter) {
            Parallel.For(0, scene.frameBuffer.height,
                row => {
                    for (uint col = 0; col < scene.frameBuffer.width; ++col) {
                        uint pixelIndex = (uint)(row * scene.frameBuffer.width + col);
                        var seed = RNG.HashSeed(BaseSeedCamera, pixelIndex, (uint)iter);
                        var rng = new RNG(seed);
                        RenderPixel(scene, pathCache, endpoints, (uint)row, col, rng);
                    }
                }
            );
        }

        private void RenderPixel(Scene scene, PathCache pathCache, int[] endpoints, uint row, uint col, RNG rng) {
            // Sample a ray from the camera
            float u = rng.NextFloat();
            float v = rng.NextFloat();
            (Ray primaryRay, Vector2 filmSample) = scene.SampleCamera(row, col, u, v);

            var value = EstimatePixelValue(scene, pathCache, endpoints, filmSample, primaryRay, rng);
            value = value * (1.0f / NumIterations);

            // TODO we do nearest neighbor splatting manually here, to avoid numerical
            //      issues if the primary samples are almost 1 (400 + 0.99999999f = 401)
            scene.frameBuffer.Splat((float)col, (float)row, value);
        }
    }
}
