using SeeSharp.Common;

namespace SeeSharp.Shading.MicrofacetDistributions;

/// <summary>
/// GGX microfacet distribution. Based on PBRT v3 with some sprinkles of Heitz's JCGT paper.
/// </summary>
public struct TrowbridgeReitzDistribution {
    /// <summary>
    /// Squared roughness in one direction
    /// </summary>
    public float AlphaX;

    /// <summary>
    /// Squared roughness in the other direction
    /// </summary>
    public float AlphaY;

    /// <summary>
    /// Computes the distribution of microfacets with the given normal.
    /// </summary>
    /// <param name="normal">The normal vector of the microfacets, in shading space.</param>
    /// <returns>The fraction of microfacets that are oriented with the given normal.</returns>
    public float NormalDistribution(Vector3 normal) {
        float tan2Theta = TanThetaSqr(normal);
        if (float.IsInfinity(tan2Theta)) return 0;

        float cos4Theta = CosThetaSqr(normal) * CosThetaSqr(normal);

        float e = tan2Theta * (
              CosPhiSqr(normal) / (AlphaX * AlphaX)
            + SinPhiSqr(normal) / (AlphaY * AlphaY)
        );

        return 1 / (MathF.PI * AlphaX * AlphaY * cos4Theta * (1 + e) * (1 + e));
    }

    /// <summary>
    /// Computes the masking-shadowing function:
    /// The ratio of visible microfacet area to the total area of all correctly oriented microfacets.
    /// </summary>
    /// <param name="outDir">
    /// The view direction, in shading space.
    /// </param>
    /// <returns>The masking shadowing function value ("G" in most papers).</returns>
    public float MaskingShadowing(Vector3 outDir) {
        return 1 / (1 + MaskingRatio(outDir));
    }

    public float MaskingShadowing(Vector3 outDir, Vector3 inDir) {
        return 1 / (1 + MaskingRatio(outDir) + MaskingRatio(inDir));
    }

    /// <summary>
    /// The Pdf that is used for importance sampling microfacet normals from this distribution.
    /// This usually importance samples the portion of normals that are in the hemisphere of the outgoing direction.
    /// </summary>
    /// <param name="outDir">The outgoing direction in shading space.</param>
    /// <param name="normal">The normal in shading space.</param>
    /// <returns>The pdf value.</returns>
    public float Pdf(Vector3 outDir, Vector3 normal) {
        if (!SameHemisphere(outDir, normal)) return 0;
        return MaskingShadowing(outDir) * Math.Max(0, Vector3.Dot(outDir, normal))
            * NormalDistribution(normal) / Math.Abs(outDir.Z);
    }

    /// <summary>
    /// Warps the given primary sample to follow the pdf computed by <see cref="Pdf(Vector3, Vector3)"/>.
    /// Taken from: Heitz. 2018. "Sampling the GGX Distribution of Visible Normals." JCGT.
    /// </summary>
    /// <returns>The direction that corresponds to the given primary sample.</returns>
    public Vector3 Sample(Vector3 outDir, Vector2 primary) {
        bool flip = false;
        if (outDir.Z < 0) {
            flip = true;
            outDir = -outDir;
        }

        // Section 3.2: transforming the view direction to the hemisphere configuration
        Vector3 Vh = Vector3.Normalize(new Vector3(AlphaX * outDir.X, AlphaY * outDir.Y, outDir.Z));

        // Section 4.1: orthonormal basis (with special case if cross product is zero)
        float lensq = Vh.X * Vh.X + Vh.Y * Vh.Y;
        Vector3 T1 = lensq > 0 ? new Vector3(-Vh.Y, Vh.X, 0) * MathF.ReciprocalSqrtEstimate(lensq) : new Vector3(1, 0, 0);
        Vector3 T2 = Vector3.Cross(Vh, T1);

        // Section 4.2: parameterization of the projected area
        float r = MathF.Sqrt(primary.X);
        float phi = 2.0f * MathF.PI * primary.Y;
        float t1 = r * MathF.Cos(phi);
        float t2 = r * MathF.Sin(phi);
        float s = 0.5f * (1.0f + Vh.Z);
        t2 = (1.0f - s) * MathF.Sqrt(1.0f - t1 * t1) + s * t2;

        // Section 4.3: reprojection onto hemisphere
        Vector3 Nh = t1 * T1 + t2 * T2 + MathF.Sqrt(Math.Max(0.0f, 1.0f - t1 * t1 - t2 * t2)) * Vh;

        // Section 3.4: transforming the normal back to the ellipsoid configuration
        Vector3 Ne = Vector3.Normalize(new Vector3(AlphaX * Nh.X, AlphaY * Nh.Y, Math.Max(0.0f, Nh.Z)));

        return flip ? -Ne : Ne;
    }

    /// <summary>
    /// Computes the ratio of self-masked area to visible area. Used by <see cref="MaskingShadowing(Vector3)"/>.
    /// </summary>
    /// <param name="outDir">View direction, in shading space.</param>
    /// <returns>Ratio of self-masked area to visible area.</returns>
    public float MaskingRatio(Vector3 outDir) {
        float absTanTheta = MathF.Abs(TanTheta(outDir));
        if (float.IsInfinity(absTanTheta)) return 0;
        float alpha = MathF.Sqrt(CosPhiSqr(outDir) * AlphaX * AlphaX + SinPhiSqr(outDir) * AlphaY * AlphaY);
        float alpha2Tan2Theta = alpha * absTanTheta * (alpha * absTanTheta);
        return (-1 + MathF.Sqrt(1 + alpha2Tan2Theta)) / 2;
    }
}