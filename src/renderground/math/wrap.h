#pragma once

#include <cmath>

namespace ground
{

inline void WrapToUniformTriangle(float rnd1, float rnd2, float& u, float& v) {
    float sqrtRnd1 = std::sqrt(rnd1);
    u = 1.0f - sqrtRnd1;
    v = rnd2 * sqrtRnd1;
}

} // namespace ground
