#pragma once

#include <renderground/api/api.h>
#include <cstdint>
#include <cmath>

inline Vector3 operator/ (const Vector3& v, float s) {
    return Vector3 {v.x / s, v.y / s, v.z / s };
}

inline Vector3 operator* (const Vector3& v, float s) {
    return Vector3 {v.x * s, v.y * s, v.z * s};
}

inline Vector3 operator* (float s, const Vector3& v) {
    return Vector3 {v.x * s, v.y * s, v.z * s};
}

inline Vector3 operator+ (const Vector3& a, const Vector3& b) {
    return Vector3 {a.x + b.x, a.y + b.y, a.z + b.z};
}

inline Vector3 operator- (const Vector3& a, const Vector3& b) {
    return Vector3 {a.x - b.x, a.y - b.y, a.z - b.z};
}

inline Vector3 operator- (const Vector3& a) {
    return Vector3 {-a.x, -a.y, -a.z};
}

inline Vector3 operator* (const Vector3& a, const Vector3& b) {
    return Vector3 {a.x * b.x, a.y * b.y, a.z * b.z};
}

inline float& GetAxis(Vector3& v, int i) {
    if (i == 0) return v.x;
    else if (i == 1) return v.y;
    else return v.z;
}

inline const float& GetAxis(const Vector3& v, int i) {
    if (i == 0) return v.x;
    else if (i == 1) return v.y;
    else return v.z;
}

inline float Dot(const Vector3& a, const Vector3& b) {
    return a.x * b.x + a.y * b.y + a.z * b.z;
}

inline Vector3 Cross(const Vector3 a, const Vector3 b) {
    return Vector3{
        a.y * b.z - a.z * b.y,
        a.z * b.x - a.x * b.z,
        a.x * b.y - a.y * b.x
    };
}

inline float LengthSquared(const Vector3& v) {
    return Dot(v, v);
}

inline float Length(const Vector3& v) {
    return std::sqrt(LengthSquared(v));
}

inline Vector3 Normalize(const Vector3& v) {
    return v * (1 / Length(v));
}

inline ColorRGB operator* (const ColorRGB& a, const ColorRGB& b) {
    return ColorRGB { a.r * b.r, a.g * b.g, a.b * b.b };
}

inline ColorRGB operator* (const ColorRGB& a, float s) {
    return ColorRGB { a.r * s, a.g * s, a.b * s };
}

inline ColorRGB operator* (float s, const ColorRGB& a) {
    return a * s;
}

inline ColorRGB operator+ (const ColorRGB& a, const ColorRGB& b) {
    return ColorRGB { a.r + b.r, a.g + b.g, a.b + b.b };
}

inline ColorRGB operator+ (const ColorRGB& a, float s) {
    return ColorRGB { a.r + s, a.g + s, a.b + s };
}

inline ColorRGB operator+ (float s, const ColorRGB& a) {
    return a + s;
}

inline Vector2 operator+ (const Vector2& a, const Vector2& b) {
    return Vector2 { a.x + b.x, a.y + b.y };
}

inline Vector2 operator* (const Vector2& v, float s) {
    return Vector2 { v.x * s, v.y * s};
}

inline Vector2 operator* (float s, const Vector2& v) {
    return v * s;
}

// Small and fast random number generator based on MWC64X
// http://cas.ee.ic.ac.uk/people/dt10/research/rngs-gpu-mwc64x.html
class RNG {
public:
    RNG(uint64_t seed = 0) : state(seed) {}

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
    uint64_t state;

    uint32_t MWC64X() {
        const uint32_t c = (state) >> 32;
        const uint32_t x = (state) & 0xFFFFFFFF;
        state = x * ((uint64_t)4294883355U) + c;
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