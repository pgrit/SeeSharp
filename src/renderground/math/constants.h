#pragma once

#include <cstdint>
#include <iostream>

namespace ground {

constexpr float PI = 3.1415926f;

inline float DegreesToRadians(float x) {
    return x * PI / 180.0f;
}

inline float RadiansToDegrees(float x) {
    return x * 180.0f / PI;
}

inline int FloatAsInt(float f) {
    union { float vf; int vi; } v;
    v.vf = f;
    return v.vi;
}

inline float IntAsFloat(int i) {
    union { float vf; int vi; } v;
    v.vi = i;
    return v.vf;
}

template <typename T>
T Clamp(T value, T min, T max) {
    return (value < min) ? min : ((value > max) ? max : value);
}

template <typename T, typename U>
T Lerp(T a, T b, U u) {
    return a * (1 - u) + b * u;
}

template <typename T, typename U>
T Lerp(T a, T b, T c, U u, U v) {
    return a * (1 - u - v) + b * u + c * v;
}

template <typename T>
T Reflect(T v, T n) {
    return v - (2 * dot(n, v)) * n;
}

template <typename T>
inline void _CheckNormalized(const T& n, const char* file, int line) {
#ifdef SANITY_CHECKS
    const float len = Length(n);
    const float tolerance = 0.001f;
    if (len < 1.0f - tolerance || len > 1.0f + tolerance) {
        std::cerr << "Vector not normalized in " << file << ", line " << line << std::endl;
        abort();
    }
#endif
}
#define CheckNormalized(x) _CheckNormalized(x, __FILE__, __LINE__)

inline void _CheckFloatEqual(float a, float b, const char* file, int line) {
#ifdef SANITY_CHECKS
    const float tolerance = 0.001f;
    if (a < b - tolerance || a > b + tolerance) {
        std::cerr << "Value not equal in " << file << ", line " << line << std::endl;
        abort();
    }
#endif
}
#define CheckFloatEqual(a, b) _CheckFloatEqual(a, b, __FILE__, __LINE__)

} // namespace ground