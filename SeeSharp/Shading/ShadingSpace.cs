namespace SeeSharp.Shading;

/// <summary>
/// Defines useful functions and operations on directions in shading space.
/// This is where all conventions of the shading space are defined.
/// </summary>
public static class ShadingSpace {
    /// <summary>
    /// Computes an orthogonal tangent and binormal for a given normal vector.
    /// </summary>
    /// <param name="normal">The surface normal</param>
    /// <param name="tangent">A computed tangent</param>
    /// <param name="binormal">A computed binormal</param>
    public static void ComputeBasisVectors(in Vector3 normal, out Vector3 tangent, out Vector3 binormal) {
        if (Math.Abs(normal.X) > Math.Abs(normal.Y)) {
            float denom = MathF.Sqrt(normal.X * normal.X + normal.Z * normal.Z);
            tangent = new Vector3(-normal.Z, 0.0f, normal.X) / denom;
        } else {
            float denom = MathF.Sqrt(normal.Y * normal.Y + normal.Z * normal.Z);
            tangent = new Vector3(0.0f, normal.Z, -normal.Y) / denom;
        }
        binormal = Vector3.Cross(normal, tangent);
    }

    /// <summary>
    /// Trnasforms the given direction into normalized shading space.
    /// Assumes that both the direction and the shading normal are normalized.
    /// </summary>
    public static Vector3 WorldToShading(in Vector3 shadingNormal, in Vector3 worldDirection) {
        SanityChecks.IsNormalized(worldDirection);

        ComputeBasisVectors(shadingNormal, out var tangent, out var binormal);
        float z = Vector3.Dot(shadingNormal, worldDirection);
        float x = Vector3.Dot(tangent, worldDirection);
        float y = Vector3.Dot(binormal, worldDirection);

        return new Vector3(x, y, z);
    }

    public static Vector3 WorldToShading(in Vector3 shadingNormal, in Vector3 tangent, in Vector3 binormal, in Vector3 worldDirection) {
        SanityChecks.IsNormalized(worldDirection);

        float z = Vector3.Dot(shadingNormal, worldDirection);
        float x = Vector3.Dot(tangent, worldDirection);
        float y = Vector3.Dot(binormal, worldDirection);

        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Trnasforms the given direction from shading space into world space and normalizes it.
    /// Assumes the shading normal is a valid normal (i.e. normalized).
    /// </summary>
    public static Vector3 ShadingToWorld(in Vector3 shadingNormal, in Vector3 shadingDirection) {
        SanityChecks.IsNormalized(shadingDirection);

        ComputeBasisVectors(shadingNormal, out var tangent, out var binormal);
        Vector3 dir = shadingDirection.Z * shadingNormal
                    + shadingDirection.X * tangent
                    + shadingDirection.Y * binormal;
        return dir;
    }

    public static Vector3 ShadingToWorld(in Vector3 shadingNormal, in Vector3 tangent, in Vector3 binormal, in Vector3 shadingDirection) {
        SanityChecks.IsNormalized(shadingDirection);

        Vector3 dir = shadingDirection.Z * shadingNormal
                    + shadingDirection.X * tangent
                    + shadingDirection.Y * binormal;
        return dir;
    }

    /// <param name="direction">A direction in shading space</param>
    /// <returns>The cosine of the angle to the normal</returns>
    public static float CosTheta(in Vector3 direction) => direction.Z;

    /// <returns>Square of the cosine of the angle between the direction and the normal</returns>
    public static float CosThetaSqr(in Vector3 direction) => direction.Z * direction.Z;

    /// <returns>Absolute value of the cosine of the angle between the direction and the normal</returns>
    public static float AbsCosTheta(in Vector3 direction) => MathF.Abs(direction.Z);

    public static float SinThetaSqr(in Vector3 direction) => MathF.Max(0, 1 - CosThetaSqr(direction));
    public static float SinTheta(in Vector3 direction) => MathF.Sqrt(SinThetaSqr(direction));
    public static float TanTheta(in Vector3 direction) => SinTheta(direction) / CosTheta(direction);
    public static float TanThetaSqr(in Vector3 direction) => SinThetaSqr(direction) / CosThetaSqr(direction);

    public static float CosPhi(in Vector3 direction) {
        float sinTheta = SinTheta(direction);
        return sinTheta == 0 ? 1 : Math.Clamp(direction.X / sinTheta, -1, 1);
    }

    public static float SinPhi(in Vector3 direction) {
        float sinTheta = SinTheta(direction);
        return sinTheta == 0 ? 0 : Math.Clamp(direction.Y / sinTheta, -1, 1);
    }

    public static float CosPhiSqr(in Vector3 direction) {
        float c = CosPhi(direction);
        return c * c;
    }

    public static float SinPhiSqr(in Vector3 direction) {
        float s = SinPhi(direction);
        return s * s;
    }

    /// <returns>The perfect mirror reflection of outDir about the normal</returns>
    public static Vector3 Reflect(in Vector3 outDir, in Vector3 normal)
    => -outDir + 2 * Vector3.Dot(outDir, normal) * normal;

    /// <summary>
    /// Computes the specular refraction of a direction about a normal in shading space.
    /// </summary>
    /// <param name="inDir">Direction that is refracted</param>
    /// <param name="normal">
    ///     The normal about which to refract (can be e.g. a shading or microfacet normal)
    /// </param>
    /// <param name="eta">The ratio of IORs</param>
    /// <returns>Refracted direction, or null in case of total reflection.</returns>
    public static Vector3? Refract(in Vector3 inDir, in Vector3 normal, float eta) {
        // Compute cosine using Snell's law
        float cosThetaI = Vector3.Dot(normal, inDir);
        float sin2ThetaI = MathF.Max(0, 1 - cosThetaI * cosThetaI);
        float sin2ThetaT = eta * eta * sin2ThetaI;

        if (sin2ThetaT >= 1) return null; // total internal reflection

        float cosThetaT = MathF.Sqrt(1 - sin2ThetaT);
        var wt = eta * -inDir + (eta * cosThetaI - cosThetaT) * normal;
        return wt;
    }

    /// <summary>
    /// Tests if the two directions are in the same hemisphere w.r.t the shading normal.
    /// </summary>
    /// <param name="dirA">A direction in shading space</param>
    /// <param name="dirB">Another direction in shading space</param>
    /// <returns>True if the sign of the cosine to the normal is the same for both.</returns>
    public static bool SameHemisphere(in Vector3 dirA, in Vector3 dirB) => dirA.Z * dirB.Z > 0;
}
