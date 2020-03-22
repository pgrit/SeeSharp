#include <iostream>

#include "renderground.h"
#include "geometry/geometry.h"

static ground::Scene globalScene;

extern "C" {

GROUND_API void InitScene() {

}

GROUND_API int AddTriangleMesh(const float* vertices, int numVerts,
    const int* indices, int numIdx)
{
    return globalScene.AddMesh(ground::Mesh(
        reinterpret_cast<const ground::Float3*>(vertices), numVerts,
        indices, numIdx));
}

GROUND_API void FinalizeScene() {
    globalScene.Finalize();
}

GROUND_API Hit TraceSingle(const float* pos, const float* dir) {
    return Hit { 13 };
}

GROUND_API int CreateImage(int width, int height, int numChannels) {
    return 0;
}

GROUND_API void AddSplat(int image, float x, float y, const float* value) {

}

GROUND_API void WriteImage(int image, const char* filename) {

}

}