#pragma once

#include "geometry/mesh.h"

namespace ground {

class Emitter {
public:
    virtual ~Emitter() {}

    virtual ColorRGB ComputeEmission(const SurfacePoint& point,
        const Vector3& outDir) const = 0;

    virtual SurfaceSample WrapPrimaryToSurface(const Vector2& primarySample) const = 0;
    virtual float PrimaryToSurfaceJacobian(const SurfacePoint& sample) const = 0;
    virtual EmitterSample WrapPrimaryToRay(const Vector2& primaryPos, const Vector2& primaryDir) const = 0;
    virtual float PrimaryToRayJacobian(const SurfacePoint& point, const Vector3& dir) const = 0;
};

class DiffuseSurfaceEmitter : public Emitter {
public:
    DiffuseSurfaceEmitter(const Mesh* mesh, const ColorRGB& radiance);

    ColorRGB ComputeEmission(const SurfacePoint& point, const Vector3& outDir) const final;

    SurfaceSample WrapPrimaryToSurface(const Vector2& primarySample) const final;
    float PrimaryToSurfaceJacobian(const SurfacePoint& sample) const final;

    EmitterSample WrapPrimaryToRay(const Vector2& primaryPos, const Vector2& primaryDir) const final;
    float PrimaryToRayJacobian(const SurfacePoint& point, const Vector3& dir) const final;

private:
    ColorRGB radiance;
    const Mesh* mesh;
};

} // namespace Ground