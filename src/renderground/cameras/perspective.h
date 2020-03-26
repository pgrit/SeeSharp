#pragma once

#include "cameras/camera.h"

namespace ground {

class PerspectiveCamera : public Camera {
public:
    PerspectiveCamera(const Transform* transform, float verticalFieldOfView,
        Image* frameBuffer);

    Ray GenerateRay(const Float2& filmSample, const Float2& lensSample, float time) final;

private:
    float verticalFieldOfView;
};

} // namespace ground