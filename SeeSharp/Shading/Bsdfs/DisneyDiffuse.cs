using SeeSharp.Sampling;
using SimpleImageIO;
using System;
using System.Numerics;

namespace SeeSharp.Shading.Bsdfs {
    public struct DisneyDiffuse {
        public RgbColor reflectance;

        public DisneyDiffuse(RgbColor reflectance) => this.reflectance = reflectance;

        public RgbColor Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No transmission
            if (!SameHemisphere(outDir, inDir))
                return RgbColor.Black;

            float fresnelOut = FresnelSchlick.SchlickWeight(AbsCosTheta(outDir));
            float fresnelIn = FresnelSchlick.SchlickWeight(AbsCosTheta(inDir));

            // Diffuse fresnel - go from 1 at normal incidence to .5 at grazing.
            // Burley 2015, eq (4).
            return reflectance / MathF.PI * (1 - fresnelOut / 2) * (1 - fresnelIn / 2);
        }

        public Vector3? Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            // Transform primary sample to cosine hemisphere
            var local = SampleWarp.ToCosHemisphere(primarySample);

            // Make sure it ends up on the same hemisphere as the outgoing direction
            if (CosTheta(outDir) < 0)
                local.Direction.Z *= -1;

            return local.Direction;
        }

        public (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No transmission
            if (!SameHemisphere(outDir, inDir))
                return (0, 0);

            float pdf = AbsCosTheta(inDir) / MathF.PI;
            float pdfReverse = AbsCosTheta(outDir) / MathF.PI;
            return (pdf, pdfReverse);
        }
    }

    public struct DisneyRetroReflection {
        RgbColor reflectance;
        float roughness;

        public DisneyRetroReflection(RgbColor reflectance, float roughness) {
            this.reflectance = reflectance;
            this.roughness = roughness;
        }

        public RgbColor Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            if (!SameHemisphere(outDir, inDir))
                return RgbColor.Black;

            Vector3 wh = inDir + outDir;
            if (wh == Vector3.Zero) return RgbColor.Black;
            wh = Vector3.Normalize(wh);

            float cosThetaD = Vector3.Dot(inDir, wh);

            float Fo = FresnelSchlick.SchlickWeight(AbsCosTheta(outDir));
            float Fi = FresnelSchlick.SchlickWeight(AbsCosTheta(inDir));
            float Rr = 2 * roughness * cosThetaD * cosThetaD;

            // Burley 2015, eq (4).
            return reflectance / MathF.PI * Rr * (Fo + Fi + Fo * Fi * (Rr - 1));
        }

        public Vector3? Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            // Transform primary sample to cosine hemisphere
            var local = SampleWarp.ToCosHemisphere(primarySample);

            // Make sure it ends up on the same hemisphere as the outgoing direction
            if (CosTheta(outDir) < 0)
                local.Direction.Z *= -1;

            return local.Direction;
        }

        public (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No transmission
            if (!SameHemisphere(outDir, inDir))
                return (0, 0);

            float pdf = AbsCosTheta(inDir) / MathF.PI;
            float pdfReverse = AbsCosTheta(outDir) / MathF.PI;
            return (pdf, pdfReverse);
        }
    }
}
