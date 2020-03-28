#include "api/transforms.h"
#include "geometry/transform.h"
#include "api/internal.h"

#include <vector>

std::vector<std::unique_ptr<ground::Transform>> globalTransforms;

extern "C" {

GROUND_API int CreateTransform(Vector3 translation, Vector3 eulerAngles, Vector3 scale) {
    globalTransforms.emplace_back(new ground::Transform(ApiToInternal(translation),
        ApiToInternal(eulerAngles), ApiToInternal(scale)));
    return int(globalTransforms.size()) - 1;
}

}
