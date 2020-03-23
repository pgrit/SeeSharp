#pragma once

#include <vector>

#include "geometry/float3.h"

namespace ground {

class Mesh {
public:
    Mesh(const Float3* verts, int numVerts, const int* indices, int numIndices);

    size_t GetNumVertices() const { return vertices.size(); }
    size_t GetNumTriangles() const { return indices.size() / 3; }

    const float* GetVertexData() const { return (float*)vertices.data(); }
    const int* GetIndexData() const { return indices.data(); }

private:
    std::vector<Float3> vertices;
    std::vector<int> indices;
};

} // namespace ground
