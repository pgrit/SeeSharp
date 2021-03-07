using System;
using System.Numerics;

namespace SeeSharp.Sampling {
    /// <summary>
    /// Uniform random number generator. Wrapper around System.Random.
    /// </summary>
    public class RNG {
        Random rng;

        public RNG(ulong seed = 0xAB1200CF8190) {
            rng = new Random((int)seed);
        }

        public float NextFloat(float min, float max) {
            float r = NextFloat();
            return min * (1 - r) + max * r;
        }

        public float NextFloat() {
            return (float)rng.NextDouble();
        }

        public Vector2 NextFloat2D()
        => new Vector2(NextFloat(), NextFloat());

        public Vector3 NextFloat3D()
        => new Vector3(NextFloat(), NextFloat(), NextFloat());

        /// <summary>Random number from min (inclusive) to max (exclusive)</summary>
        public int NextInt(int min, int max) {
            if (max <= min)
                return min;

            return rng.Next(min, max);
        }

        /// <summary> Hashes 4 bytes using FNV </summary>
        private static uint FnvHash(uint h, uint d) {
            h = (h * 16777619) ^ (d        & 0xFF);
            h = (h * 16777619) ^ ((d >>  8) & 0xFF);
            h = (h * 16777619) ^ ((d >> 16) & 0xFF);
            h = (h * 16777619) ^ ((d >> 24) & 0xFF);
            return h;
        }

        /// <summary> Computes a new seed by hashing. </summary>
        /// <param name="chainIndex">e.g., a pixel index</param>
        /// <param name="sampleIndex">current sample within the, e.g., pixel</param>
        public static uint HashSeed(uint BaseSeed, uint chainIndex, uint sampleIndex) {
            var h1 = FnvHash(FnvHash(0x811C9DC5, BaseSeed), chainIndex);
            var h2 = FnvHash(FnvHash(0x811C9DC5, h1), sampleIndex);
            return h2;
        }
    }
}