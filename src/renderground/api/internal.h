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

inline ground::Float3& ApiToInternal(Vector3& api) {
    return *reinterpret_cast<ground::Float3*>(&api);
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

#define ApiAssert(cond) ApiCheck(cond, __FILE__, __LINE__, __func__)
inline void ApiCheck(bool cond, const char* file, int line, const char* func) {
    if (!cond) {
        std::cerr << "Condition not met in " << func << "(): "
                  << file << ", line " << line << std::endl;
        abort();
    }
}