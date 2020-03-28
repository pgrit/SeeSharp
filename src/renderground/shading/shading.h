#pragma once

#include "image/image.h"
#include "geometry/hit.h"
#include "math/float3.h"

namespace ground
{

class Material {
public:
    virtual ~Material() {}

    virtual float EvaluateBsdf(const SurfacePoint& point,
        const Float3& inDir, const Float3& outDir, float wavelength,
        bool isOnLightSubpath) const = 0;

    virtual float WrapPrimarySampleToBsdf(const SurfacePoint& point,
        Float3* inDir, const Float3& outDir, float wavelength,
        bool isOnLightSubpath) const = 0;

    virtual float ComputeEmission(const SurfacePoint& point,
        const Float3& outDir, float wavelength) const = 0;
};

} // namespace ground
