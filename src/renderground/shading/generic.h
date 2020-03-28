#pragma once

#include "shading/shading.h"

namespace ground {

struct GenericMaterialParameters {
    const Image* baseColor;
    const Image* emission;
};

class GenericMaterial : public Material {
public:
    GenericMaterial(const GenericMaterialParameters& params);

    float EvaluateBsdf(const SurfacePoint& point,
        const Float3& inDir, const Float3& outDir, float wavelength,
        bool isOnLightSubpath) const final;

    float WrapPrimarySampleToBsdf(const SurfacePoint& point,
        Float3* inDir, const Float3& outDir, float wavelength,
        bool isOnLightSubpath) const final;

    float ComputeEmission(const SurfacePoint& point,
        const Float3& outDir, float wavelength) const final;
};

} // namespace ground