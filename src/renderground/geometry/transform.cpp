#include "geometry/transform.h"

namespace ground {

Transform::Transform(const Float3& pos, const Float3& rot, const Float3& scale) {
    auto s = Scale(scale.x, scale.y, scale.z);
    auto r = Euler(DegreesToRadians(rot.x),
        DegreesToRadians(rot.y), DegreesToRadians(rot.z));
    auto t = Translate(pos.x, pos.y, pos.z);
    matrix = t * r * s;
    inverse = Invert(matrix);
    inverseTranspose = Transpose(inverse);
}

Float3 Transform::ApplyToDirection(const Float3& dir) const {
    auto v = matrix * Float4(dir, 0.0f);
    return Float3(v.x, v.y, v.z);
}

Float3 Transform::ApplyToPoint(const Float3& pos) const {
    auto v = matrix * Float4(pos, 1.0f);
    return Float3(v.x / v.w, v.y / v.w, v.z / v.w);
}

Float3 Transform::ApplyToNormal(const Float3& n) const {
    auto v = inverseTranspose * Float4(n, 0.0f);
    return Float3(v.x, v.y, v.z);
}

Float3 Transform::InvApplyToDirection(const Float3& dir) const {
    auto v = inverse * Float4(dir, 0.0f);
    return Float3(v.x, v.y, v.z);
}

Float3 Transform::InvApplyToPoint(const Float3& pos) const {
    auto v = inverse * Float4(pos, 1.0f);
    return Float3(v.x / v.w, v.y / v.w, v.z / v.w);
}

} // namespace ground