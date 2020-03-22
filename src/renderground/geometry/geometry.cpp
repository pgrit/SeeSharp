#include <embree3/rtcore.h>

#include "geometry/geometry.h"

namespace ground {

int Scene::AddMesh(Mesh&& mesh) {
    meshes.emplace_back(mesh);
    return meshes.size() - 1;
}

void Scene::Finalize() {

}

} // namespace ground