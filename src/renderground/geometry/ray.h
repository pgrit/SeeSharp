#ifndef RENDERGROUND_GEOMETRY_RAY_H
#define RENDERGROUND_GEOMETRY_RAY_H

#include "geometry/float3.h"

namespace ground {

struct Ray {
    Float3 origin;
    Float3 direction;
};

} // namespace ground

#endif // RENDERGROUND_GEOMETRY_RAY_H