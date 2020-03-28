#pragma once

#include "math/float3.h"

namespace ground {

struct Float4 {
    float x, y, z, w;

    Float4(float x, float y, float z, float w)
    : x(x), y(y), z(z), w(w)
    {}

    Float4(float v = 0.0f)
    : x(v), y(v), z(v), w(v)
    {}

    Float4(const Float3& v, float w)
    : x(v.x), y(v.y), z(v.z), w(w)
    {}

    float& operator[] (int i) {
        if (i == 0) return x;
        else if (i == 1) return y;
        else if (i == 2) return z;
        else return w;
    }

    const float& operator[] (int i) const {
        if (i == 0) return x;
        else if (i == 1) return y;
        else if (i == 2) return z;
        else return w;
    }

    operator Float3() explicit const {
        return Float3(x/w, y/w, z/w);
    }
};

inline float Dot(const Float4& a, const Float4& b) {
    return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
}

inline Float4 operator* (const Float4& a, float b) {
    return Float4(a.x * b, a.y * b, a.z * b, a.w * b);
}

inline Float4 Abs(const Float4& a) {
    return Float4(std::abs(a.x), std::abs(a.y), std::abs(a.z), std::abs(a.w));
}

} // namespace ground