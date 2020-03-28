#pragma once

#include "math/float2.h"
#include "math/float3.h"

namespace ground {

struct SurfacePoint {
    Float3 position;
    Float3 normal;
    Float2 barycentricCoords;
    unsigned int geomId;
    unsigned int primId;
};

struct Hit {
    SurfacePoint point;
    float distance;
    float errorOffset;
};

} // namespace ground
