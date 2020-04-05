using System;

namespace Ground {
    /// <summary>
    /// Small and fast random number generator based on MWC64X
    /// http://cas.ee.ic.ac.uk/people/dt10/research/rngs-gpu-mwc64x.html
    /// </summary>
    class RNG {
        public RNG(UInt64 seed = 0) {
            this.state = seed;
        }

        public float NextFloat(float min, float max) {
            float r = NextFloat();
            return min * (1 - r) + max * r;
        }

        public float NextFloat() {
            float val = (float)MWC64X() / (float)0xFFFFFFFF;

            // Ensure that neither exact 0 nor exact 1 are ever returned.
            // This avoids annoying checks everywhere in the renderer.
            val = Math.Max(val, float.Epsilon);
            val = Math.Min(val, 1.0f - float.Epsilon);

            return val;
        }

        // Random number from min (inclusive) to max (exclusive)
        public int NextInt(int min, int max) {
            if (max == min)
                return min;

            var delta = ((UInt64)max - (UInt64)min);
            return (int)(MWC64X() % delta) + min;
        }

        public void Discard(int n) {
            for (int i = 0; i < n; ++i) MWC64X();
        }

        UInt64 state;

        UInt32 MWC64X() {
            var c = (UInt32)(state >> 32);
            var x = (UInt32)(state & 0xFFFFFFFF);
            state = x * ((UInt64)4294883355U) + (UInt64)c;
            return x^c;
        }

        /// <summary> Hashes 4 bytes using FNV </summary>
        private static UInt32 FnvHash(UInt32 h, UInt32 d) {
            h = (h * 16777619) ^ ( d        & 0xFF);
            h = (h * 16777619) ^ ((d >>  8) & 0xFF);
            h = (h * 16777619) ^ ((d >> 16) & 0xFF);
            h = (h * 16777619) ^ ((d >> 24) & 0xFF);
            return h;
        }

        /// <summary> Computes a new seed by hashing. </summary>
        /// <param name="chainIndex">e.g., a pixel index</param>
        /// <param name="sampleIndex">current sample within the, e.g., pixel</param>
        public static UInt32 HashSeed(UInt32 BaseSeed,
            UInt32 chainIndex, UInt32 sampleIndex)
        {
            var h1 = FnvHash(FnvHash(0x811C9DC5, BaseSeed), chainIndex);
            var h2 = FnvHash(FnvHash(0x811C9DC5, h1), sampleIndex);
            return h2;
        }
    }
}