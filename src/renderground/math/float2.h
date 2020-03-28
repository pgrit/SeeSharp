#pragma once

namespace ground {

struct Float2 {
    float x, y;

    Float2(float x, float y) : x(x), y(y) {}
    Float2(float s = 0) : Float2(s, s) {}
};

} // namespace ground
