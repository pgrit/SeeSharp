#pragma once

#include <vector>

#include "geometry/hit.h"
#include "math/float3.h"
#include "math/distribution.h"

namespace ground {

class Mesh {
public:
    Mesh(const Float3* verts, int numVerts, const int* indices, int numIndices);

    size_t GetNumVertices() const { return vertices.size(); }
    size_t GetNumTriangles() const { return indices.size() / 3; }

    const float* GetVertexData() const { return (float*)vertices.data(); }
    const int* GetIndexData() const { return indices.data(); }

    SurfacePoint PrimarySampleToSurface(const Float2& primarySample, float* jacobian) const;

    Float3 PointFromBarycentric(int primId, const Float2& barycentric) const;

private:
    std::vector<Float3> vertices;
    std::vector<int> indices;

    std::vector<Float3> faceNormals;
    std::vector<float> surfaceAreas;
    float totalSurfaceArea;
    Distribution1D triangleDistribution;

    // TODO:
    //  - texture coordinates (per vertex)
    //  - shading normals (per vertex)
};

} // namespace ground
