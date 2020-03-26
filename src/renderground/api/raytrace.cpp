#include "api/raytrace.h"
#include "geometry/geometry.h"

#include <tbb/parallel_for.h>

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

GROUND_API Hit TraceSingle(Ray ray) {
    // Internal ray representation and API are data layout compatible
    auto r = reinterpret_cast<ground::Ray*>(&ray);

    ground::Hit hit = globalScene.Intersect(*r);

    return Hit { hit.geomId };
}

GROUND_API void TraceMulti(const Ray* rays, int num, Hit* hits) {
    // TODO this should instead trigger a call to IntersectN
    //      in Embree, to reap additional performance!
    tbb::parallel_for(tbb::blocked_range<int>(0, num),
        [&](tbb::blocked_range<int> r) {
        for (int i = r.begin(); i < r.end(); ++i) {
            auto ray = reinterpret_cast<const ground::Ray*>(rays + i);
        ground::Hit hit = globalScene.Intersect(*ray);
            hits[i] = { hit.geomId };
        }
    });
}

}