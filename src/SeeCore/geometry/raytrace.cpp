#include "api/raytrace.h"
#include "geometry/scene.h"

#include <tbb/parallel_for.h>

std::vector<std::unique_ptr<ground::Scene>> globalScenes;

extern "C" {

SEE_CORE_API int InitScene() {
    globalScenes.emplace_back(new ground::Scene());
    globalScenes.back()->Init();
    return int(globalScenes.size()) - 1;
}

SEE_CORE_API int AddTriangleMesh(int scene, const float* vertices, int numVerts, const int* indices, int numIdx) {
    return globalScenes[scene]->AddMesh(vertices, indices, numVerts, numIdx / 3);
}

SEE_CORE_API void FinalizeScene(int scene) {
    globalScenes[scene]->Finalize();
}

SEE_CORE_API Hit TraceSingle(int scene, Ray ray) {
    return globalScenes[scene]->Intersect(ray);
}

}