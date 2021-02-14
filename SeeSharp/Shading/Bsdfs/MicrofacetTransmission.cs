using SeeSharp.Shading.MicrofacetDistributions;
using System;
using System.Numerics;

namespace SeeSharp.Shading.Bsdfs {
    public struct MicrofacetTransmission {
        public ColorRGB Transmittance;
        public TrowbridgeReitzDistribution Distribution;
        public float outsideIOR, insideIOR;

        public MicrofacetTransmission(ColorRGB transmittance, TrowbridgeReitzDistribution distribution,
                                      float outsideIOR, float insideIOR) {
            this.Transmittance = transmittance;
            this.Distribution = distribution;
            this.outsideIOR = outsideIOR;
            this.insideIOR = insideIOR;
        }

        public ColorRGB Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            if (ShadingSpace.SameHemisphere(outDir, inDir)) return ColorRGB.Black;  // transmission only

            float cosThetaO = ShadingSpace.CosTheta(outDir);
            float cosThetaI = ShadingSpace.CosTheta(inDir);
            if (cosThetaI == 0 || cosThetaO == 0) return ColorRGB.Black;

            // Compute the half vector
            float eta = ShadingSpace.CosTheta(outDir) > 0 ? (insideIOR / outsideIOR) : (outsideIOR / insideIOR);
            Vector3 wh = Vector3.Normalize(outDir + inDir * eta);
            if (ShadingSpace.CosTheta(wh) < 0) wh = -wh;

            if (Vector3.Dot(outDir, wh) * Vector3.Dot(inDir, wh) > 0) return ColorRGB.Black;

            var F = new ColorRGB(FresnelDielectric.Evaluate(Vector3.Dot(outDir, wh), outsideIOR, insideIOR));

            float sqrtDenom = Vector3.Dot(outDir, wh) + eta * Vector3.Dot(inDir, wh);
            float factor = isOnLightSubpath ? (1 / eta) : 1;

            var numerator = Distribution.NormalDistribution(wh) * Distribution.MaskingShadowing(outDir, inDir);
            numerator *= eta * eta * Math.Abs(Vector3.Dot(inDir, wh)) * Math.Abs(Vector3.Dot(outDir, wh));
            numerator *= factor * factor;

            var denom = (cosThetaI * cosThetaO * sqrtDenom * sqrtDenom);

            return (ColorRGB.White - F) * Transmittance * Math.Abs(numerator / denom);
        }

        float ComputeOneDir(Vector3 outDir, Vector3 inDir) {
            // Compute the half vector
            float eta = ShadingSpace.CosTheta(outDir) > 0 ? (insideIOR / outsideIOR) : (outsideIOR / insideIOR);
            Vector3 wh = Vector3.Normalize(outDir + inDir * eta);

            if (Vector3.Dot(outDir, wh) * Vector3.Dot(inDir, wh) > 0) return 0;

            // Compute change of variables _dwh\_dinDir_ for microfacet transmission
            float sqrtDenom = Vector3.Dot(outDir, wh) + eta * Vector3.Dot(inDir, wh);
            float dwh_dinDir = Math.Abs((eta * eta * Vector3.Dot(inDir, wh)) / (sqrtDenom * sqrtDenom));

            return Distribution.Pdf(outDir, wh) * dwh_dinDir;
        }

        public (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            if (ShadingSpace.SameHemisphere(outDir, inDir)) return (0, 0);
            return (ComputeOneDir(outDir, inDir), ComputeOneDir(inDir, outDir));
        }

        public Vector3? Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            if (outDir.Z == 0) return null;

            Vector3 wh = Distribution.Sample(outDir, primarySample);
            if (Vector3.Dot(outDir, wh) < 0) return null;

            float eta = ShadingSpace.CosTheta(outDir) > 0 ? (outsideIOR / insideIOR) : (insideIOR / outsideIOR);
            var inDir = ShadingSpace.Refract(outDir, wh, eta);
            return inDir;
        }
    }
}
