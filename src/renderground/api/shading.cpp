#include "api/shading.h"
#include "api/internal.h"

#include "shading/generic.h"
#include "shading/emitter.h"

#include <unordered_map>

std::vector<std::unique_ptr<ground::Material>> globalMaterials;
std::unordered_map<int, int> globalMeshToMaterial;

std::vector<std::unique_ptr<ground::Emitter>> globalEmitters;
std::unordered_map<int, int> globalMeshToEmitter;
std::unordered_map<int, int> globalEmitterToMesh;

inline ground::Material* LookupMaterial(int meshId) {
    auto matId = globalMeshToMaterial[meshId];

    ApiCheck(matId < globalMaterials.size());

    return globalMaterials[matId].get();
}

extern "C" {

GROUND_API int AddUberMaterial(const UberShaderParams* params) {
    ApiCheck(params->baseColorTexture < 0 ||
        params->baseColorTexture < globalImages.size());
    ApiCheck(params->emissionTexture < 0 ||
        params->emissionTexture < globalImages.size());

    ground::GenericMaterialParameters p {
        params->baseColorTexture < 0
            ? nullptr
            : globalImages[params->baseColorTexture].get(),

        params->emissionTexture < 0
            ? nullptr
            : globalImages[params->emissionTexture].get()
    };

    globalMaterials.emplace_back(new ground::GenericMaterial(globalScene.get(), p));
    return int(globalMaterials.size()) - 1;
}

// TODO add sanity checks to report if meshes do not have a material assigned
//      could be done in a scene validation step?

GROUND_API void AssignMaterial(int mesh, int material) {
    ApiCheck(mesh < globalScene->GetNumMeshes());
    ApiCheck(material < globalMaterials.size());

    globalMeshToMaterial[mesh] = material;
}

GROUND_API ColorRGB ComputeEmission(const SurfacePoint* point, Vector3 outDir)
{
    auto material = LookupMaterial(point->meshId);
    auto clr = material->ComputeEmission(*point, outDir);
    return ColorRGB {clr.x, clr.y, clr.z};
}

GROUND_API BsdfSample WrapPrimarySampleToBsdf(const SurfacePoint* point,
    Vector3 outDir, float u, float v, bool isOnLightSubpath)
{
    auto material = LookupMaterial(point->meshId);

    Vector3 inDir;
    auto sampleInfo = material->WrapPrimarySampleToBsdf(*point,
        &inDir, outDir, isOnLightSubpath, Vector2{u, v});

    return BsdfSample {
        inDir,
        sampleInfo.jacobian,
        sampleInfo.reverseJacobian
    };
}

GROUND_API BsdfSample ComputePrimaryToBsdfJacobian(const SurfacePoint* point,
    Vector3 outDir, Vector3 inDir, bool isOnLightSubpath)
{
    auto material = LookupMaterial(point->meshId);

    auto jacobians = material->ComputeJacobians(*point, inDir,
        outDir, isOnLightSubpath);

    // TODO refactor, no need to also return a direction here, the caller knows it anyway
    return BsdfSample {
        inDir,
        jacobians.jacobian,
        jacobians.reverseJacobian
    };
}

GROUND_API ColorRGB EvaluateBsdf(const SurfacePoint* point,
    Vector3 outDir, Vector3 inDir, bool isOnLightSubpath)
{
    auto material = LookupMaterial(point->meshId);

    auto clr = material->EvaluateBsdf(*point, inDir, outDir, isOnLightSubpath);
    return ColorRGB {clr.x, clr.y, clr.z};
}

GROUND_API float ComputeShadingCosine(const SurfacePoint* point,
    Vector3 outDir, Vector3 inDir, bool isOnLightSubpath)
{
    auto material = LookupMaterial(point->meshId);
    return material->ShadingCosine(*point, inDir, outDir, isOnLightSubpath);
}

GROUND_API int AttachDiffuseEmitter(int meshId, ColorRGB radiance) {
    auto mesh = globalScene->GetMesh(meshId);

    globalEmitters.emplace_back(new ground::DiffuseSurfaceEmitter(mesh, radiance));
    auto emitterId = int(globalEmitters.size()) - 1;

    globalMeshToEmitter[meshId] = emitterId;
    globalEmitterToMesh[emitterId] = meshId;

    return emitterId;
}

GROUND_API int GetNumberEmitters() {
    return globalEmitters.size();
}

GROUND_API int GetEmitterMesh(int emitterId) {
    ApiCheck(emitterId >= 0 && emitterId < globalEmitters.size());
    return globalEmitterToMesh[emitterId];
}

GROUND_API SurfaceSample WrapPrimarySampleToEmitterSurface(int emitterId, float u, float v) {
    ApiCheck(u >= 0 && u <= 1);
    ApiCheck(v >= 0 && v <= 1);
    ApiCheck(emitterId >= 0 && emitterId < globalEmitters.size());

    const int meshId = GetEmitterMesh(emitterId);

    auto sample = globalEmitters[emitterId]->WrapPrimaryToSurface(Vector2{ u, v });
    sample.point.meshId = meshId;
    return sample;
}

GROUND_API EmitterSample WrapPrimarySampleToEmitterRay(int emitterId,
    Vector2 primaryPos, Vector2 primaryDir)
{
    ApiCheck(emitterId >= 0 && emitterId < globalEmitters.size());

    const int meshId = GetEmitterMesh(emitterId);

    auto sample = globalEmitters[emitterId]->WrapPrimaryToRay(primaryPos, primaryDir);
    sample.surface.point.meshId = meshId;
    return sample;
}

GROUND_API float ComputePrimaryToEmitterRayJacobian(SurfacePoint origin, Vector3 direction) {
    int emitterId = globalMeshToEmitter[origin.meshId];
    return globalEmitters[emitterId]->PrimaryToRayJacobian(origin, direction);
}

GROUND_API float ComputePrimaryToEmitterSurfaceJacobian(const SurfacePoint* point) {
    int emitterId = globalMeshToEmitter[point->meshId];
    return globalEmitters[emitterId]->PrimaryToSurfaceJacobian(*point);
}

}