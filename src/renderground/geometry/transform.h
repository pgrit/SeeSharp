#pragma once

#include "math/float3.h"
#include "math/float4x4.h"

namespace ground {

class Transform {
public:
    Transform(const Float3& pos, const Float3& rot, const Float3& scale);

    Float3 ApplyToDirection(const Float3& pos) const;
    Float3 ApplyToPoint(const Float3& pos) const;
    Float3 ApplyToNormal(const Float3& pos) const;

    Float3 InvApplyToDirection(const Float3& pos) const;
    Float3 InvApplyToPoint(const Float3& pos) const;
    Float3 InvApplyToNormal(const Float3& pos) const;

private:
    Float4x4 matrix;
    Float4x4 inverse;
    Float4x4 inverseTranspose;
};

} // namespace ground