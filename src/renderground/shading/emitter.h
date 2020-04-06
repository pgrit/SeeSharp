#pragma once

#include "geometry/mesh.h"

namespace ground {

class Emitter {
public:
    virtual ~Emitter() {}

    virtual Vector3 ComputeEmission(const SurfacePoint& point,
        const Vector3& outDir) const = 0;

    virtual EmitterSample WrapPrimaryToRay(const SurfacePoint& point,
        const Vector2& primarySample) const = 0;
};

class DiffuseSurfaceEmitter : public Emitter {
public:
    DiffuseSurfaceEmitter(const Mesh* mesh, const ColorRGB& radiance);

    Vector3 ComputeEmission(const SurfacePoint& point, const Vector3& outDir) const final;

    EmitterSample WrapPrimaryToRay(const SurfacePoint& point, const Vector2& primarySample) const final;

private:
    ColorRGB radiance;
};

} // namespace Ground