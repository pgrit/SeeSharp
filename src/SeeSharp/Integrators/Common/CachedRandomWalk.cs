using GroundWrapper;
using GroundWrapper.Geometry;
using GroundWrapper.Sampling;
using GroundWrapper.Shading;
using GroundWrapper.Shading.Emitters;
using System.Numerics;

namespace Integrators.Common {
    public class CachedRandomWalk : RandomWalk {
        public PathCache cache;
        public int lastId;

        public CachedRandomWalk(Scene scene, RNG rng, int maxDepth, PathCache cache)
            : base(scene, rng, maxDepth) {
            this.cache = cache;
        }

        public override ColorRGB StartFromEmitter(EmitterSample emitterSample, ColorRGB initialWeight) {
            // Add the vertex on the light source
            lastId = cache.AddVertex(new PathVertex {
                // TODO is any of these actually useful? Only the point right now, but only because we do not pre-compute
                //      the next event weight (which would be more efficient to begin with)
                point = emitterSample.point,
                pdfFromAncestor = 0.0f, // unused
                pdfToAncestor = 0.0f, // unused
                weight = ColorRGB.Black, // the first known weight is that at the first hit point
                ancestorId = -1,
                depth = 0
            });
            return base.StartFromEmitter(emitterSample, initialWeight);
        }

        protected override ColorRGB OnHit(Ray ray, SurfacePoint hit, float pdfFromAncestor, float pdfToAncestor,
                                          ColorRGB throughput, int depth, float toAncestorJacobian, Vector3 nextDirection) {
            // Add the next vertex
            lastId = cache.AddVertex(new PathVertex {
                point = hit,
                pdfFromAncestor = pdfFromAncestor,
                pdfToAncestor = pdfToAncestor,
                weight = throughput,
                ancestorId = lastId,
                depth = (byte)depth
            });
            return ColorRGB.Black;
        }
    }
}
