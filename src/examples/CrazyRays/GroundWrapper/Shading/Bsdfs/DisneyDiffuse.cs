using GroundWrapper.Sampling;
using System;
using System.Numerics;

namespace GroundWrapper.Shading.Bsdfs {
    public class DisneyDiffuse : BsdfComponent {
        public ColorRGB Reflectance;

        ColorRGB BsdfComponent.Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No transmission
            if (!ShadingSpace.SameHemisphere(outDir, inDir))
                return ColorRGB.Black;

            float fresnelOut = FresnelSchlick.SchlickWeight(ShadingSpace.AbsCosTheta(outDir));
            float fresnelIn = FresnelSchlick.SchlickWeight(ShadingSpace.AbsCosTheta(inDir));

            // Diffuse fresnel - go from 1 at normal incidence to .5 at grazing.
            // Burley 2015, eq (4).
            return Reflectance / MathF.PI * (1 - fresnelOut / 2) * (1 - fresnelIn / 2);
        }

        Vector3? BsdfComponent.Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            // Transform primary sample to cosine hemisphere
            var local = SampleWrap.ToCosHemisphere(primarySample);
            return local.direction;
        }

        (float, float) BsdfComponent.Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No transmission
            if (!ShadingSpace.SameHemisphere(outDir, inDir))
                return (0, 0);

            float pdf = ShadingSpace.AbsCosTheta(inDir) / MathF.PI;
            float pdfReverse = ShadingSpace.AbsCosTheta(outDir) / MathF.PI;
            return (pdf, pdfReverse);
        }
    }

    public class DisneyRetroReflection : BsdfComponent {
        public ColorRGB Reflectance;
        public float Roughness;

        ColorRGB BsdfComponent.Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            if (!ShadingSpace.SameHemisphere(outDir, inDir))
                return ColorRGB.Black;

            Vector3 wh = inDir + outDir;
            if (wh == Vector3.Zero) return ColorRGB.Black;
            wh = Vector3.Normalize(wh);

            float cosThetaD = Vector3.Dot(inDir, wh);

            float Fo = FresnelSchlick.SchlickWeight(ShadingSpace.AbsCosTheta(outDir));
            float Fi = FresnelSchlick.SchlickWeight(ShadingSpace.AbsCosTheta(inDir));
            float Rr = 2 * Roughness * cosThetaD * cosThetaD;

            // Burley 2015, eq (4).
            return Reflectance / MathF.PI * Rr * (Fo + Fi + Fo * Fi * (Rr - 1));
        }

        Vector3? BsdfComponent.Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            // Transform primary sample to cosine hemisphere
            var local = SampleWrap.ToCosHemisphere(primarySample);
            return local.direction;
        }

        (float, float) BsdfComponent.Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No transmission
            if (!ShadingSpace.SameHemisphere(outDir, inDir))
                return (0, 0);

            float pdf = ShadingSpace.AbsCosTheta(inDir) / MathF.PI;
            float pdfReverse = ShadingSpace.AbsCosTheta(outDir) / MathF.PI;
            return (pdf, pdfReverse);
        }
    }
}
