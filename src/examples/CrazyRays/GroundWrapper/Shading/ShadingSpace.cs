using GroundWrapper.Sampling;
using System;
using System.Numerics;

namespace GroundWrapper.Shading {
    /// <summary>
    /// Defines useful functions and operations on directions in shading space.
    /// This is where all conventions of the shading space are defined.
    /// </summary>
    public static class ShadingSpace {
        public static Vector3 WorldToShading(Vector3 shadingNormal, Vector3 worldDirection) {
            shadingNormal = Vector3.Normalize(shadingNormal);
            worldDirection = Vector3.Normalize(worldDirection);

            var (tangent, binormal) = SampleWrap.ComputeBasisVectors(shadingNormal);

            float z = Vector3.Dot(shadingNormal, worldDirection);
            float x = Vector3.Dot(tangent, worldDirection);
            float y = Vector3.Dot(binormal, worldDirection);

            return new Vector3(x, y, z);
        }

        public static Vector3 ShadingToWorld(Vector3 shadingNormal, Vector3 shadingDirection) {
            shadingNormal = Vector3.Normalize(shadingNormal);
            shadingDirection = Vector3.Normalize(shadingDirection);

            var (tangent, binormal) = SampleWrap.ComputeBasisVectors(shadingNormal);
            Vector3 dir = shadingDirection.Z * shadingNormal
                        + shadingDirection.X * tangent
                        + shadingDirection.Y * binormal;
            return dir;
        }

        public static float CosTheta(Vector3 direction)
            => direction.Z;
        public static float CosThetaSqr(Vector3 direction)
            => direction.Z * direction.Z;
        public static float AbsCosTheta(Vector3 direction)
            => MathF.Abs(direction.Z);

        public static float SinThetaSqr(Vector3 direction)
            => MathF.Max(0, 1 - CosThetaSqr(direction));
        public static float SinTheta(Vector3 direction)
            => MathF.Sqrt(SinThetaSqr(direction));
        public static float TanTheta(Vector3 direction)
            => SinTheta(direction) / CosTheta(direction);
        public static float TanThetaSqr(Vector3 direction)
            => SinThetaSqr(direction) / CosThetaSqr(direction);

        public static float CosPhi(Vector3 direction) {
            float sinTheta = SinTheta(direction);
            return sinTheta == 0 ? 1 : Math.Clamp(direction.X / sinTheta, -1, 1);
        }
        public static float SinPhi(Vector3 direction) {
            float sinTheta = SinTheta(direction);
            return sinTheta == 0 ? 0 : Math.Clamp(direction.Y / sinTheta, -1, 1);
        }

        public static float CosPhiSqr(Vector3 direction)
            => CosPhi(direction) * CosPhi(direction);

        public static float SinPhiSqr(Vector3 direction)
            => SinPhi(direction) * SinPhi(direction);

        /// <summary>
        /// Projects two directions onto the horizontal shading plane and computes the 
        /// cosine between the two. (i.e., cos(|phiA - phiB|) )
        /// </summary>
        /// <param name="dirA">A shading space direction.</param>
        /// <param name="dirB">A shading space direction.</param>
        /// <returns>Cosine of the difference in phi between the two directions.</returns>
        public static float CosDeltaPhi(Vector3 dirA, Vector3 dirB) {
            float lenSqrA = dirA.X * dirA.X + dirA.Y * dirA.Y;
            float lenSqrB = dirB.X * dirB.X + dirB.Y * dirB.Y;

            // Prevent NaNs if either vector's 2D projection is the 2D zero vector
            if (lenSqrA == 0.0 || lenSqrB == 0.0) return 1.0f;

            return Math.Clamp(
                (dirA.X * dirB.X + dirA.Y * dirB.Y) / MathF.Sqrt(lenSqrA * lenSqrB),
                -1, 1);
        }

        public static Vector3 Reflect(Vector3 outDir, Vector3 normal)
            => -outDir + 2 * Vector3.Dot(outDir, normal) * normal;

        /// <summary>
        /// Computes the specular refraction of a direction about a normal in shading space.
        /// </summary>
        /// <param name="inDir">Direction that is refracted</param>
        /// <param name="normal">The normal about which to refract (can be e.g. a shading or microfacet normal)</param>
        /// <param name="eta">The ratio of IORs</param>
        /// <returns>Refracted direction, or null in case of total reflection.</returns>
        public static Vector3? Refract(Vector3 inDir, Vector3 normal, float eta) {
            // Compute cosine using Snell's law
            float cosThetaI = Vector3.Dot(normal, inDir);
            float sin2ThetaI = MathF.Max(0, 1 - cosThetaI * cosThetaI);
            float sin2ThetaT = eta * eta * sin2ThetaI;

            // Handle total internal reflection for transmission
            if (sin2ThetaT >= 1) return null;
            float cosThetaT = MathF.Sqrt(1 - sin2ThetaT);
            var wt = eta * -inDir + (eta * cosThetaI - cosThetaT) * normal;
            return wt;
        }

        /// <summary>
        /// Tests if the two directions are in the same hemisphere w.r.t the shading normal.
        /// </summary>
        /// <param name="dirA"></param>
        /// <param name="dirB"></param>
        /// <returns>True if the sign of the cosine to the normal is the same for both.</returns>
        public static bool SameHemisphere(Vector3 dirA, Vector3 dirB)
            => dirA.Z * dirB.Z > 0;
    }
}
