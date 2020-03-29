#pragma once

#include <renderground/api/api.h>

inline Vector3 operator- (const Vector3& a) {
    return Vector3 { -a.x, -a.y, -a.z };
}

inline Vector3 operator+ (const Vector3& a, const Vector3& b) {
    return Vector3 { a.x + b.x, a.y + b.y, a.z + b.z };
}

inline Vector3 operator- (const Vector3& a, const Vector3& b) {
    return a + (-b);
}

inline Vector3 operator* (const Vector3& a, float s) {
    return Vector3 { a.x * s, a.y * s, a.z * s };
}

inline Vector3 operator/ (const Vector3& a, float s) {
    return a * (1.0f / s);
}

inline Vector3 operator* (float s, const Vector3& a) {
    return a * s;
}

inline float Dot (const Vector3& a, const Vector3& b) {
    return a.x * b.x + a.y * b.y + a.z * b.z;
}

inline float LengthSquared(const Vector3& v) {
    return Dot(v, v);
}

inline float Length(const Vector3& v) {
    return std::sqrt(LengthSquared(v));
}

// Small and fast random number generator based on MWC64X
// http://cas.ee.ic.ac.uk/people/dt10/research/rngs-gpu-mwc64x.html
class RNG {
public:
    RNG(uint64_t seed = 0) : state_(seed) {}

    float NextFloat(float min, float max) {
        const float r = NextFloat();
        return min * (1 - r) + max * r;
    }

    float NextFloat() {
        return static_cast<float>(MWC64X()) / static_cast<float>(0xFFFFFFFF);
    }

    // Random number from min (inclusive) to max (exclusive)
    int NextInt(int min, int max) {
        return max == min ? min : MWC64X() % (max - min) + min;
    }

    void Discard(int n) {
        for (int i = 0; i < n; ++i) MWC64X();
    }

private:
    uint64_t state_;

    uint32_t MWC64X() {
        const uint32_t c = (state_) >> 32;
        const uint32_t x = (state_) & 0xFFFFFFFF;
        state_ = x * ((uint64_t)4294883355U) + c;
        return x^c;
    }
};

// Hashes 4 bytes using FNV
inline uint32_t FnvHash(uint32_t h, uint32_t d) {
    h = (h * 16777619) ^ ( d        & 0xFF);
    h = (h * 16777619) ^ ((d >>  8) & 0xFF);
    h = (h * 16777619) ^ ((d >> 16) & 0xFF);
    h = (h * 16777619) ^ ((d >> 24) & 0xFF);
    return h;
}

inline uint32_t HashSeed(uint32_t BaseSeed, uint32_t chainIndex) {
    return FnvHash(FnvHash(0x811C9DC5, BaseSeed), chainIndex);
}