#pragma once

#include <vector>
#include <embree3/rtcore.h>

#include "geometry/mesh.h"
#include "geometry/ray.h"
#include "geometry/hit.h"

namespace ground {

class Scene {
public:
    ~Scene() {
        if (isInit) {
            rtcReleaseScene(embreeScene);
            rtcReleaseDevice(embreeDevice);
        }
    }

    void Init();

    int AddMesh(Mesh&& mesh);

    void Finalize();

    Hit Intersect(const Ray& ray);

private:
    std::vector<Mesh> meshes;
    bool isInit = false;
    bool isFinal = false;

    RTCDevice embreeDevice;
    RTCScene embreeScene;
};


} // namespace ground
