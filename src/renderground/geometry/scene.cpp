#include <cassert>
#include <algorithm>

#include "geometry/scene.h"
#include "geometry/hit.h"

namespace ground {

void errorFunction(void* userPtr, enum RTCError error, const char* str) {
    printf("error %d: %s\n", error, str);
}

RTCDevice initializeDevice() {
    RTCDevice device = rtcNewDevice(NULL);

    if (!device)
        printf("error %d: cannot create Embree device\n", rtcGetDeviceError(NULL));

    rtcSetDeviceErrorFunction(device, errorFunction, NULL);
    return device;
}

void Scene::Init() {
    embreeDevice = initializeDevice();
    embreeScene = rtcNewScene(embreeDevice);
    isInit = true;
}

int Scene::AddMesh(Mesh&& mesh) {
    // Add the mesh data to our internal storage, used for sampling etc.
    meshes.emplace_back(mesh);
    const auto& m = meshes.back();
    const int meshId = meshes.size() - 1;

    // Create the Embree buffers
    RTCGeometry geom = rtcNewGeometry(embreeDevice, RTC_GEOMETRY_TYPE_TRIANGLE);
    float* vertices = (float*) rtcSetNewGeometryBuffer(geom,
        RTC_BUFFER_TYPE_VERTEX, 0, RTC_FORMAT_FLOAT3,
        3 * sizeof(float), m.GetNumVertices());
    unsigned* indices = (unsigned*) rtcSetNewGeometryBuffer(geom,
        RTC_BUFFER_TYPE_INDEX, 0, RTC_FORMAT_UINT3,
        3 * sizeof(unsigned), m.GetNumTriangles());

    // Copy vertex and index data
    std::copy(m.GetVertexData(), m.GetVertexData() + m.GetNumVertices() * 3, vertices);
    std::copy(m.GetIndexData(), m.GetIndexData() + m.GetNumTriangles() * 3, indices);

    rtcCommitGeometry(geom);

    int geomId = rtcAttachGeometry(embreeScene, geom);

    // Right now, we rely on the Embree ID and ours to be identical by default.
    // This restriction can be lifted by a lookup table if necessary.
    assert(geomId == meshId);

    rtcReleaseGeometry(geom);

    return meshId;
}

void Scene::Finalize() {
    rtcCommitScene(embreeScene);
}

Hit Scene::Intersect(const Ray& ray) {
    struct RTCIntersectContext context;
    rtcInitIntersectContext(&context);

    // TODO utilize the intersection context
    //      in particular, allow coherent vs incoherent flags
    //      for multiple ray intersections.

    struct RTCRayHit rayhit;
    rayhit.ray.org_x = ray.origin.x;
    rayhit.ray.org_y = ray.origin.y;
    rayhit.ray.org_z = ray.origin.z;
    rayhit.ray.dir_x = ray.direction.x;
    rayhit.ray.dir_y = ray.direction.y;
    rayhit.ray.dir_z = ray.direction.z;
    rayhit.ray.tnear = ray.minDistance;
    rayhit.ray.tfar = std::numeric_limits<float>::infinity();
    rayhit.ray.mask = 0;
    rayhit.ray.flags = 0;
    rayhit.hit.geomID = RTC_INVALID_GEOMETRY_ID;
    rayhit.hit.instID[0] = RTC_INVALID_GEOMETRY_ID;

    rtcIntersect1(embreeScene, &context, &rayhit);

    Float3 position = ray.origin + rayhit.ray.tfar * ray.direction;

    float errorOffset = std::max(
            std::max(std::abs(position.x),std::abs(position.y)),
            std::max(std::abs(position.z),rayhit.ray.tfar)
        ) * 32.0f * 1.19209e-07f;

    Hit hit {
        SurfacePoint {
            position,
            Float3(rayhit.hit.Ng_x, rayhit.hit.Ng_y, rayhit.hit.Ng_z),
            Float2(rayhit.hit.u, rayhit.hit.v),
            rayhit.hit.geomID,
            rayhit.hit.primID,
        },
        rayhit.ray.tfar,
        errorOffset
    };

    // Embree does not normalize the face normal
    hit.point.normal = Normalize(hit.point.normal);

    return hit;
}

} // namespace ground