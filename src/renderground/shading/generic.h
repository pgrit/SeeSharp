#pragma once

#include "shading/shading.h"

namespace ground {

struct GenericMaterialParameters {
    const Image* baseColor;
};

class GenericMaterial : public Material {
public:
    GenericMaterial(const Scene* scene, const GenericMaterialParameters& params);

    Vector3 EvaluateBsdf(const SurfacePoint& point, const Vector3& inDir,
        const Vector3& outDir, bool isOnLightSubpath) const final;

    float ShadingCosine(const SurfacePoint& point, const Vector3& inDir,
        const Vector3& outDir, bool isOnLightSubpath) const final;

    BsdfSampleInfo WrapPrimarySampleToBsdf(const SurfacePoint& point,
        Vector3* inDir, const Vector3& outDir, bool isOnLightSubpath,
        const Vector2& primarySample) const final;

    BsdfSampleInfo ComputeJacobians(const SurfacePoint& point,
        const Vector3& inDir, const Vector3& outDir,
        bool isOnLightSubpath) const final;

private:
    GenericMaterialParameters parameters;
};

} // namespace ground