#include "emitter.h"

namespace ground {

DiffuseSurfaceEmitter::DiffuseSurfaceEmitter(const Mesh* mesh, const ColorRGB& radiance)
    : radiance(radiance)
{
}

Vector3 DiffuseSurfaceEmitter::ComputeEmission(const SurfacePoint& point, const Vector3& outDir) const
{
    return Vector3();
}

EmitterSample DiffuseSurfaceEmitter::WrapPrimaryToRay(const SurfacePoint& point, const Vector2& primarySample) const
{
    return EmitterSample();
}

} // namespace Ground