#pragma once

#include <cstdint>

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
T Clamp(T a, T b, T c) {
    return (a < b) ? b : ((a > c) ? c : a);
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

#define AssertNormalized(x) CheckNormalized(x, __FILE__, __LINE__)

template <typename T>
inline void CheckNormalized(const T& n, const char* file, int line) {
#ifdef CHECK_NORMALS
    const float len = length(n);
    const float tolerance = 0.001f;
    if (len < 1.0f - tolerance || len > 1.0f + tolerance) {
        std::cerr << "Vector not normalized in " << file << ", line " << line << std::endl;
        abort();
    }
#endif
}


} // namespace ground