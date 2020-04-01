#pragma once

#include "image/image.h"
#include "geometry/hit.h"
#include "geometry/scene.h"
#include "math/float3.h"

namespace ground
{

struct BsdfSampleInfo {
    float jacobian;
    float reverseJacobian;
};

class Material {
public:
    Material(const Scene* scene) : scene(scene) {}
    virtual ~Material() {}

    virtual Float3 EvaluateBsdf(const SurfacePoint& point,
        const Float3& inDir, const Float3& outDir,
        bool isOnLightSubpath) const = 0;

    virtual BsdfSampleInfo WrapPrimarySampleToBsdf(const SurfacePoint& point,
        Float3* inDir, const Float3& outDir, bool isOnLightSubpath,
        const Float2& primarySample) const = 0;

    virtual Float3 ComputeEmission(const SurfacePoint& point,
        const Float3& outDir) const = 0;

    virtual BsdfSampleInfo ComputeJacobians(const SurfacePoint& point,
        const Float3& inDir, const Float3& outDir,
        bool isOnLightSubpath) const = 0;

    virtual bool IsEmissive() const = 0;

protected:
    const Scene* scene;
};

} // namespace ground
