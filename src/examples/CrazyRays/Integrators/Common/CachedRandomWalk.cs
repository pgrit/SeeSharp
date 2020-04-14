using GroundWrapper;

namespace Integrators.Common {
    public class CachedRandomWalk : RandomWalk {
        PathCache cache;
        public int lastId;

        public CachedRandomWalk(Scene scene, RNG rng, int maxDepth, PathCache cache) 
            : base(scene, rng, maxDepth) {
            this.cache = cache;
        }

        public override ColorRGB StartFromEmitter(EmitterSample emitterSample, ColorRGB initialWeight) {
            // Add the vertex on the light source
            lastId = cache.AddVertex(new PathVertex {
                point = emitterSample.surface.point,
                pdfFromAncestor = emitterSample.surface.jacobian,
                pdfToAncestor = 0.0f, // cannot continue beyond an end-point (guard value used to detect this more easily)
                weight = ColorRGB.Black, // the first known weight is that at the first hit point
                ancestorId = -1,
                depth = 0
            });
            return base.StartFromEmitter(emitterSample, initialWeight);
        }

        protected override ColorRGB OnHit(Ray ray, Hit hit, float pdfFromAncestor, float pdfToAncestor,
                                          ColorRGB throughput, int depth, GeometryTerms geometryTerms) {
            // Add the next vertex
            lastId = cache.AddVertex(new PathVertex {
                point = hit.point,
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
