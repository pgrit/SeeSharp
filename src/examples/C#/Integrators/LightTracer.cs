using System;
using System.Threading.Tasks;
using Ground;

namespace Experiments {

    class LightTracer {
        public void Render(Scene scene) {
            PathCache pathCache = new PathCache(TotalPaths * MaxDepth);

            Parallel.For(0, TotalPaths, idx => {
                var seed = RNG.HashSeed(BaseSeed, (uint)idx, (uint)idx);
                var rng = new RNG(seed);
                TraceLightPath(scene, rng, pathCache);
            });
        }

        void TraceLightPath(Scene scene, RNG rng, PathCache pathCache) {
            var emitter = SelectEmitter(scene, rng); // TODO once this is a proper selection: obtain and consider PDF

            var primaryPos = rng.NextFloat2D();
            var emitterPosSample = emitter.WrapPrimaryToSurface(primaryPos.x, primaryPos.y);

            var primaryDir = rng.NextFloat2D();
            Ray ray = emitter.WrapPrimaryToRay(primaryPos, primaryDir);

            var walker = new RandomWalk(scene, rng, pathCache, true, MaxDepth);

            var lastVertexId = walker.StartWalk(
                initialPoint: emitterPosSample.point,
                surfaceAreaPdf: emitterPosSample.jacobian,
                initialRay: ray,
                directionPdf: directionPdf,
                initialWeight: weight);
        }

        Emitter SelectEmitter(Scene scene, RNG rng) {
            return scene.Emitters[0]; // TODO proper selection
        }

        const int TotalPaths = 512 * 512;
        const UInt32 BaseSeed = 0xC030114;
        const int MaxDepth = 3;
    }

}