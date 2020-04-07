#pragma once

#include "image/image.h"
#include "geometry/scene.h"
#include "api/cpputils.h"

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

    virtual Vector3 EvaluateBsdf(const SurfacePoint& point,
        const Vector3& inDir, const Vector3& outDir,
        bool isOnLightSubpath) const = 0;

    virtual float ShadingCosine(const SurfacePoint& point,
        const Vector3& inDir, const Vector3& outDir,
        bool isOnLightSubpath) const = 0;

    virtual BsdfSampleInfo WrapPrimarySampleToBsdf(const SurfacePoint& point,
        Vector3* inDir, const Vector3& outDir, bool isOnLightSubpath,
        const Vector2& primarySample) const = 0;

    virtual BsdfSampleInfo ComputeJacobians(const SurfacePoint& point,
        const Vector3& inDir, const Vector3& outDir,
        bool isOnLightSubpath) const = 0;

protected:
    const Scene* scene;
};

} // namespace ground
