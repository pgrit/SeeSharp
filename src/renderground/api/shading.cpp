#include "api/shading.h"
#include "api/internal.h"

#include "shading/generic.h"

#include <unordered_map>

std::vector<std::unique_ptr<ground::Material>> globalMaterials;
std::unordered_map<int, int> globalMeshToMaterial;

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

    globalMaterials.emplace_back(new ground::GenericMaterial(&globalScene, p));
    return int(globalMaterials.size()) - 1;
}

// TODO add sanity checks to report if meshes do not have a material assigned
//      could be done in a scene validation step?

GROUND_API void AssignMaterial(int mesh, int material) {
    ApiCheck(mesh < globalScene.GetNumMeshes());
    ApiCheck(material < globalMaterials.size());

    globalMeshToMaterial[mesh] = material;
}

GROUND_API ColorRGB ComputeEmission(const SurfacePoint* point, Vector3 outDir)
{
    auto material = LookupMaterial(point->meshId);
    auto clr = material->ComputeEmission(ApiToInternal(*point),
        ApiToInternal(outDir));
    return ColorRGB {clr.x, clr.y, clr.z};
}

GROUND_API BsdfSample WrapPrimarySampleToBsdf(const SurfacePoint* point,
    Vector3 outDir, float u, float v, bool isOnLightSubpath)
{
    auto material = LookupMaterial(point->meshId);

    ground::Float3 inDir;
    auto sampleInfo = material->WrapPrimarySampleToBsdf(ApiToInternal(*point),
        &inDir, ApiToInternal(outDir), isOnLightSubpath,
        ground::Float2(u, v));

    return BsdfSample {
        InternalToApi(inDir),
        sampleInfo.jacobian,
        sampleInfo.reverseJacobian
    };
}

GROUND_API BsdfSample ComputePrimaryToBsdfJacobian(const SurfacePoint* point,
    Vector3 outDir, Vector3 inDir, bool isOnLightSubpath)
{
    auto material = LookupMaterial(point->meshId);

    auto jacobians = material->ComputeJacobians(ApiToInternal(*point), ApiToInternal(inDir),
        ApiToInternal(outDir), isOnLightSubpath);

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

    auto clr = material->EvaluateBsdf(ApiToInternal(*point), ApiToInternal(inDir),
        ApiToInternal(outDir), isOnLightSubpath);
    return ColorRGB {clr.x, clr.y, clr.z};
}

}