#pragma once

#include <cmath>

#include "math/constants.h"
#include "math/float4.h"
#include "math/float3.h"

namespace ground {

struct Float4x4 {
    Float4 rows[4];

    Float4x4() {}
    Float4x4(const Float4& r0, const Float4& r1, const Float4& r2, const Float4& r3) : rows{r0, r1, r2, r3} {}

    const Float4& operator [] (int row) const { return rows[row]; }
    Float4& operator [] (int row) { return rows[row]; }

    static inline Float4x4 identity() {
        return Float4x4(Float4(1.0f, 0.0f, 0.0f, 0.0f),
                        Float4(0.0f, 1.0f, 0.0f, 0.0f),
                        Float4(0.0f, 0.0f, 1.0f, 0.0f),
                        Float4(0.0f, 0.0f, 0.0f, 1.0f));
    }

    static inline Float4x4 zero() {
        return Float4x4(Float4(0.0f, 0.0f, 0.0f, 0.0f),
                        Float4(0.0f, 0.0f, 0.0f, 0.0f),
                        Float4(0.0f, 0.0f, 0.0f, 0.0f),
                        Float4(0.0f, 0.0f, 0.0f, 0.0f));
    }
};

inline Float4x4 Perspective(float fov, float aspect, float znear, float zfar) {
    // Camera points towards -z.  0 < znear < zfar.
    // Matrix maps z range [-znear, -zfar] to [-1, 1], after homogeneous division.
    const float f_h =   1.0f / std::tan(fov * PI / 360.0f);
    const float f_v = aspect / std::tan(fov * PI / 360.0f);
    const float d = 1.0f / (znear - zfar);

    Float4x4 r;
    r[0][0] = f_h;  r[0][1] = 0.0f; r[0][2] = 0.0f;               r[0][3] = 0.0f;
    r[1][0] = 0.0f; r[1][1] = -f_v; r[1][2] = 0.0f;               r[1][3] = 0.0f;
    r[2][0] = 0.0f; r[2][1] = 0.0f; r[2][2] = (znear + zfar) * d; r[2][3] = 2.0f * znear * zfar * d;
    r[3][0] = 0.0f; r[3][1] = 0.0f; r[3][2] = -1.0f;              r[3][3] = 0.0f;

    return r;
}

inline Float4x4 Scale(float x, float y, float z, float w = 1.0f) {
    return Float4x4(Float4(   x, 0.0f, 0.0f, 0.0f),
                    Float4(0.0f,    y, 0.0f, 0.0f),
                    Float4(0.0f, 0.0f,    z, 0.0f),
                    Float4(0.0f, 0.0f, 0.0f,    w));
}

inline Float4x4 Translate(float x, float y, float z) {
    return Float4x4(Float4(1.0f, 0.0f, 0.0f,    x),
                    Float4(0.0f, 1.0f, 0.0f,    y),
                    Float4(0.0f, 0.0f, 1.0f,    z),
                    Float4(0.0f, 0.0f, 0.0f, 1.0f));
}

inline Float4x4 RotateX(float angle) {
    return Float4x4(Float4(1.0f,         0.0f,        0.0f, 0.0f),
                    Float4(0.0f,  cosf(angle), sinf(angle), 0.0f),
                    Float4(0.0f, -sinf(angle), cosf(angle), 0.0f),
                    Float4(0.0f,         0.0f,        0.0f, 1.0f));
}

inline Float4x4 RotateY(float angle) {
    return Float4x4(Float4(cosf(angle), 0.0f, -sinf(angle), 0.0f),
                    Float4(       0.0f, 1.0f,         0.0f, 0.0f),
                    Float4(sinf(angle), 0.0f,  cosf(angle), 0.0f),
                    Float4(       0.0f, 0.0f,         0.0f, 1.0f));
}

inline Float4x4 RotateZ(float angle) {
    return Float4x4(Float4( cosf(angle), sinf(angle), 0.0f, 0.0f),
                    Float4(-sinf(angle), cosf(angle), 0.0f, 0.0f),
                    Float4(        0.0f,        0.0f, 1.0f, 0.0f),
                    Float4(        0.0f,        0.0f, 0.0f, 1.0f));
}

inline float Determinant(const Float4x4& a) {
    float m0 = a[1][1] * a[2][2] * a[3][3] - a[1][1] * a[2][3] * a[3][2] - a[2][1] * a[1][2] * a[3][3] +
               a[2][1] * a[1][3] * a[3][2] + a[3][1] * a[1][2] * a[2][3] - a[3][1] * a[1][3] * a[2][2];

    float m1 = -a[1][0] * a[2][2] * a[3][3] + a[1][0] * a[2][3] * a[3][2] + a[2][0] * a[1][2] * a[3][3] -
               a[2][0] * a[1][3] * a[3][2] - a[3][0] * a[1][2] * a[2][3] + a[3][0] * a[1][3] * a[2][2];

    float m2 = a[1][0] * a[2][1] * a[3][3] - a[1][0] * a[2][3] * a[3][1] - a[2][0] * a[1][1] * a[3][3] +
               a[2][0] * a[1][3] * a[3][1] + a[3][0] * a[1][1] * a[2][3] - a[3][0] * a[1][3] * a[2][1];

    float m3 = -a[1][0] * a[2][1] * a[3][2] + a[1][0] * a[2][2] * a[3][1] + a[2][0] * a[1][1] * a[3][2] -
               a[2][0] * a[1][2] * a[3][1] - a[3][0] * a[1][1] * a[2][2] + a[3][0] * a[1][2] * a[2][1];

    float det = a[0][0] * m0 + a[0][1] * m1 + a[0][2] * m2 + a[0][3] * m3;

    return det;
}

inline Float4x4 Transpose(const Float4x4& a) {
    return Float4x4(Float4(a.rows[0][0], a.rows[1][0], a.rows[2][0], a.rows[3][0]),
                    Float4(a.rows[0][1], a.rows[1][1], a.rows[2][1], a.rows[3][1]),
                    Float4(a.rows[0][2], a.rows[1][2], a.rows[2][2], a.rows[3][2]),
                    Float4(a.rows[0][3], a.rows[1][3], a.rows[2][3], a.rows[3][3]));
}

inline Float4x4 operator * (const Float4x4& a, const Float4x4& b) {
    Float4x4 t = Transpose(b);
    return Float4x4(Float4(Dot(a[0], t[0]), Dot(a[0], t[1]), Dot(a[0], t[2]), Dot(a[0], t[3])),
                    Float4(Dot(a[1], t[0]), Dot(a[1], t[1]), Dot(a[1], t[2]), Dot(a[1], t[3])),
                    Float4(Dot(a[2], t[0]), Dot(a[2], t[1]), Dot(a[2], t[2]), Dot(a[2], t[3])),
                    Float4(Dot(a[3], t[0]), Dot(a[3], t[1]), Dot(a[3], t[2]), Dot(a[3], t[3])));
}

inline Float4x4 operator * (const Float4x4& a, float b) {
    return Float4x4(a.rows[0] * b, a.rows[1] * b, a.rows[2] * b, a.rows[3] * b);
}

inline Float4x4 operator * (float a, const Float4x4& b) {
    return b * a;
}

inline Float4 operator * (const Float4x4& a, const Float4& b) {
    return Float4(Dot(a[0], b),
                  Dot(a[1], b),
                  Dot(a[2], b),
                  Dot(a[3], b));
}

inline Float4 operator * (const Float4& a, const Float4x4& b) {
    return Float4(b[0][0] * a[0] + b[1][0] * a[1] + b[2][0] * a[2] + b[3][0] * a[3],
                  b[0][1] * a[0] + b[1][1] * a[1] + b[2][1] * a[2] + b[3][1] * a[3],
                  b[0][2] * a[0] + b[1][2] * a[1] + b[2][2] * a[2] + b[3][2] * a[3],
                  b[0][3] * a[0] + b[1][3] * a[1] + b[2][3] * a[2] + b[3][3] * a[3]);
}

inline Float4x4 Invert(const Float4x4& a) {
    Float4x4 result;

    result[0][0] = +a[1][1] * a[2][2] * a[3][3] - a[1][1] * a[2][3] * a[3][2] - a[2][1] * a[1][2] * a[3][3]
                   +a[2][1] * a[1][3] * a[3][2] + a[3][1] * a[1][2] * a[2][3] - a[3][1] * a[1][3] * a[2][2];
    result[1][0] = -a[1][0] * a[2][2] * a[3][3] + a[1][0] * a[2][3] * a[3][2] + a[2][0] * a[1][2] * a[3][3]
                   -a[2][0] * a[1][3] * a[3][2] - a[3][0] * a[1][2] * a[2][3] + a[3][0] * a[1][3] * a[2][2];
    result[2][0] = +a[1][0] * a[2][1] * a[3][3] - a[1][0] * a[2][3] * a[3][1] - a[2][0] * a[1][1] * a[3][3]
                   +a[2][0] * a[1][3] * a[3][1] + a[3][0] * a[1][1] * a[2][3] - a[3][0] * a[1][3] * a[2][1];
    result[3][0] = -a[1][0] * a[2][1] * a[3][2] + a[1][0] * a[2][2] * a[3][1] + a[2][0] * a[1][1] * a[3][2]
                   -a[2][0] * a[1][2] * a[3][1] - a[3][0] * a[1][1] * a[2][2] + a[3][0] * a[1][2] * a[2][1];

    float det = a[0][0] * result[0][0] + a[0][1] * result[1][0] + a[0][2] * result[2][0] + a[0][3] * result[3][0];

    if (det == 0)
        return Float4x4::zero();

    result[0][1] = -a[0][1] * a[2][2] * a[3][3] + a[0][1] * a[2][3] * a[3][2] + a[2][1] * a[0][2] * a[3][3]
                   -a[2][1] * a[0][3] * a[3][2] - a[3][1] * a[0][2] * a[2][3] + a[3][1] * a[0][3] * a[2][2];
    result[1][1] = +a[0][0] * a[2][2] * a[3][3] - a[0][0] * a[2][3] * a[3][2] - a[2][0] * a[0][2] * a[3][3]
                   +a[2][0] * a[0][3] * a[3][2] + a[3][0] * a[0][2] * a[2][3] - a[3][0] * a[0][3] * a[2][2];
    result[2][1] = -a[0][0] * a[2][1] * a[3][3] + a[0][0] * a[2][3] * a[3][1] + a[2][0] * a[0][1] * a[3][3]
                   -a[2][0] * a[0][3] * a[3][1] - a[3][0] * a[0][1] * a[2][3] + a[3][0] * a[0][3] * a[2][1];
    result[3][1] = +a[0][0] * a[2][1] * a[3][2] - a[0][0] * a[2][2] * a[3][1] - a[2][0] * a[0][1] * a[3][2]
                   +a[2][0] * a[0][2] * a[3][1] + a[3][0] * a[0][1] * a[2][2] - a[3][0] * a[0][2] * a[2][1];

    result[0][2] = +a[0][1] * a[1][2] * a[3][3] - a[0][1] * a[1][3] * a[3][2] - a[1][1] * a[0][2] * a[3][3]
                   +a[1][1] * a[0][3] * a[3][2] + a[3][1] * a[0][2] * a[1][3] - a[3][1] * a[0][3] * a[1][2];
    result[1][2] = -a[0][0] * a[1][2] * a[3][3] + a[0][0] * a[1][3] * a[3][2] + a[1][0] * a[0][2] * a[3][3]
                   -a[1][0] * a[0][3] * a[3][2] - a[3][0] * a[0][2] * a[1][3] + a[3][0] * a[0][3] * a[1][2];
    result[2][2] = +a[0][0] * a[1][1] * a[3][3] - a[0][0] * a[1][3] * a[3][1] - a[1][0] * a[0][1] * a[3][3]
                   +a[1][0] * a[0][3] * a[3][1] + a[3][0] * a[0][1] * a[1][3] - a[3][0] * a[0][3] * a[1][1];
    result[3][2] = -a[0][0] * a[1][1] * a[3][2] + a[0][0] * a[1][2] * a[3][1] + a[1][0] * a[0][1] * a[3][2]
                   -a[1][0] * a[0][2] * a[3][1] - a[3][0] * a[0][1] * a[1][2] + a[3][0] * a[0][2] * a[1][1];

    result[0][3] = -a[0][1] * a[1][2] * a[2][3] + a[0][1] * a[1][3] * a[2][2] + a[1][1] * a[0][2] * a[2][3]
                   -a[1][1] * a[0][3] * a[2][2] - a[2][1] * a[0][2] * a[1][3] + a[2][1] * a[0][3] * a[1][2];
    result[1][3] = +a[0][0] * a[1][2] * a[2][3] - a[0][0] * a[1][3] * a[2][2] - a[1][0] * a[0][2] * a[2][3]
                   +a[1][0] * a[0][3] * a[2][2] + a[2][0] * a[0][2] * a[1][3] - a[2][0] * a[0][3] * a[1][2];
    result[2][3] = -a[0][0] * a[1][1] * a[2][3] + a[0][0] * a[1][3] * a[2][1] + a[1][0] * a[0][1] * a[2][3]
                   -a[1][0] * a[0][3] * a[2][1] - a[2][0] * a[0][1] * a[1][3] + a[2][0] * a[0][3] * a[1][1];
    result[3][3] = +a[0][0] * a[1][1] * a[2][2] - a[0][0] * a[1][2] * a[2][1] - a[1][0] * a[0][1] * a[2][2]
                   +a[1][0] * a[0][2] * a[2][1] + a[2][0] * a[0][1] * a[1][2] - a[2][0] * a[0][2] * a[1][1];

    result = result * (1.0f / det);
    return result;
}

inline Float4x4 Abs(const Float4x4& a) {
    return Float4x4(Abs(a[0]), Abs(a[1]), Abs(a[2]), Abs(a[3]));
}

inline Float4x4 Euler(float x, float y, float z) {
    return RotateX(x) * RotateY(y) * RotateZ(z);
}

} // namespace ground