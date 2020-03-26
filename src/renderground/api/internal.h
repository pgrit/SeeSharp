#include "api/api.h"
#include "math/float3.h"
#include "geometry/transform.h"
#include "cameras/camera.h"
#include "image/image.h"

#include <vector>
#include <memory>

extern std::vector<std::unique_ptr<ground::Transform>> globalTransforms;
extern std::vector<std::unique_ptr<ground::Camera>> globalCameras;
extern std::vector<std::unique_ptr<ground::Image>> globalImages;

inline ground::Float3& ApiVectorToInternal(Vector3& api) {
    return *reinterpret_cast<ground::Float3*>(&api);
}

inline ground::Float2& ApiVectorToInternal(Vector2& api) {
    return *reinterpret_cast<ground::Float2*>(&api);
}

inline Ray& InternalRayToApi(ground::Ray& r) {
    return *reinterpret_cast<Ray*>(&r);
}
