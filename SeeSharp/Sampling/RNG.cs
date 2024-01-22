namespace SeeSharp.Sampling;

/// <summary>
/// Uniform random number generator. Uses PCG and FNV hashing to efficiently generate random numbers
/// even with highly correlated seeds (e.g., consecutive numbers).
/// </summary>
public struct RNG {
    uint state;

    /// <summary>
    /// Creates a new random generator with identical state to the provided random generator.
    /// </summary>
    public RNG(RNG other) {
        state = other.state;
    }

    /// <summary>
    /// Creates a new random generator with either the given seed or a constant default value.
    /// </summary>
    public RNG(uint seed = 0xAB20F90u) => state = seed;

    /// <summary>
    /// Computes a new seed by hashing. Hashes each individual component using PCG to reduce correlation
    /// and then hashes the three resulting PCG hashes using FNV to achieve a single 32 bit value.
    /// </summary>
    /// <param name="baseSeed">A global base seed</param>
    /// <param name="chainIndex">e.g., a pixel index</param>
    /// <param name="sampleIndex">current sample within the, e.g., pixel</param>
    public RNG(uint baseSeed, uint chainIndex, uint sampleIndex)
    : this(HashSeed(baseSeed, chainIndex, sampleIndex)) { }

    /// <summary>
    /// Comptues a random floating point value between two numbers
    /// </summary>
    /// <param name="min">Minimum value (inclusive)</param>
    /// <param name="max">Maximum value (inclusive)</param>
    public float NextFloat(float min, float max) {
        float r = NextFloat();
        return min * (1 - r) + max * r;
    }

    /// <returns>A floating point value in [0,1] (inclusive)</returns>
    public float NextFloat() {
        return Next() / (float)uint.MaxValue;
    }

    /// <returns>A pair of floating point values in [0,1] (inclusive)</returns>
    public Vector2 NextFloat2D()
    => new(NextFloat(), NextFloat());

    /// <returns>A triple of floating point values in [0,1] (inclusive)</returns>
    public Vector3 NextFloat3D()
    => new(NextFloat(), NextFloat(), NextFloat());

    /// <summary>Random number from 0 (inclusive) to max (exclusive)</summary>
    public uint NextInt(uint max) {
        // https://arxiv.org/pdf/1805.10941.pdf
        uint x = Next();
        ulong m = (ulong)x * max;
        uint l = (uint)m;
        if (l < max) {
            uint t = (0u - max) % max;
            while (l < t) {
                x = Next();
                m = (ulong)x * max;
                l = (uint)m;
            }
        }
        return (uint)(m >> 32);
    }

    /// <summary>Random number from 0 (inclusive) to max (exclusive)</summary>
    public int NextInt(int max) {
        Debug.Assert(max > 0);
        return (int)NextInt((uint)max);
    }

    /// <summary>Random number from min (inclusive) to max (exclusive)</summary>
    public int NextInt(int min, int max) {
        Debug.Assert(max > min);
        return min + (int)NextInt((uint)(max - min));
    }

    /// <summary>Random number from min (inclusive) to max (exclusive)</summary>
    public uint NextInt(uint min, uint max) {
        Debug.Assert(max > min);
        return min + (uint)NextInt((uint)(max - min));
    }

    public uint Next() {
        uint word = ((state >> (int)((state >> 28) + 4)) ^ state) * 277803737u;
        state = state * 747796405u + 2891336453u;
        return ((word >> 22) ^ word);
    }

    const uint FnvOffsetBasis = 2166136261;
    const uint FnvPrime = 16777619;

    static uint FnvHash(uint hash, uint data) {
        hash = (hash * FnvPrime) ^ (data & 0xFF);
        hash = (hash * FnvPrime) ^ ((data >> 8) & 0xFF);
        hash = (hash * FnvPrime) ^ ((data >> 16) & 0xFF);
        hash = (hash * FnvPrime) ^ ((data >> 24) & 0xFF);
        return hash;
    }

    static uint PcgHash(uint input) {
        uint state = input * 747796405u + 2891336453u;
        uint word = ((state >> (int)((state >> 28) + 4)) ^ state) * 277803737u;
        return (word >> 22) ^ word;
    }

    static uint HashSeed(uint BaseSeed, uint chainIndex, uint sampleIndex) {
        var hash = FnvHash(FnvOffsetBasis, PcgHash(BaseSeed));
        hash = FnvHash(hash, PcgHash(chainIndex));
        hash = FnvHash(hash, PcgHash(sampleIndex));
        return PcgHash(hash);
    }
}