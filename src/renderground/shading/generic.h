#pragma once

#include "shading/shading.h"

namespace ground {

struct GenericMaterialParameters {
    const Image* baseColor;
    const Image* emission;
};

class GenericMaterial : public Material {
public:
    GenericMaterial(const Scene* scene, const GenericMaterialParameters& params);

    float EvaluateBsdf(const SurfacePoint& point,
        const Float3& inDir, const Float3& outDir, float wavelength,
        bool isOnLightSubpath) const final;

    BsdfSampleInfo WrapPrimarySampleToBsdf(const SurfacePoint& point,
        Float3* inDir, const Float3& outDir, float wavelength,
        bool isOnLightSubpath, const Float2& primarySample) const final;

    float ComputeEmission(const SurfacePoint& point,
        const Float3& outDir, float wavelength) const final;

    BsdfSampleInfo ComputeJacobians(const SurfacePoint& point,
        const Float3& inDir, const Float3& outDir, float wavelength,
        bool isOnLightSubpath) const final;

private:
    GenericMaterialParameters parameters;
};

} // namespace ground