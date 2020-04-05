#include <cassert>

#include "geometry/mesh.h"
#include "math/wrap.h"
#include "math/constants.h"

namespace ground {

Mesh::Mesh(const Vector3* verts, int numVerts, const int* indices, int numIndices,
    const Vector2* texCoords, const Vector3* shadingNormals)
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
        auto& v1 = vertices[indices[face * 3 + 0]];
        auto& v2 = vertices[indices[face * 3 + 1]];
        auto& v3 = vertices[indices[face * 3 + 2]];

        // Compute the normal. Winding order is CCW always.
        Vector3 n = Cross(v2 - v1, v3 - v1);
        float len = Length(n);
        faceNormals[face] = n / len;
        surfaceAreas[face] = len * 0.5f;

        totalSurfaceArea += surfaceAreas[face];
    }

    triangleDistribution.Build(surfaceAreas.begin(), surfaceAreas.end());

    // Per-vertex texture coordinates
    this->textureCoordinates.resize(numVerts);
    if (!texCoords) { // not given: default to (0,0) everywhere
        std::fill(textureCoordinates.begin(), textureCoordinates.end(), Vector2{0, 0});
    } else {
        std::copy(texCoords, texCoords + numVerts, textureCoordinates.begin());
    }

    // Per-vertex shading normals
    this->shadingNormals.resize(numVerts);
    if (!shadingNormals) { // not given: default to face normals
        for (int face = 0; face < numFaces; ++face) {
            this->shadingNormals[indices[face * 3 + 0]] = faceNormals[face];
            this->shadingNormals[indices[face * 3 + 1]] = faceNormals[face];
            this->shadingNormals[indices[face * 3 + 2]] = faceNormals[face];
        }
    } else {
        std::copy(shadingNormals, shadingNormals + numVerts, this->shadingNormals.begin());
    }

    // TODO pre-normalize shading normals here? Can that be problematic with interpolation?
}

SurfacePoint Mesh::PrimarySampleToSurface(const Vector2& primarySample, float* jacobian) const {
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
    Vector2 barycentricCoords { u, v };

    *jacobian = selectionJacobian / surfaceAreas[primId];
    CheckFloatEqual(*jacobian, 1.0f / totalSurfaceArea);

    return SurfacePoint {
        PointFromBarycentric(primId, barycentricCoords),
        faceNormals[primId],
        barycentricCoords,
        0xFFFFFFFF, // To be filled by the caller, we don't know our own Id
        primId,
    };
}

float Mesh::ComputePrimaryToSurfaceJacobian(const SurfacePoint& point) const {
    return 1.0f / totalSurfaceArea;
}

template<typename VertArray, typename IdxArray>
auto InterpolateOnTriangle(int primId, const Vector2& barycentric,
    const VertArray& vertexData, const IdxArray& indices)
{
    auto& v1 = vertexData[indices[primId * 3 + 0]];
    auto& v2 = vertexData[indices[primId * 3 + 1]];
    auto& v3 = vertexData[indices[primId * 3 + 2]];

    return barycentric.x * v2
         + barycentric.y * v3
         + (1 - barycentric.x - barycentric.y) * v1;
}

Vector3 Mesh::PointFromBarycentric(int primId, const Vector2& barycentric) const {
    return InterpolateOnTriangle(primId, barycentric, vertices, indices);
}

Vector2 Mesh::ComputeTextureCoordinates(int primId, const Vector2& barycentric) const {
    return InterpolateOnTriangle(primId, barycentric, textureCoordinates, indices);
}

Vector3 Mesh::ComputeShadingNormal(int primId, const Vector2& barycentric) const {
    // TODO this frequent call to Normalize could possibly be optimized away
    return Normalize(InterpolateOnTriangle(primId, barycentric, shadingNormals, indices));
}

} // namespace ground