#include "api/shading.h"
#include "api/internal.h"

#include "shading/generic.h"

#include <unordered_map>

std::vector<std::unique_ptr<ground::Material>> globalMaterials;
std::unordered_map<int, int> globalMeshToMaterial;

extern "C" {

GROUND_API int AddUberMaterial(const UberShaderParams* params) {
    ApiAssert(params->baseColorTexture < globalImages.size());
    ApiAssert(params->emissionTexture < globalImages.size());

    ground::GenericMaterialParameters p {
        globalImages[params->baseColorTexture].get(),
        globalImages[params->emissionTexture].get()
    };
    globalMaterials.emplace_back(new ground::GenericMaterial(p));
    return int(globalMaterials.size()) - 1;
}

GROUND_API void AssignMaterial(int mesh, int material) {
    ApiAssert(mesh < globalScene.GetNumMeshes());
    ApiAssert(material < globalMaterials.size());

    globalMeshToMaterial[mesh] = material;
}

GROUND_API void ComputeEmission(const SurfacePoint* point, float* value) {
    *value = 1.0f;
}

GROUND_API BsdfSample WrapPrimarySampleToBsdf(const SurfacePoint* point,
    float u, float v, float* value)
{
    *value = 1.0f;

    return BsdfSample {
        point->normal,
        1.0f
    };
}

GROUND_API void EvaluateBsdf(const SurfacePoint* point, float* value) {

}

}