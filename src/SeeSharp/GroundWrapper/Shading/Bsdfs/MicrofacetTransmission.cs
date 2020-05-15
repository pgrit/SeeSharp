using GroundWrapper.Shading.MicrofacetDistributions;
using System;
using System.Numerics;

namespace GroundWrapper.Shading.Bsdfs {
    public struct MicrofacetTransmission : BsdfComponent {
        public ColorRGB Transmittance;
        public MicrofacetDistribution Distribution;
        public float outsideIOR, insideIOR;

        ColorRGB BsdfComponent.Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            if (ShadingSpace.SameHemisphere(outDir, inDir)) 
                return ColorRGB.Black;  // transmission only

            float cosThetaO = ShadingSpace.CosTheta(outDir);
            float cosThetaI = ShadingSpace.CosTheta(inDir);
            if (cosThetaI == 0 || cosThetaO == 0) 
                return ColorRGB.Black;

            // Compute $\wh$ from $\outDir$ and $\inDir$ for microfacet transmission
            float eta = ShadingSpace.CosTheta(outDir) > 0 ? (insideIOR / outsideIOR) : (outsideIOR / insideIOR);
            Vector3 wh = Vector3.Normalize(outDir + inDir * eta);
            if (ShadingSpace.CosTheta(wh) < 0) wh = -wh;

            var F = new ColorRGB(FresnelDielectric.Evaluate(Vector3.Dot(outDir, wh), outsideIOR, insideIOR));

            float sqrtDenom = Vector3.Dot(outDir, wh) + eta * Vector3.Dot(inDir, wh);
            float factor = isOnLightSubpath ? (1 / eta) : 1;

            return (ColorRGB.White - F) * Transmittance *
                   Math.Abs(Distribution.NormalDistribution(wh) * Distribution.MaskingShadowing(outDir, inDir) * eta * eta *
                            Math.Abs(Vector3.Dot(inDir, wh)) * Math.Abs(Vector3.Dot(outDir, wh)) * factor * factor /
                            (cosThetaI * cosThetaO * sqrtDenom * sqrtDenom));
        }

        float ComputeOneDir(Vector3 outDir, Vector3 inDir) {
            // Compute $\wh$ from $\outDir$ and $\inDir$ for microfacet transmission
            float eta = ShadingSpace.CosTheta(outDir) > 0 ? (insideIOR / outsideIOR) : (outsideIOR / insideIOR);
            Vector3 wh = Vector3.Normalize(outDir + inDir * eta);

            // Compute change of variables _dwh\_dinDir_ for microfacet transmission
            float sqrtDenom = Vector3.Dot(outDir, wh) + eta * Vector3.Dot(inDir, wh);
            float dwh_dinDir = Math.Abs((eta * eta * Vector3.Dot(inDir, wh)) / (sqrtDenom * sqrtDenom));

            return Distribution.Pdf(outDir, wh) * dwh_dinDir;
        }

        (float, float) BsdfComponent.Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            if (ShadingSpace.SameHemisphere(outDir, inDir)) 
                return (0, 0);
            
            return (ComputeOneDir(outDir, inDir), ComputeOneDir(inDir, outDir));
        }

        Vector3? BsdfComponent.Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            if (outDir.Z == 0) 
                return null;

            Vector3 wh = Distribution.Sample(outDir, primarySample);

            if (Vector3.Dot(outDir, wh) < 0) 
                return null;

            float eta = ShadingSpace.CosTheta(outDir) > 0 ? (outsideIOR / insideIOR) : (insideIOR / outsideIOR);
            var inDir = ShadingSpace.Refract(outDir, wh, eta);
            return inDir;
        }
    }
}
