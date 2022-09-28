namespace SeeSharp.Shading.Bsdfs;

public struct DiffuseBsdf {
    RgbColor reflectance;

    public DiffuseBsdf(RgbColor reflectance) => this.reflectance = reflectance;

    public RgbColor Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
        // No transmission
        if (!SameHemisphere(outDir, inDir))
            return RgbColor.Black;
        return reflectance / MathF.PI;
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

public struct DiffuseTransmission {
    RgbColor transmittance;

    public DiffuseTransmission(RgbColor transmittance) => this.transmittance = transmittance;

    public RgbColor Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
        // No reflection
        if (SameHemisphere(outDir, inDir))
            return RgbColor.Black;
        return transmittance / MathF.PI;
    }

    public Vector3? Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
        // Transform primary sample to cosine hemisphere
        var local = SampleWarp.ToCosHemisphere(primarySample);

        // Make sure the sample is in the other hemisphere as the outgoing direction
        if (CosTheta(outDir) > 0)
            local.Direction.Z *= -1;

        return local.Direction;
    }

    public (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
        // No reflection
        if (SameHemisphere(outDir, inDir))
            return (0, 0);

        float pdf = AbsCosTheta(inDir) / MathF.PI;
        float pdfReverse = AbsCosTheta(outDir) / MathF.PI;
        return (pdf, pdfReverse);
    }
}
