#include "geometry/transform.h"

namespace ground {

Transform::Transform(const Float3& pos, const Float3& rot, const Float3& scale) {
    auto s = Scale(scale.x, scale.y, scale.z);
    auto r = Euler(rot.x, rot.y, rot.z);
    auto t = Translate(pos.x, pos.y, pos.z);
    matrix = t * r * s;
    inverse = Invert(matrix);
    inverseTranspose = Transpose(inverse);
}

} // namespace ground