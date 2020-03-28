#include <cassert>

#include "geometry/mesh.h"
#include "math/wrap.h"
#include "math/constants.h"

namespace ground {

Mesh::Mesh(const Float3* verts, int numVerts, const int* indices, int numIndices)
: vertices(numVerts), indices(numIndices)
{
    // TODO assert that indices is a multiple of two
    //      (sanity check level, i.e., not only in debug mode)
    const int numFaces = numIndices / 3;

    std::copy(verts, verts + numVerts, this->vertices.begin());
    std::copy(indices, indices + numIndices, this->indices.begin());

    // Compute surface normals and areas
    faceNormals.resize(numFaces);
    surfaceAreas.resize(numFaces);
    totalSurfaceArea = 0;
    for (int face = 0; face < numFaces; ++face) {
        auto& v1 = vertices[indices[face + 0]];
        auto& v2 = vertices[indices[face + 1]];
        auto& v3 = vertices[indices[face + 2]];

        // Compute the normal. Winding order is CCW always.
        Float3 n = Cross(v2 - v1, v3 - v1);
        float len = Length(n);
        faceNormals[face] = n / len;
        surfaceAreas[face] = len * 0.5f;

        totalSurfaceArea += surfaceAreas[face];
    }

    triangleDistribution.Build(surfaceAreas.begin(), surfaceAreas.end());
}

SurfacePoint Mesh::PrimarySampleToSurface(const Float2& primarySample, float* jacobian) const {
    // TODO sanity check that both primary sample values
    //      are in [0,1]

    // Select a triangle proportionally to its surface area
    float selectionJacobian = 1.0f;
    unsigned int primId = triangleDistribution.TransformPrimarySample(
        primarySample.x, &selectionJacobian);

    // Remap the first PSS dimension to the selected triangle
    float lo = primId == 0 ? 0.0f : triangleDistribution.GetJacobian(primId - 1);
    float delta = selectionJacobian;
    float remapped = (primarySample.x - lo) * delta;
    assert(remapped >= 0.0f && remapped <= 1.0f);

    // Remap to a uniform distribution of barycentric coordinates
    float u = 0, v = 0;
    WrapToUniformTriangle(remapped, primarySample.y, u, v);
    Float2 barycentricCoords(u, v);

    *jacobian = selectionJacobian / surfaceAreas[primId];
    AssertFloatEqual(*jacobian, 1.0f / totalSurfaceArea);

    return SurfacePoint {
        PointFromBarycentric(primId, barycentricCoords),
        faceNormals[primId],
        barycentricCoords,
        0xFFFFFFFF, // To be filled by the caller, we don't know our own Id
        primId,
    };
}

Float3 Mesh::PointFromBarycentric(int primId, const Float2& barycentric) const {
    auto& v1 = vertices[indices[primId + 0]];
    auto& v2 = vertices[indices[primId + 1]];
    auto& v3 = vertices[indices[primId + 2]];

    return barycentric.x * v1
         + barycentric.y * v2
         + (1 - barycentric.x - barycentric.y) * v3;
}

} // namespace ground