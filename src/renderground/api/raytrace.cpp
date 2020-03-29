#include "api/raytrace.h"
#include "api/internal.h"
#include "api/cpputils.h"
#include "geometry/scene.h"
#include "math/float2.h"
#include "math/constants.h"

#include <tbb/parallel_for.h>

ground::Scene globalScene;

extern "C" {

GROUND_API void InitScene() {
    globalScene.Init();
}

GROUND_API int AddTriangleMesh(const float* vertices, int numVerts,
    const int* indices, int numIdx, const float* texCoords, const float* shadingNormals)
{
    ApiCheck(numIdx % 3 == 0);

    return globalScene.AddMesh(ground::Mesh(
        reinterpret_cast<const ground::Float3*>(vertices), numVerts,
        indices, numIdx, reinterpret_cast<const ground::Float2*>(texCoords),
        reinterpret_cast<const ground::Float3*>(shadingNormals)));
}

GROUND_API void FinalizeScene() {
    globalScene.Finalize();
}

GROUND_API Hit TraceSingle(Ray ray) {
    ground::Hit hit = globalScene.Intersect(ApiToInternal(ray));
    return InternalToApi(hit);
}

GROUND_API void TraceMulti(const Ray* rays, int num, Hit* hits) {
    // TODO this should instead trigger a call to IntersectN
    //      in Embree, to reap additional performance!
    tbb::parallel_for(tbb::blocked_range<int>(0, num),
        [&](tbb::blocked_range<int> r) {
        for (int i = r.begin(); i < r.end(); ++i) {
            ground::Hit hit = globalScene.Intersect(ApiToInternal(rays[i]));
            hits[i] = InternalToApi(hit);
        }
    });
}

GROUND_API SurfaceSample WrapPrimarySampleToSurface(int meshId, float u, float v) {
    ApiCheck(u >= 0 && u <= 1);
    ApiCheck(v >= 0 && v <= 1);

    auto& m = globalScene.GetMesh(meshId);

    float jacobian = 0;
    auto point = m.PrimarySampleToSurface(ground::Float2(u, v), &jacobian);
    point.geomId = meshId;

    return SurfaceSample {
        InternalToApi(point),
        jacobian
    };
}

GROUND_API bool IsOccluded(const Hit* from, Vector3 to) {
    // TODO this function could (and should) call a special variant of "TraceSingle"
    //      that only checks occlusion for performance.

    auto shadowDir = to - from->point.position;
    auto shadowHit = TraceSingle(Ray{from->point.position, shadowDir, from->errorOffset});
    if (shadowHit.point.meshId >= 0 && shadowHit.distance < 1.0f - from->errorOffset)
        return true;
    return false;
}

GROUND_API Ray SpawnRay(const Hit* from, Vector3 direction) {
    float sign = Dot(direction, from->point.normal) < 0.0f ? -1.0f : 1.0f;
    return Ray {
        from->point.position + sign * from->errorOffset * from->point.normal,
        direction,
        from->errorOffset
    };
}

GROUND_API GeometryTerms ComputeGeometryTerms(const SurfacePoint* from, const SurfacePoint* to) {
    auto dir = to->position - from->position;
    float squaredDistance = LengthSquared(dir);
    dir = dir / std::sqrt(squaredDistance);

    ground::CheckNormalized(from->normal);
    ground::CheckNormalized(to->normal);

    float cosSurface = std::abs(Dot(from->normal, dir));
    float cosLight = std::abs(Dot(to->normal, -dir));

    float geomTerm = cosSurface * cosLight / squaredDistance;

    // avoid NaNs if we happen to sample the exact same point "to" and "from"
    if (squaredDistance == 0.0f) geomTerm = 0.0f;

    return GeometryTerms {
        cosSurface,
        cosLight,
        squaredDistance,
        geomTerm
    };
}

}