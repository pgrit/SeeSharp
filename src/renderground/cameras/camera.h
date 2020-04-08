#pragma once

#include "geometry/transform.h"
#include "image/image.h"

namespace ground {

class Camera {
public:
    Camera(const Transform* transform, Image* frameBuffer)
    : transform(transform), frameBuffer(frameBuffer)
    {}

    virtual ~Camera() {}

    virtual Ray GenerateRay(const Vector2& filmSample, const Vector2& lensSample, float time) const = 0;
    virtual Vector2 WorldToFilm(const Vector3& worldSpacePoint) const = 0;

protected:
    const Transform* transform;
    Image* frameBuffer;
};

} // namespace ground