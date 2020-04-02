#pragma once

#include "math/float4x4.h"

namespace ground {

class Transform {
public:
    Transform(const Vector3& pos, const Vector3& rot, const Vector3& scale);

    Vector3 ApplyToDirection(const Vector3& dir) const;
    Vector3 ApplyToPoint(const Vector3& pos) const;
    Vector3 ApplyToNormal(const Vector3& n) const;

    Vector3 InvApplyToDirection(const Vector3& dir) const;
    Vector3 InvApplyToPoint(const Vector3& pos) const;

private:
    Float4x4 matrix;
    Float4x4 inverse;
    Float4x4 inverseTranspose;
};

} // namespace ground