#pragma once

#include <vector>

#include "renderground/api/cpputils.h"
#include "renderground/math/distribution.h"

namespace ground {

class Mesh {
public:
    Mesh(const Vector3* verts, int numVerts, const int* indices, int numIndices,
        const Vector2* texCoords, const Vector3* shadingNormals);

    size_t GetNumVertices() const { return vertices.size(); }
    size_t GetNumTriangles() const { return indices.size() / 3; }

    const float* GetVertexData() const { return (float*)vertices.data(); }
    const int* GetIndexData() const { return indices.data(); }

    SurfacePoint PrimarySampleToSurface(const Vector2& primarySample, float* jacobian) const;
    float ComputePrimaryToSurfaceJacobian(const SurfacePoint& point) const;

    Vector3 PointFromBarycentric(int primId, const Vector2& barycentric) const;

    Vector2 ComputeTextureCoordinates(int primId, const Vector2& barycentric) const;
    Vector3 ComputeShadingNormal(int primId, const Vector2& barycentric) const;

private:
    std::vector<Vector3> vertices;
    std::vector<int> indices;

    std::vector<Vector3> faceNormals;
    std::vector<float> surfaceAreas;
    float totalSurfaceArea;
    Distribution1D triangleDistribution;

    std::vector<Vector2> textureCoordinates;
    std::vector<Vector3> shadingNormals;
};

} // namespace ground
