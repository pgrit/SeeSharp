#ifndef RENDERGROUND_GEOMETRY_GEOMETRY_H
#define RENDERGROUND_GEOMETRY_GEOMETRY_H

#include <vector>

#include "geometry/mesh.h"
#include "geometry/ray.h"

namespace ground {

class Scene {
public:
    int AddMesh(Mesh&& mesh);

    void Finalize();

    void Intersect(const Ray& ray);

private:
    std::vector<Mesh> meshes;
    bool isFinal = false;
};


} // namespace ground

#endif // RENDERGROUND_GEOMETRY_GEOMETRY_H