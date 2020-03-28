#pragma once

#include "cameras/camera.h"

namespace ground {

class PerspectiveCamera : public Camera {
public:
    PerspectiveCamera(const Transform* transform, float verticalFieldOfView,
        Image* frameBuffer, float nearClip=0.1f, float farClip=10000.0f);

    Ray GenerateRay(const Float2& filmSample, const Float2& lensSample, float time) final;

private:
    Float4x4 localToView;
    Float4x4 viewToLocal;

    Float4x4 viewToRaster;
    Float4x4 rasterToView;
};

} // namespace ground