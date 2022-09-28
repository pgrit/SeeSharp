using SeeSharp.Shading.MicrofacetDistributions;

namespace SeeSharp.Shading.Bsdfs;

public struct MicrofacetReflection {
    TrowbridgeReitzDistribution distribution;
    Fresnel fresnel;
    RgbColor tint;

    public MicrofacetReflection(TrowbridgeReitzDistribution distribution, Fresnel fresnel, RgbColor tint) {
        this.distribution = distribution;
        this.fresnel = fresnel;
        this.tint = tint;
    }

    public RgbColor Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
        if (!SameHemisphere(outDir, inDir))
            return RgbColor.Black;

        float cosThetaO = AbsCosTheta(outDir);
        float cosThetaI = AbsCosTheta(inDir);
        Vector3 halfVector = inDir + outDir;

        // Handle degenerate cases for microfacet reflection
        if (cosThetaI == 0 || cosThetaO == 0)
            return RgbColor.Black;
        if (halfVector.X == 0 && halfVector.Y == 0 && halfVector.Z == 0)
            return RgbColor.Black;

        // For the Fresnel call, make sure that wh is in the same hemisphere
        // as the surface normal, so that total internal reflection is handled correctly.
        halfVector = Vector3.Normalize(halfVector);
        if (CosTheta(halfVector) < 0)
            halfVector = -halfVector;

        var cosine = Vector3.Dot(inDir, halfVector);
        var f = fresnel.Evaluate(cosine);

        var nd = distribution.NormalDistribution(halfVector);
        var ms = distribution.MaskingShadowing(outDir, inDir);
        return tint * nd * ms * f / (4 * cosThetaI * cosThetaO);
    }

    public (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
        var halfVector = outDir + inDir;
        halfVector = Vector3.Normalize(halfVector);

        // Compute the jacobian of the reflection about the half vector
        float reflectJacobianFwd = Math.Abs(4 * Vector3.Dot(outDir, halfVector));
        float reflectJacobianRev = Math.Abs(4 * Vector3.Dot(inDir, halfVector));

        // catch NaN causing corner cases
        if (halfVector == Vector3.Zero || reflectJacobianFwd == 0.0f || reflectJacobianRev == 0.0f)
            return (0, 0);

        var pdfForward = distribution.Pdf(outDir, halfVector) / reflectJacobianFwd;
        var pdfReverse = distribution.Pdf(inDir, halfVector) / reflectJacobianRev;
        return (pdfForward, pdfReverse);
    }

    public Vector3? Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
        if (outDir.Z == 0)
            return null;

        var halfVector = distribution.Sample(outDir, primarySample);
        var inDir = Reflect(outDir, halfVector);

        return inDir;
    }
}