#include "cameras/perspective.h"

namespace ground {

PerspectiveCamera::PerspectiveCamera(const Transform* transform,
    float verticalFieldOfView, Image* frameBuffer)
: Camera(transform, frameBuffer)
, verticalFieldOfView(verticalFieldOfView)
{

}

Ray PerspectiveCamera::GenerateRay(const Float2& filmSample, const Float2& lensSample, float time) {
    Ray r;
    return r;
}

} // namespace ground