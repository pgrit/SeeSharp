#include "api/camera.h"
#include "api/internal.h"
#include "cameras/perspective.h"

std::vector<std::unique_ptr<ground::Camera>> globalCameras;

extern "C" {

GROUND_API int CreatePerspectiveCamera(int transformId, float verticalFieldOfView, int frameBufferId) {
    auto transform = globalTransforms[transformId].get();
    auto frameBuffer = globalImages[frameBufferId].get();
    globalCameras.emplace_back(new ground::PerspectiveCamera(transform, verticalFieldOfView, frameBuffer));
    return int(globalCameras.size()) - 1;
}

GROUND_API Ray GenerateCameraRay(int camera, CameraSampleInfo sampleInfo) {
    return globalCameras[camera]->GenerateRay(sampleInfo.filmSample,
        sampleInfo.lensSample, sampleInfo.time);
}

}
