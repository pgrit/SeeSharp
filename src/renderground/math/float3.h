#pragma once

#include <cmath>

namespace ground {

struct Float3 {
    float x, y, z;

    Float3(float x, float y, float z) : x(x), y(y), z(z) {}
    Float3(float v = 0.0f) : Float3(v,v,v) {}

    inline float operator[] (int idx) const {
        if (idx == 0) return x;
        else if (idx == 1) return y;
        else return z;
    }

    inline float& operator[] (int idx) {
        if (idx == 0) return x;
        else if (idx == 1) return y;
        else return z;
    }

    Float3& operator *= (float s);
};

inline Float3 operator/ (const Float3& v, float s) {
    return Float3(v.x / s, v.y / s, v.z / s);
}

inline Float3 operator* (const Float3& v, float s) {
    return Float3(v.x * s, v.y * s, v.z * s);
}

inline Float3 operator* (float s, const Float3& v) {
    return Float3(v.x * s, v.y * s, v.z * s);
}

inline Float3 operator+ (const Float3& a, const Float3& b) {
    return Float3(a.x + b.x, a.y + b.y, a.z + b.z);
}

inline Float3 operator- (const Float3& a, const Float3& b) {
    return Float3(a.x - b.x, a.y - b.y, a.z - b.z);
}

inline Float3 operator* (const Float3& a, const Float3& b) {
    return Float3(a.x * b.x, a.y * b.y, a.z * b.z);
}

inline float Dot(const Float3& a, const Float3& b) {
    return a.x * b.x + a.y * b.y + a.z * b.z;
}

inline Float3 Cross(const Float3 a, const Float3 b) {
    return Float3(
        a.y * b.z - a.z * b.y,
        a.z * b.x - a.x * b.z,
        a.x * b.y - a.y * b.x
    );
}

inline float LengthSquared(const Float3& v) {
    return Dot(v, v);
}

inline float Length(const Float3& v) {
    return std::sqrt(LengthSquared(v));
}

inline Float3 Normalize(const Float3& v) {
    return v * (1 / Length(v));
}

inline Float3& Float3::operator *= (float s) {
    *this = (*this) * s;
    return *this;
}

} // namespace ground
