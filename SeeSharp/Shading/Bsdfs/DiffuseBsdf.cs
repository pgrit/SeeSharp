using SeeSharp.Sampling;
using System;
using System.Numerics;

namespace SeeSharp.Shading.Bsdfs {
    public struct DiffuseBsdf {
        ColorRGB reflectance;

        public DiffuseBsdf(ColorRGB reflectance) => this.reflectance = reflectance;

        public ColorRGB Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No transmission
            if (!ShadingSpace.SameHemisphere(outDir, inDir))
                return ColorRGB.Black;
            return reflectance / MathF.PI;
        }

        public Vector3? Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            // Transform primary sample to cosine hemisphere
            var local = SampleWarp.ToCosHemisphere(primarySample);

            // Make sure it ends up on the same hemisphere as the outgoing direction
            if (ShadingSpace.CosTheta(outDir) < 0)
                local.Direction.Z *= -1;

            return local.Direction;
        }

        public (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No transmission
            if (!ShadingSpace.SameHemisphere(outDir, inDir))
                return (0, 0);

            float pdf = ShadingSpace.AbsCosTheta(inDir) / MathF.PI;
            float pdfReverse = ShadingSpace.AbsCosTheta(outDir) / MathF.PI;
            return (pdf, pdfReverse);
        }
    }

    public struct DiffuseTransmission {
        ColorRGB transmittance;

        public DiffuseTransmission(ColorRGB transmittance) => this.transmittance = transmittance;

        public ColorRGB Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No reflection
            if (ShadingSpace.SameHemisphere(outDir, inDir))
                return ColorRGB.Black;
            return transmittance / MathF.PI;
        }

        public Vector3? Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            // Transform primary sample to cosine hemisphere
            var local = SampleWarp.ToCosHemisphere(primarySample);

            // Make sure the sample is in the other hemisphere as the outgoing direction
            if (ShadingSpace.CosTheta(outDir) > 0)
                local.Direction.Z *= -1;

            return local.Direction;
        }

        public (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No reflection
            if (ShadingSpace.SameHemisphere(outDir, inDir))
                return (0, 0);

            float pdf = ShadingSpace.AbsCosTheta(inDir) / MathF.PI;
            float pdfReverse = ShadingSpace.AbsCosTheta(outDir) / MathF.PI;
            return (pdf, pdfReverse);
        }
    }
}
