#pragma once

#include "image/image.h"
#include "geometry/hit.h"
#include "math/float3.h"

namespace ground
{

struct BsdfSampleInfo {
    float jacobian;
    float reverseJacobian;
};

class Material {
public:
    virtual ~Material() {}

    virtual float EvaluateBsdf(const SurfacePoint& point,
        const Float3& inDir, const Float3& outDir, float wavelength,
        bool isOnLightSubpath) const = 0;

    virtual BsdfSampleInfo WrapPrimarySampleToBsdf(const SurfacePoint& point,
        Float3* inDir, const Float3& outDir, float wavelength,
        bool isOnLightSubpath, const Float2& primarySample) const = 0;

    virtual float ComputeEmission(const SurfacePoint& point,
        const Float3& outDir, float wavelength) const = 0;

    virtual BsdfSampleInfo ComputeJacobians(const SurfacePoint& point,
        const Float3& inDir, const Float3& outDir, float wavelength,
        bool isOnLightSubpath) const = 0;
};

} // namespace ground
