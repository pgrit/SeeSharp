using SeeSharp.Geometry;
using SeeSharp.Shading.Bsdfs;
using SeeSharp.Image;
using System.Numerics;

namespace SeeSharp.Shading.Materials {
    public class DiffuseMaterial : Material {
        public class Parameters {
            public Image<ColorRGB> baseColor = Image<ColorRGB>.Constant(ColorRGB.White);
            public bool transmitter = false;
        }

        public DiffuseMaterial(Parameters parameters) => this.parameters = parameters;

        public override float GetRoughness(SurfacePoint hit) => 1;

        public override ColorRGB GetScatterStrength(SurfacePoint hit) {
            var tex = hit.TextureCoordinates;
            var baseColor = parameters.baseColor[tex.X * parameters.baseColor.Width, tex.Y * parameters.baseColor.Height];
            return baseColor;
        }

        public override ColorRGB Evaluate(SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // Transform directions to shading space and normalize
            var normal = hit.ShadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);
            inDir = ShadingSpace.WorldToShading(normal, inDir);

            var baseColor = parameters.baseColor.TextureLookup(hit.TextureCoordinates);
            if (parameters.transmitter) {
                return new DiffuseTransmission(baseColor).Evaluate(outDir, inDir, isOnLightSubpath);
            } else {
                return new DiffuseBsdf(baseColor).Evaluate(outDir, inDir, isOnLightSubpath);
            }
        }

        public override BsdfSample Sample(SurfacePoint hit, Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            var normal = hit.ShadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);

            var baseColor = parameters.baseColor.TextureLookup(hit.TextureCoordinates);
            Vector3? sample = null;
            if (parameters.transmitter) {
                sample = new DiffuseTransmission(baseColor).Sample(outDir, isOnLightSubpath, primarySample);
            } else {
                sample = new DiffuseBsdf(baseColor).Sample(outDir, isOnLightSubpath, primarySample);
            }

            if (!sample.HasValue)
                return BsdfSample.Invalid;

            // Terminate if no valid direction was sampled
            if (!sample.HasValue)
                return BsdfSample.Invalid;
            var sampledDir = ShadingSpace.ShadingToWorld(hit.ShadingNormal, sample.Value);

            // Evaluate all components
            var outWorld = ShadingSpace.ShadingToWorld(hit.ShadingNormal, outDir);
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

            var baseColor = parameters.baseColor.TextureLookup(hit.TextureCoordinates);
            if (parameters.transmitter) {
                return new DiffuseTransmission(baseColor).Pdf(outDir, inDir, isOnLightSubpath);
            } else {
                return new DiffuseBsdf(baseColor).Pdf(outDir, inDir, isOnLightSubpath);
            }
        }

        Parameters parameters;
    }
}
