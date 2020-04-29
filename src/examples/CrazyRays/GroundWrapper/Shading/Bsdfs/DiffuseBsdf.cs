using GroundWrapper.Sampling;
using System;
using System.Numerics;

namespace GroundWrapper.Shading.Bsdfs {
    public struct DiffuseBsdf : BsdfComponent {
        public ColorRGB reflectance;

        ColorRGB BsdfComponent.Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No transmission
            if (!ShadingSpace.SameHemisphere(outDir, inDir))
                return ColorRGB.Black;
            return reflectance / MathF.PI;
        }

        Vector3? BsdfComponent.Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            // Transform primary sample to cosine hemisphere
            var local = SampleWrap.ToCosHemisphere(primarySample);

            // Make sure it ends up on the same hemisphere as the outgoing direction
            if (ShadingSpace.CosTheta(outDir) < 0) 
                local.direction.Z *= -1;

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

    public struct DiffuseTransmission : BsdfComponent {
        public ColorRGB Transmittance;

        ColorRGB BsdfComponent.Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No reflection
            if (ShadingSpace.SameHemisphere(outDir, inDir))
                return ColorRGB.Black;
            return Transmittance / MathF.PI;
        }

        Vector3? BsdfComponent.Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            // Transform primary sample to cosine hemisphere
            var local = SampleWrap.ToCosHemisphere(primarySample);
            return -local.direction;
        }

        (float, float) BsdfComponent.Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No reflection
            if (ShadingSpace.SameHemisphere(outDir, inDir))
                return (0, 0);

            float pdf = ShadingSpace.AbsCosTheta(inDir) / MathF.PI;
            float pdfReverse = ShadingSpace.AbsCosTheta(outDir) / MathF.PI;
            return (pdf, pdfReverse);
        }
    }
}
