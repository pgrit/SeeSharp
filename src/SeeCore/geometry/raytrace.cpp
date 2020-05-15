#include "api/raytrace.h"
#include "geometry/scene.h"

#include <tbb/parallel_for.h>

std::unique_ptr<ground::Scene> globalScene;

extern "C" {

SEE_CORE_API void InitScene() {
    globalScene.reset(new ground::Scene());
    globalScene->Init();
}

SEE_CORE_API void DeleteScene() {
    globalScene.release();
}

SEE_CORE_API int AddTriangleMesh(const float* vertices, int numVerts, const int* indices, int numIdx) {
    return globalScene->AddMesh(vertices, indices, numVerts, numIdx / 3);
}

SEE_CORE_API void FinalizeScene() {
    globalScene->Finalize();
}

SEE_CORE_API Hit TraceSingle(Ray ray) {
    return globalScene->Intersect(ray);
}

}