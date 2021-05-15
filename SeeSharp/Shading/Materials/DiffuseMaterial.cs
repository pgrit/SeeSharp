using SeeSharp.Geometry;
using SeeSharp.Shading.Bsdfs;
using SeeSharp.Image;
using System.Numerics;
using SimpleImageIO;

namespace SeeSharp.Shading.Materials {
    /// <summary>
    /// A simple Lambertian material that either only reflects or only transmits.
    /// </summary>
    public class DiffuseMaterial : Material {
        /// <summary>
        /// Specifies the color and whether the material transmits or reflects
        /// </summary>
        public class Parameters {
            /// <summary>
            /// Scattering color
            /// </summary>
            public TextureRgb BaseColor = new TextureRgb(RgbColor.White);

            /// <summary>
            /// If true, only transmittance happens, if false, only reflection
            /// </summary>
            public bool Transmitter = false;
        }

        /// <returns>True if we only transmit</returns>
        public override bool IsTransmissive(SurfacePoint hit) => parameters.Transmitter;

        /// <summary>
        /// Creates a new diffuse material with the given parameters
        /// </summary>
        public DiffuseMaterial(Parameters parameters) => this.parameters = parameters;

        /// <returns>Always 1</returns>
        public override float GetRoughness(SurfacePoint hit) => 1;

        /// <returns>Always 1</returns>
        public override float GetIndexOfRefractionRatio(SurfacePoint hit, Vector3 outDir, Vector3 inDir) => 1;

        /// <returns>The base color</returns>
        public override RgbColor GetScatterStrength(SurfacePoint hit)
        => parameters.BaseColor.Lookup(hit.TextureCoordinates);

        /// <returns>1/pi * baseColor, or zero if the directions are not in the right hemispheres</returns>
        public override RgbColor Evaluate(SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            bool shouldReflect = ShouldReflect(hit, outDir, inDir);

            // Transform directions to shading space and normalize
            var normal = hit.ShadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);
            inDir = ShadingSpace.WorldToShading(normal, inDir);

            var baseColor = parameters.BaseColor.Lookup(hit.TextureCoordinates);
            if (parameters.Transmitter && !shouldReflect) {
                return new DiffuseTransmission(baseColor).Evaluate(outDir, inDir, isOnLightSubpath);
            } else if (shouldReflect) {
                return new DiffuseBsdf(baseColor).Evaluate(outDir, inDir, isOnLightSubpath);
            } else
                return RgbColor.Black;
        }

        /// <summary>
        /// Importance samples the cosine hemisphere
        /// </summary>
        public override BsdfSample Sample(SurfacePoint hit, Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            var normal = hit.ShadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);

            var baseColor = parameters.BaseColor.Lookup(hit.TextureCoordinates);
            Vector3? sample;
            if (parameters.Transmitter) {
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

        /// <returns>PDF value used by <see cref="Sample"/></returns>
        public override (float, float) Pdf(SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // Transform directions to shading space and normalize
            var normal = hit.ShadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);
            inDir = ShadingSpace.WorldToShading(normal, inDir);

            var baseColor = parameters.BaseColor.Lookup(hit.TextureCoordinates);
            if (parameters.Transmitter) {
                return new DiffuseTransmission(baseColor).Pdf(outDir, inDir, isOnLightSubpath);
            } else {
                return new DiffuseBsdf(baseColor).Pdf(outDir, inDir, isOnLightSubpath);
            }
        }

        Parameters parameters;
    }
}
