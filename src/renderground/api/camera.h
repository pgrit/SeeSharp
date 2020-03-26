#pragma once

#include <renderground/api/api.h>

extern "C" {

// The perspective camera is positioned at the origin and pointing along
// the z axis by default.
GROUND_API int CreatePerspectiveCamera(int transformId, float verticalFieldOfView, int frameBufferId);

// Stores primary space sample values for a camera sample query.
struct GROUND_API CameraSampleInfo {
    Vector2 filmSample;
    Vector2 lensSample;
    float time;
};

GROUND_API Ray GenerateCameraRay(int camera, CameraSampleInfo sampleInfo);

}
