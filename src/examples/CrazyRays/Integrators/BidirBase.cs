using GroundWrapper;
using GroundWrapper.Geometry;
using GroundWrapper.Sampling;
using GroundWrapper.Shading;
using GroundWrapper.Shading.Emitters;
using Integrators.Common;
using System.Numerics;
using System.Threading.Tasks;

namespace Integrators {
    public abstract class BidirBase : Integrator {
        public int NumIterations = 2;
        public int NumLightPaths = 0;
        public int MaxDepth = 10;

        public uint BaseSeedLight = 0xC030114u;
        public uint BaseSeedCamera = 0x13C0FEFEu;

        public Scene scene;
        public PathCache pathCache;
        public int[] endpoints;

        /// <summary>
        /// Called for each light path, used to populate the path cache.
        /// </summary>
        /// <returns>
        /// The index of the last vertex along the path.
        /// </returns>
        public abstract int TraceLightPath(RNG rng, uint pathIndex);

        /// <summary>
        /// Called once per iteration after the light paths have been traced.
        /// Use this to create acceleration structures etc.
        /// </summary>
        public abstract void ProcessPathCache();

        /// <summary>
        /// Called once for each pixel per iteration. Expected to perform some sort of path tracing,
        /// possibly connecting vertices with those from the light path cache.
        /// </summary>
        /// <returns>The estimated pixel value.</returns>
        public abstract ColorRGB EstimatePixelValue(SurfacePoint cameraPoint, Vector2 filmPosition, Ray primaryRay,
                                                    float pdfFromCamera, ColorRGB initialWeight, RNG rng);

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

        public virtual Emitter SelectEmitterForBidir(RNG rng) {
            return scene.Emitters[0]; // TODO proper selection
        }

        public virtual Emitter SelectEmitterForNextEvent(RNG rng, Ray ray, SurfacePoint hit) {
            return scene.Emitters[0]; // TODO proper selection
        }

        public override void Render(Scene scene) {
            this.scene = scene;

            if (NumLightPaths <= 0) {
                NumLightPaths = scene.FrameBuffer.Width * scene.FrameBuffer.Height;
            }

            pathCache = new PathCache(NumLightPaths * MaxDepth);
            endpoints = new int[NumLightPaths];

            for (uint iter = 0; iter < NumIterations; ++iter) {
                TraceAllLightPaths(iter);
                ProcessPathCache();
                TraceAllCameraPaths(iter);
                pathCache.Clear();
            }
        }

        private void TraceAllLightPaths(uint iter) {
            Parallel.For(0, NumLightPaths, idx => {
                var seed = RNG.HashSeed(BaseSeedLight, (uint)idx, (uint)iter);
                var rng = new RNG(seed);
                endpoints[idx] = TraceLightPath(rng, (uint)idx);
            });
        }

        private void TraceAllCameraPaths(uint iter) {
            Parallel.For(0, scene.FrameBuffer.Height,
                row => {
                    for (uint col = 0; col < scene.FrameBuffer.Width; ++col) {
                        uint pixelIndex = (uint)(row * scene.FrameBuffer.Width + col);
                        var seed = RNG.HashSeed(BaseSeedCamera, pixelIndex, (uint)iter);
                        var rng = new RNG(seed);
                        RenderPixel((uint)row, col, rng);
                    }
                }
            );
        }

        private void RenderPixel(uint row, uint col, RNG rng) {
            // Sample a ray from the camera
            var offset = rng.NextFloat2D();
            var filmSample = new Vector2(col, row) + offset;
            Ray primaryRay = scene.Camera.GenerateRay(filmSample);

            // Compute the corresponding solid angle pdf (required for MIS)
            float pdfFromCamera = scene.Camera.SolidAngleToPixelJacobian(primaryRay.direction); // TODO this should be returned by Camera.Sample() which should replace GenerateRay() to follow conventions similar to the BSDF system
            var initialWeight = ColorRGB.White; // TODO this should be computed by the camera and returned by SampleCamera()
            var cameraPoint = new SurfacePoint {
                position = scene.Camera.Position,
                normal = scene.Camera.Direction
            };

            var value = EstimatePixelValue(cameraPoint, filmSample, primaryRay, pdfFromCamera, initialWeight, rng);
            value = value * (1.0f / NumIterations);

            // TODO we do nearest neighbor splatting manually here, to avoid numerical
            //      issues if the primary samples are almost 1 (400 + 0.99999999f = 401)
            scene.FrameBuffer.Splat((float)col, (float)row, value);
        }
    }
}
