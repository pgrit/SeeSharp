using SeeSharp;
using SeeSharp.Geometry;
using SeeSharp.Sampling;
using SeeSharp.Shading;
using SeeSharp.Shading.Emitters;
using TinyEmbree;

namespace SeeSharp.Integrators.Common {
    public class CachedRandomWalk : RandomWalk {
        public PathCache cache;
        public int lastId;
        protected int pathIdx;

        float nextReversePdf = 0.0f;

        public CachedRandomWalk(Scene scene, RNG rng, int maxDepth, PathCache cache, int pathIdx)
            : base(scene, rng, maxDepth) {
            this.cache = cache;
            this.pathIdx = pathIdx;
        }

        public override ColorRGB StartFromEmitter(EmitterSample emitterSample, ColorRGB initialWeight) {
            nextReversePdf = 0.0f;
            // Add the vertex on the light source
            lastId = cache.AddVertex(new PathVertex {
                // TODO are any of these actually useful? Only the point right now, but only because we do not pre-compute
                //      the next event weight (which would be more efficient to begin with)
                Point = emitterSample.Point,
                PdfFromAncestor = 0.0f, // unused
                PdfReverseAncestor = 0.0f, // unused
                Weight = ColorRGB.Black, // the first known weight is that at the first hit point
                AncestorId = -1,
                Depth = 0
            }, pathIdx);
            return base.StartFromEmitter(emitterSample, initialWeight);
        }

        public override ColorRGB StartFromBackground(Ray ray, ColorRGB initialWeight, float pdf) {
            nextReversePdf = 0.0f;
            // Add the vertex on the light source
            lastId = cache.AddVertex(new PathVertex {
                // TODO are any of these actually useful? Only the point right now, but only because we do not pre-compute
                //      the next event weight (which would be more efficient to begin with)
                Point = new SurfacePoint { Position = ray.Origin },
                PdfFromAncestor = 0.0f, // unused
                PdfReverseAncestor = 0.0f, // unused
                Weight = ColorRGB.Black, // the first known weight is that at the first hit point
                AncestorId = -1,
                Depth = 0
            }, pathIdx);
            return base.StartFromBackground(ray, initialWeight, pdf);
        }

        protected override ColorRGB OnHit(Ray ray, SurfacePoint hit, float pdfFromAncestor, ColorRGB throughput,
                                          int depth, float toAncestorJacobian) {
            // Add the next vertex
            lastId = cache.AddVertex(new PathVertex {
                Point = hit,
                PdfFromAncestor = pdfFromAncestor,
                PdfReverseAncestor = nextReversePdf,
                Weight = throughput,
                AncestorId = lastId,
                Depth = (byte)depth
            }, pathIdx);
            return ColorRGB.Black;
        }

        protected override void OnContinue(float pdfToAncestor, int depth) {
            nextReversePdf = pdfToAncestor;
        }
    }
}
