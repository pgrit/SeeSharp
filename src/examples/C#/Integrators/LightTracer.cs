using System;
using System.Threading.Tasks;
using Ground;

namespace Experiments {

    public class LightTracer {
        public void Render(Scene scene) {
            for (int iter = 0; iter < NumIterations; ++iter) {
                PathCache pathCache = new PathCache(TotalPaths * MaxDepth);

                Parallel.For(0, TotalPaths, idx => {
                    var seed = RNG.HashSeed(BaseSeed, (uint)idx, (uint)iter);
                    var rng = new RNG(seed);
                    TraceLightPath(scene, rng, pathCache, idx);
                });

                Parallel.For(0, TotalPaths, idx => {
                    ConnectPathVerticesToCamera(scene, endpointIds[idx], pathCache);
                });

                pathCache.Clear();
            }
        }

        void ConnectPathVerticesToCamera(Scene scene, int vertexId, PathCache pathCache) {
            while (vertexId > 0) { // >0 because we do not connect to the light source directly
                var vertex = pathCache[vertexId];

                // Compute image plane location
                var (raster, isVisible) = scene.ProjectOntoFilm(vertex.point.position);
                if (!isVisible)
                    goto Next;

                // Trace shadow ray
                if (scene.IsOccluded(vertex.point, scene.CameraPosition))
                    goto Next;

                // Compute image contribution and splat
                scene.frameBuffer.Splat(raster.x, raster.y, ColorRGB.White * (1.0f / NumIterations));

            Next:
                vertexId = vertex.ancestorId;
            }
        }

        void TraceLightPath(Scene scene, RNG rng, PathCache pathCache, int pathIdx) {
            var emitter = SelectEmitter(scene, rng); // TODO once this is a proper selection: obtain and consider PDF

            var primaryPos = rng.NextFloat2D();
            var primaryDir = rng.NextFloat2D();
            var emitterSample = emitter.WrapPrimaryToRay(primaryPos, primaryDir);
            Ray ray = scene.SpawnRay(emitterSample.surface.point, emitterSample.direction);

            var radiance = emitter.ComputeEmission(emitterSample.surface.point, emitterSample.direction);

            float pdf = emitterSample.surface.jacobian * emitterSample.jacobian;
            var weight = radiance * (emitterSample.shadingCosine / pdf);

            var walker = new RandomWalk(scene, rng, pathCache, true, MaxDepth);
            var lastVertexId = walker.StartWalk(
                initialPoint: emitterSample.surface.point,
                surfaceAreaPdf: emitterSample.surface.jacobian,
                initialRay: ray,
                directionPdf: emitterSample.jacobian,
                initialWeight: weight);

            // keep track of the endpoint
            endpointIds[pathIdx] = lastVertexId;
        }

        Emitter SelectEmitter(Scene scene, RNG rng) {
            return scene.Emitters[0]; // TODO proper selection
        }

        const int TotalPaths = 512 * 512;
        const UInt32 BaseSeed = 0xC030114;
        const int MaxDepth = 3;
        const int NumIterations = 2;

        int[] endpointIds = new int[TotalPaths];
    }

}