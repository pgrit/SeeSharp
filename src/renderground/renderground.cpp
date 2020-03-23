#include <iostream>

#include "renderground.h"
#include "geometry/geometry.h"
#include "image/image.h"

static ground::Scene globalScene;

extern "C" {

GROUND_API void InitScene() {
    globalScene.Init();
}

GROUND_API int AddTriangleMesh(const float* vertices, int numVerts,
    const int* indices, int numIdx)
{
    // TODO ensure that numIdx is a multiple of 3

    return globalScene.AddMesh(ground::Mesh(
        reinterpret_cast<const ground::Float3*>(vertices), numVerts,
        indices, numIdx));
}

GROUND_API void FinalizeScene() {
    globalScene.Finalize();
}

GROUND_API Hit TraceSingle(const float* pos, const float* dir) {
    ground::Ray ray {
        { pos[0], pos[1], pos[2] },
        { dir[0], dir[1], dir[2] }
    };

    ground::Hit hit = globalScene.Intersect(ray);

    return Hit { hit.geomId };
}


std::vector<ground::Image> images;

GROUND_API int CreateImage(int width, int height, int numChannels) {
    images.emplace_back(width, height, numChannels);
    return images.size() - 1;
}

GROUND_API void AddSplat(int image, float x, float y, const float* value) {
    // TODO check that the image id is correct (Debug mode?)
    images[image].AddValue(x, y, value);
}

GROUND_API void WriteImage(int image, const char* filename) {
    ground::WriteImageToFile(images[image], filename);
}

}