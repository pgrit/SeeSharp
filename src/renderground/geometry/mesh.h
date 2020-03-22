#ifndef RENDERGROUND_GEOMETRY_MESH_H
#define RENDERGROUND_GEOMETRY_MESH_H

#include <vector>

#include "geometry/float3.h"

namespace ground {

class Mesh {
public:
    Mesh(const Float3* verts, int numVerts, const int* indices, int numIndices);

private:
    std::vector<Float3> vertices;
    std::vector<int> indices;
};

} // namespace ground

#endif // RENDERGROUND_GEOMETRY_MESH_H