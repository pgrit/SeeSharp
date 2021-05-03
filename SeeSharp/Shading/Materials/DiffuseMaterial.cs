using SeeSharp.Geometry;
using SeeSharp.Shading.Bsdfs;
using SeeSharp.Image;
using System.Numerics;
using SimpleImageIO;

namespace SeeSharp.Shading.Materials {
    public class DiffuseMaterial : Material {
        public class Parameters {
            public TextureRgb baseColor = new TextureRgb(RgbColor.White);
            public bool transmitter = false;
        }

        public DiffuseMaterial(Parameters parameters) => this.parameters = parameters;

        public override float GetRoughness(SurfacePoint hit) => 1;

        public override float GetIndexOfRefractionRatio(SurfacePoint hit, Vector3 outDir, Vector3 inDir) => 1;

        public override RgbColor GetScatterStrength(SurfacePoint hit)
        => parameters.baseColor.Lookup(hit.TextureCoordinates);

        public override RgbColor Evaluate(SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            bool shouldReflect = ShouldReflect(hit, outDir, inDir);

            // Transform directions to shading space and normalize
            var normal = hit.ShadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);
            inDir = ShadingSpace.WorldToShading(normal, inDir);

            var baseColor = parameters.baseColor.Lookup(hit.TextureCoordinates);
            if (parameters.transmitter && !shouldReflect) {
                return new DiffuseTransmission(baseColor).Evaluate(outDir, inDir, isOnLightSubpath);
            } else if (shouldReflect) {
                return new DiffuseBsdf(baseColor).Evaluate(outDir, inDir, isOnLightSubpath);
            } else
                return RgbColor.Black;
        }

        public override BsdfSample Sample(SurfacePoint hit, Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            var normal = hit.ShadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);

            var baseColor = parameters.baseColor.Lookup(hit.TextureCoordinates);
            Vector3? sample;
            if (parameters.transmitter) {
                sample = new DiffuseTransmission(baseColor).Sample(outDir, isOnLightSubpath, primarySample);
            } else {
                sample = new DiffuseBsdf(baseColor).Sample(outDir, isOnLightSubpath, primarySample);
            }

            // Terminate if no valid direction was sampled
            if (!sample.HasValue)
                return BsdfSample.Invalid;

            var sampledDir = ShadingSpace.ShadingToWorld(normal, sample.Value);

            // Evaluate all components
            var outWorld = ShadingSpace.ShadingToWorld(normal, outDir);
            var value = EvaluateWithCosine(hit, outWorld, sampledDir, isOnLightSubpath);

            // Compute all pdfs
            var (pdfFwd, pdfRev) = Pdf(hit, outWorld, sampledDir, isOnLightSubpath);
            if (pdfFwd == 0)
                return BsdfSample.Invalid;

            // Combine results with balance heuristic MIS
            return new BsdfSample {
                pdf = pdfFwd,
                pdfReverse = pdfRev,
                weight = value / pdfFwd,
                direction = sampledDir
            };
        }

        public override (float, float) Pdf(SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // Transform directions to shading space and normalize
            var normal = hit.ShadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);
            inDir = ShadingSpace.WorldToShading(normal, inDir);

            var baseColor = parameters.baseColor.Lookup(hit.TextureCoordinates);
            if (parameters.transmitter) {
                return new DiffuseTransmission(baseColor).Pdf(outDir, inDir, isOnLightSubpath);
            } else {
                return new DiffuseBsdf(baseColor).Pdf(outDir, inDir, isOnLightSubpath);
            }
        }

        Parameters parameters;
    }
}
