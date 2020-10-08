using SeeSharp.Core.Shading.MicrofacetDistributions;
using System.Numerics;

namespace SeeSharp.Core.Shading.Bsdfs {
    public struct MicrofacetReflection {
        TrowbridgeReitzDistribution distribution;
        Fresnel fresnel;
        ColorRGB tint;

        public MicrofacetReflection(TrowbridgeReitzDistribution distribution, Fresnel fresnel, ColorRGB tint) {
            this.distribution = distribution;
            this.fresnel = fresnel;
            this.tint = tint;
        }

        public ColorRGB Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            if (!ShadingSpace.SameHemisphere(outDir, inDir))
                return ColorRGB.Black;

            float cosThetaO = ShadingSpace.AbsCosTheta(outDir);
            float cosThetaI = ShadingSpace.AbsCosTheta(inDir);
            Vector3 halfVector = inDir + outDir;

            // Handle degenerate cases for microfacet reflection
            if (cosThetaI == 0 || cosThetaO == 0)
                return ColorRGB.Black;
            if (halfVector.X == 0 && halfVector.Y == 0 && halfVector.Z == 0)
                return ColorRGB.Black;

            // For the Fresnel call, make sure that wh is in the same hemisphere
            // as the surface normal, so that total internal reflection is handled correctly.
            halfVector = Vector3.Normalize(halfVector);
            if (ShadingSpace.CosTheta(halfVector) < 0)
                halfVector = -halfVector;

            var cosine = Vector3.Dot(inDir, halfVector);
            var f = fresnel.Evaluate(cosine);
            return tint * distribution.NormalDistribution(halfVector)
                * distribution.MaskingShadowing(outDir, inDir) * f / (4 * cosThetaI * cosThetaO);
        }

        public (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            if (!ShadingSpace.SameHemisphere(outDir, inDir))
                return (0, 0);

            var halfVector = outDir + inDir;

            // catch NaN causing corner cases
            if (halfVector == Vector3.Zero)
                return (0, 0);

            halfVector = Vector3.Normalize(halfVector);
            var pdfForward = distribution.Pdf(outDir, halfVector) / (4 * Vector3.Dot(outDir, halfVector));
            var pdfReverse = distribution.Pdf(inDir, halfVector) / (4 * Vector3.Dot(inDir, halfVector));
            return (pdfForward, pdfReverse);
        }

        public Vector3? Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            if (outDir.Z == 0)
                return null;

            var halfVector = distribution.Sample(outDir, primarySample);
            if (Vector3.Dot(halfVector, outDir) < 0)
                return null;

            var inDir = ShadingSpace.Reflect(outDir, halfVector);
            if (!ShadingSpace.SameHemisphere(outDir, inDir))
                return null;

            return inDir;
        }
    }
}
