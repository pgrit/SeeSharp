#pragma once

#include <renderground/api/api.h>

inline Vector3 operator- (const Vector3& a) {
    return Vector3 { -a.x, -a.y, -a.z };
}

inline Vector3 operator+ (const Vector3& a, const Vector3& b) {
    return Vector3 { a.x + b.x, a.y + b.y, a.z + b.z };
}

inline Vector3 operator- (const Vector3& a, const Vector3& b) {
    return a + (-b);
}

inline Vector3 operator* (const Vector3& a, float s) {
    return Vector3 { a.x * s, a.y * s, a.z * s };
}

inline Vector3 operator* (float s, const Vector3& a) {
    return a * s;
}

inline float Dot (const Vector3& a, const Vector3& b) {
    return a.x * b.x + a.y * b.y + a.z * b.z;
}

inline float LengthSquared(const Vector3& v) {
    return Dot(v, v);
}