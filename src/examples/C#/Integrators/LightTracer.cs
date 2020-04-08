using System;
using System.Threading.Tasks;
using Ground;

namespace Experiments {

    public class LightTracer {
        public void Render(Scene scene) {
            PathCache pathCache = new PathCache(TotalPaths * MaxDepth);

            Parallel.For(0, TotalPaths, idx => {
                var seed = RNG.HashSeed(BaseSeed, (uint)idx, (uint)idx);
                var rng = new RNG(seed);
                TraceLightPath(scene, rng, pathCache);
            });

            Parallel.For(0, TotalPaths, idx => {
                ConnectPathVerticesToCamera(endpointIds[idx], pathCache);
            });

            pathCache.Clear();

            // TODO repeat for multiple iterations
        }

        void ConnectPathVerticesToCamera(int vertexId, PathCache pathCache)
        {
            var vertex = pathCache[vertexId];

            // Compute image plane location

            // Trace shadow ray

            // Compute image contribution and splat

            // Recurse: repeat with the ancestor unless it lies on the light source itself.
            if (vertex.ancestorId >= 0)
                ConnectPathVerticesToCamera(vertex.ancestorId, pathCache);
        }

        void TraceLightPath(Scene scene, RNG rng, PathCache pathCache) {
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
        }

        Emitter SelectEmitter(Scene scene, RNG rng) {
            return scene.Emitters[0]; // TODO proper selection
        }

        const int TotalPaths = 512 * 512;
        const UInt32 BaseSeed = 0xC030114;
        const int MaxDepth = 3;

        int[] endpointIds = new int[TotalPaths];
    }

}