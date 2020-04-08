#include "cameras/perspective.h"
#include "api/cpputils.h"

namespace ground {

PerspectiveCamera::PerspectiveCamera(const Transform* transform,
    float verticalFieldOfView, Image* frameBuffer, float nearClip, float farClip)
: Camera(transform, frameBuffer)
{
    float aspectRatio = float(frameBuffer->height) / float(frameBuffer->width);
    const float fovRadians = DegreesToRadians(verticalFieldOfView);

    // Compute prespective projection matrix and its inverse
    localToView = Perspective(fovRadians, aspectRatio, nearClip, farClip);
    viewToLocal = Invert(localToView);

    // Mapping from [-1,1]x[-1,1]x[-1,1] cube (after perspective projection) to image space
    viewToRaster = Scale(frameBuffer->width * 0.5f, frameBuffer->height * 0.5f, 0.0f)
                 * Translate(1.0f, 1.0f, 0.0f);
    rasterToView = Translate(-1.0f, -1.0f, 0.0f)
                 * Scale(2.0f / frameBuffer->width, 2.0f / frameBuffer->height, 0.0f);
}

Ray PerspectiveCamera::GenerateRay(const Vector2& filmSample, const Vector2& lensSample, float time) {
    Vector3 origin{ 0, 0, 0 };

    // Map pixel coordinates to the local space of the camera
    Float4 raster(filmSample.x, filmSample.y, 0, 1);
    Float4 view = rasterToView * raster;
    Float4 local = viewToLocal * view;
    local.z = -local.z;

    Vector3 direction(local);

    // Apply the world space transformation
    origin = transform->ApplyToPoint(origin);
    direction = transform->ApplyToDirection(direction);

    return Ray {
        origin,
        Normalize(direction),
        0.0f
    };
}

} // namespace ground