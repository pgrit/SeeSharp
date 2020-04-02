#pragma once

#include <cmath>

#include "math/constants.h"

namespace ground
{

inline void ComputeBasisVectors(const Vector3& normal, Vector3& tangentOut, Vector3& binormalOut) {
    const int   id0 = (std::abs(normal.x) > std::abs(normal.y)) ?    0 : 1;
    const int   id1 = (std::abs(normal.x) > std::abs(normal.y)) ?    1 : 0;
    const float sig = (std::abs(normal.x) > std::abs(normal.y)) ? -1.f : 1.f;

    const float invLen = 1.f / std::sqrt(GetAxis(normal, id0) * GetAxis(normal, id0) + normal.z * normal.z);

    GetAxis(tangentOut, id0) = normal.z * sig * invLen;
    GetAxis(tangentOut, id1) = 0.f;
    tangentOut.z = GetAxis(normal, id0) * -1.f * sig * invLen;

    binormalOut = Cross(normal, tangentOut);

    tangentOut = Normalize(tangentOut);
    binormalOut = Normalize(binormalOut);
}

inline void WrapToUniformTriangle(float rnd1, float rnd2, float& u, float& v) {
    float sqrtRnd1 = std::sqrt(rnd1);
    u = 1.0f - sqrtRnd1;
    v = rnd2 * sqrtRnd1;
}

inline Vector3 SphericalToCartesian(float sintheta, float costheta, float phi) {
    return Vector3 {
        sintheta * cosf(phi),
        sintheta * sinf(phi),
        costheta
    };
}

struct DirectionSample {
    Vector3 direction;
    float jacobian;
};

// Wraps the primary sample space on the cosine weighted hemisphere.
// The hemisphere is centered about the positive "z" axis.
inline DirectionSample WrapToCosHemisphere(const Vector2& primary) {
    const Vector3 local_dir = SphericalToCartesian(
        std::sqrtf(1 - primary.y),
        std::sqrtf(primary.y),
        2.f * PI * primary.x);

    return DirectionSample{local_dir, local_dir.z / PI};
}

inline float ComputeCosHemisphereJacobian(float cosine) {
    return std::abs(cosine) / PI;
}

} // namespace ground
