#pragma once

#include <cmath>

#include "math/constants.h"

namespace ground
{

inline void ComputeBasisVectors(const Float3& normal, Float3& tangentOut, Float3& binormalOut) {
    const int   id0 = (std::abs(normal.x) > std::abs(normal.y)) ?    0 : 1;
    const int   id1 = (std::abs(normal.x) > std::abs(normal.y)) ?    1 : 0;
    const float sig = (std::abs(normal.x) > std::abs(normal.y)) ? -1.f : 1.f;

    const float invLen = 1.f / std::sqrt(normal[id0] * normal[id0] + normal.z * normal.z);

    tangentOut[id0] = normal.z * sig * invLen;
    tangentOut[id1] = 0.f;
    tangentOut.z    = normal[id0] * -1.f * sig * invLen;

    binormalOut = Cross(normal, tangentOut);

    tangentOut = Normalize(tangentOut);
    binormalOut = Normalize(binormalOut);
}

inline void WrapToUniformTriangle(float rnd1, float rnd2, float& u, float& v) {
    float sqrtRnd1 = std::sqrt(rnd1);
    u = 1.0f - sqrtRnd1;
    v = rnd2 * sqrtRnd1;
}

inline Float3 SphericalToCartesian(float sintheta, float costheta, float phi) {
    return Float3(sintheta * cosf(phi),
                  sintheta * sinf(phi),
                  costheta);
}

struct DirectionSample {
    Float3 direction;
    float jacobian;
};

// Wraps the primary sample space on the cosine weighted hemisphere.
// The hemisphere is centered about the positive "z" axis.
inline DirectionSample WrapToCosHemisphere(const Float2& primary) {
    const Float3 local_dir = SphericalToCartesian(
        std::sqrtf(1 - primary.y),
        std::sqrtf(primary.y),
        2.f * PI * primary.x);

    return DirectionSample{local_dir, local_dir.z / PI};
}

inline float ComputeCosHemisphereJacobian(float cosine) {
    return std::abs(cosine) / PI;
}

} // namespace ground
