using SeeSharp.Shading.MicrofacetDistributions;
using SimpleImageIO;
using System;
using System.Diagnostics;
using System.Numerics;

namespace SeeSharp.Shading.Bsdfs {
    public struct MicrofacetTransmission {
        RgbColor transmittance;
        TrowbridgeReitzDistribution distribution;
        float outsideIOR, insideIOR;

        public MicrofacetTransmission(RgbColor transmittance, TrowbridgeReitzDistribution distribution,
                                      float outsideIOR, float insideIOR) {
            this.transmittance = transmittance;
            this.distribution = distribution;
            this.outsideIOR = outsideIOR;
            this.insideIOR = insideIOR;

            Debug.Assert(outsideIOR != insideIOR);
        }

        public RgbColor Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            if (ShadingSpace.SameHemisphere(outDir, inDir)) return RgbColor.Black;  // transmission only

            float cosThetaO = ShadingSpace.CosTheta(outDir);
            float cosThetaI = ShadingSpace.CosTheta(inDir);
            if (cosThetaI == 0 || cosThetaO == 0) return RgbColor.Black;

            // Compute the half vector
            float eta = ShadingSpace.CosTheta(outDir) > 0 ? (insideIOR / outsideIOR) : (outsideIOR / insideIOR);
            Vector3 wh = Vector3.Normalize(outDir + inDir * eta);
            if (ShadingSpace.CosTheta(wh) < 0) wh = -wh;
            if (Vector3.Dot(outDir, wh) * Vector3.Dot(inDir, wh) > 0) return RgbColor.Black;

            var F = new RgbColor(FresnelDielectric.Evaluate(Vector3.Dot(outDir, wh), outsideIOR, insideIOR));

            float sqrtDenom = Vector3.Dot(outDir, wh) + eta * Vector3.Dot(inDir, wh);
            float factor = isOnLightSubpath ? (1 / eta) : 1;

            var numerator = distribution.NormalDistribution(wh) * distribution.MaskingShadowing(outDir, inDir);
            numerator *= eta * eta * Math.Abs(Vector3.Dot(inDir, wh)) * Math.Abs(Vector3.Dot(outDir, wh));
            numerator *= factor * factor;

            var denom = (cosThetaI * cosThetaO * sqrtDenom * sqrtDenom);
            Debug.Assert(float.IsFinite(denom));
            return (RgbColor.White - F) * transmittance * Math.Abs(numerator / denom);
        }

        float ComputeOneDir(Vector3 outDir, Vector3 inDir) {
            // Compute the half vector
            float eta = ShadingSpace.CosTheta(outDir) > 0 ? (insideIOR / outsideIOR) : (outsideIOR / insideIOR);
            Vector3 wh = outDir + inDir * eta;
            if (wh == Vector3.Zero) return 0; // Prevent NaN if outDir and inDir exactly align
            wh = Vector3.Normalize(wh);

            if (Vector3.Dot(outDir, wh) * Vector3.Dot(inDir, wh) > 0) return 0;

            // Compute change of variables _dwh\_dinDir_ for microfacet transmission
            float sqrtDenom = Vector3.Dot(outDir, wh) + eta * Vector3.Dot(inDir, wh);
            if (sqrtDenom == 0) return 0; // Prevent NaN in corner case
            float dwh_dinDir = Math.Abs((eta * eta * Vector3.Dot(inDir, wh)) / (sqrtDenom * sqrtDenom));

            float result = distribution.Pdf(outDir, wh) * dwh_dinDir;
            Debug.Assert(float.IsFinite(result));
            return result;
        }

        public (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            if (ShadingSpace.SameHemisphere(outDir, inDir)) return (0, 0);
            return (ComputeOneDir(outDir, inDir), ComputeOneDir(inDir, outDir));
        }

        public Vector3? Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            if (outDir.Z == 0) return null;

            Vector3 wh = distribution.Sample(outDir, primarySample);
            if (Vector3.Dot(outDir, wh) < 0) return null;

            float eta = ShadingSpace.CosTheta(outDir) > 0 ? (outsideIOR / insideIOR) : (insideIOR / outsideIOR);
            var inDir = ShadingSpace.Refract(outDir, wh, eta);
            return inDir;
        }
    }
}
