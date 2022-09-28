using SeeSharp.Shading.MicrofacetDistributions;
namespace SeeSharp.Shading.Bsdfs;

/// <summary>
/// Transmission through rough glass using GGX, based heavily on PBRTv3
/// </summary>
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
        if (SameHemisphere(outDir, inDir)) return RgbColor.Black;  // transmission only

        float cosThetaO = CosTheta(outDir);
        float cosThetaI = CosTheta(inDir);
        if (cosThetaI == 0 || cosThetaO == 0) return RgbColor.Black;

        // Compute the half vector
        float eta = CosTheta(outDir) > 0 ? (insideIOR / outsideIOR) : (outsideIOR / insideIOR);
        Vector3 wh = Vector3.Normalize(outDir + inDir * eta);
        if (CosTheta(wh) < 0) wh = -wh;
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

    float ComputeTotalReflectOneDir(Vector3 outDir, Vector3 inDir) {
        float eta = CosTheta(outDir) > 0 ? (outsideIOR / insideIOR) : (insideIOR / outsideIOR);

        Vector3 halfVector = outDir + inDir;
        halfVector = Vector3.Normalize(halfVector);

        // Check if total reflection occurs at this half vector
        float cos = Vector3.Dot(halfVector, outDir);
        float sinSqr = eta * eta * MathF.Max(0, 1 - cos * cos);
        if (sinSqr < 1) return 0; // No total reflection

        // PDF of total reflection is that of selecting the half vector, times Jacobian of the reflection
        float reflectJacobian = Math.Abs(4 * Vector3.Dot(outDir, halfVector));

        // catch NaN causing corner cases
        if (halfVector == Vector3.Zero || reflectJacobian == 0.0f)
            return 0;

        return distribution.Pdf(outDir, halfVector) / reflectJacobian;
    }

    float ComputeOneDir(Vector3 outDir, Vector3 inDir) {
        // Compute the half vector
        float eta = CosTheta(outDir) > 0 ? (insideIOR / outsideIOR) : (outsideIOR / insideIOR);
        Vector3 halfVector = outDir + inDir * eta;
        if (halfVector == Vector3.Zero) return 0; // Prevent NaN if outDir and inDir exactly align
        halfVector = Vector3.Normalize(halfVector);

        // if (CosTheta(halfVector) < 0) halfVector = -halfVector;
        if (!SameHemisphere(outDir, halfVector)) halfVector = -halfVector;

        // Compute jacobian for refraction
        float sqrtDenom = Vector3.Dot(outDir, halfVector) + eta * Vector3.Dot(inDir, halfVector);
        if (sqrtDenom == 0) return 0; // Prevent NaN in corner case
        float cos = Math.Max(0, Vector3.Dot(inDir, -halfVector));
        float jacobian = (eta * eta * cos) / (sqrtDenom * sqrtDenom);

        float result = distribution.Pdf(outDir, halfVector) * jacobian;
        Debug.Assert(float.IsFinite(result));
        return result;
    }

    public (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
        if (AbsCosTheta(outDir) == 0 || AbsCosTheta(inDir) == 0) return (0, 0);
        return (
            ComputeTotalReflectOneDir(outDir, inDir) + ComputeOneDir(outDir, inDir),
            ComputeTotalReflectOneDir(inDir, outDir) + ComputeOneDir(inDir, outDir)
        );
    }

    public Vector3? Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
        if (outDir.Z == 0) return Vector3.UnitZ; // prevent NaN

        Vector3 halfVector = distribution.Sample(outDir, primarySample);
        if (Vector3.Dot(outDir, halfVector) < 0) return Vector3.UnitZ; // prevent NaN

        float eta = CosTheta(outDir) > 0 ? (outsideIOR / insideIOR) : (insideIOR / outsideIOR);
        var inDir = Refract(outDir, halfVector, eta);

        // If total internal reflection occurs, generate a reflected sample instead
        return inDir ?? Reflect(outDir, halfVector);
    }
}
