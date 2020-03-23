#include "geometry/mesh.h"

namespace ground {

Mesh::Mesh(const Float3* verts, int numVerts, const int* indices, int numIndices)
: vertices(numVerts), indices(numIndices)
{
    std::copy(verts, verts + numVerts, this->vertices.begin());
    std::copy(indices, indices + numIndices, this->indices.begin());
}

} // namespace ground