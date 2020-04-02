#include "geometry/transform.h"

namespace ground {

Transform::Transform(const Vector3& pos, const Vector3& rot, const Vector3& scale) {
    auto s = Scale(scale.x, scale.y, scale.z);
    auto r = Euler(DegreesToRadians(rot.x),
        DegreesToRadians(rot.y), DegreesToRadians(rot.z));
    auto t = Translate(pos.x, pos.y, pos.z);
    matrix = t * r * s;
    inverse = Invert(matrix);
    inverseTranspose = Transpose(inverse);
}

Vector3 Transform::ApplyToDirection(const Vector3& dir) const {
    auto v = matrix * Float4(dir, 0.0f);
    return Vector3 { v.x, v.y, v.z };
}

Vector3 Transform::ApplyToPoint(const Vector3& pos) const {
    auto v = matrix * Float4(pos, 1.0f);
    return Vector3 { v.x / v.w, v.y / v.w, v.z / v.w };
}

Vector3 Transform::ApplyToNormal(const Vector3& n) const {
    auto v = inverseTranspose * Float4(n, 0.0f);
    return Vector3 { v.x, v.y, v.z };
}

Vector3 Transform::InvApplyToDirection(const Vector3& dir) const {
    auto v = inverse * Float4(dir, 0.0f);
    return Vector3 { v.x, v.y, v.z };
}

Vector3 Transform::InvApplyToPoint(const Vector3& pos) const {
    auto v = inverse * Float4(pos, 1.0f);
    return Vector3 { v.x / v.w, v.y / v.w, v.z / v.w };
}

} // namespace ground