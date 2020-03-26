#pragma once

#include "geometry/ray.h"
#include "geometry/transform.h"
#include "math/float2.h"
#include "image/image.h"

namespace ground {

class Camera {
public:
    Camera(const Transform* transform, Image* frameBuffer)
    : transform(transform), frameBuffer(frameBuffer)
    {}

    virtual ~Camera() {}

    virtual Ray GenerateRay(const Float2& filmSample, const Float2& lensSample, float time) = 0;

protected:
    const Transform* transform;
    Image* frameBuffer;
};

} // namespace ground