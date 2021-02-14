using System;
using System.Numerics;

namespace SeeSharp.Sampling {
    /// <summary>
    /// Small and fast random number generator based on MWC64X
    /// http://cas.ee.ic.ac.uk/people/dt10/research/rngs-gpu-mwc64x.html
    /// </summary>
    public class RNG {
        public RNG(ulong seed = 0xAB1200CF8190) {
            state = seed;
        }

        public float NextFloat(float min, float max) {
            float r = NextFloat();
            return min * (1 - r) + max * r;
        }

        public float NextFloat() {
            float val = (float)MWC64X() / 0xFFFFFFFF;

            // Ensure that neither exact 0 nor exact 1 are ever returned.
            // This avoids annoying checks everywhere in the renderer.
            val = Math.Max(val, float.Epsilon);
            val = Math.Min(val, 1.0f - float.Epsilon);

            return val;
        }

        public Vector2 NextFloat2D()
        => new Vector2(NextFloat(), NextFloat());

        public Vector3 NextFloat3D()
        => new Vector3(NextFloat(), NextFloat(), NextFloat());

        /// <summary>Random number from min (inclusive) to max (exclusive)</summary>
        public int NextInt(int min, int max) {
            if (max <= min)
                return min;

            var delta = (ulong)max - (ulong)min;
            return (int)(MWC64X() % delta) + min;
        }

        public void Discard(int n) {
            for (int i = 0; i < n; ++i) MWC64X();
        }

        ulong state;

        uint MWC64X() {
            var c = (uint)(state >> 32);
            var x = (uint)(state & 0xFFFFFFFF);
            state = x * ((ulong)4294883355U) + c;
            return x^c;
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
        public static uint HashSeed(uint BaseSeed,
            uint chainIndex, uint sampleIndex) {
            var h1 = FnvHash(FnvHash(0x811C9DC5, BaseSeed), chainIndex);
            var h2 = FnvHash(FnvHash(0x811C9DC5, h1), sampleIndex);
            return h2;
        }
    }
}