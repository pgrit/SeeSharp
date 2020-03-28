#include "api/api.h"
#include "math/float3.h"
#include "geometry/geometry.h"
#include "geometry/transform.h"
#include "geometry/hit.h"
#include "cameras/camera.h"
#include "image/image.h"
#include "shading/shading.h"

#include <vector>
#include <memory>
#include <iostream>
#include <unordered_map>

extern std::vector<std::unique_ptr<ground::Transform>> globalTransforms;
extern std::vector<std::unique_ptr<ground::Camera>> globalCameras;
extern std::vector<std::unique_ptr<ground::Image>> globalImages;
extern ground::Scene globalScene;
extern std::vector<std::unique_ptr<ground::Material>> globalMaterials;
extern std::unordered_map<int, int> globalMeshToMaterial;

// TODO the following conversion functions (which are somewhat unsafe)
//      could be completely avoided by separating data structures from
//      logic and sharing the structures betweeen API and internal.

inline ground::Float3& ApiToInternal(Vector3& api) {
    return *reinterpret_cast<ground::Float3*>(&api);
}

inline Vector3& InternalToApi(ground::Float3& api) {
    return *reinterpret_cast<Vector3*>(&api);
}

inline ground::Float2& ApiToInternal(Vector2& api) {
    return *reinterpret_cast<ground::Float2*>(&api);
}

inline Ray& InternalToApi(ground::Ray& r) {
    return *reinterpret_cast<Ray*>(&r);
}

inline ground::Ray& ApiToInternal(Ray& r) {
    return *reinterpret_cast<ground::Ray*>(&r);
}

inline const ground::Ray& ApiToInternal(const Ray& r) {
    return *reinterpret_cast<const ground::Ray*>(&r);
}

inline Hit& InternalToApi(ground::Hit& h) {
    return *reinterpret_cast<Hit*>(&h);
}

inline SurfacePoint& InternalToApi(ground::SurfacePoint& h) {
    return *reinterpret_cast<SurfacePoint*>(&h);
}

inline const ground::SurfacePoint& ApiToInternal(const SurfacePoint& h) {
    return *reinterpret_cast<const ground::SurfacePoint*>(&h);
}

inline void _ApiCheck(bool cond, const char* file, int line, const char* func) {
    if (!cond) {
        std::cerr << "Condition not met in " << func << "(): "
                  << file << ", line " << line << std::endl;
        abort();
    }
}
#define ApiCheck(cond) _ApiCheck(cond, __FILE__, __LINE__, __func__)

inline void _SanityCheck(bool cond, const char* file, int line, const char* func) {
#ifdef SANITY_CHECKS
    if (!cond) {
        std::cerr << "Condition not met in " << func << "(): "
                  << file << ", line " << line << std::endl;
        abort();
    }
#endif
}
#define SanityCheck(cond) _SanityCheck(cond, __FILE__, __LINE__, __func__)
