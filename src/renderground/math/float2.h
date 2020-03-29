#pragma once

namespace ground {

struct Float2 {
    float x, y;

    Float2(float x, float y) : x(x), y(y) {}
    Float2(float s = 0) : Float2(s, s) {}
};

inline Float2 operator+ (const Float2& a, const Float2& b) {
    return Float2 { a.x + b.x, a.y + b.y };
}

inline Float2 operator* (const Float2& v, float s) {
    return Float2 { v.x * s, v.y * s};
}

inline Float2 operator* (float s, const Float2& v) {
    return v * s;
}

} // namespace ground
