#include "emitter.h"
#include "math/wrap.h"

namespace ground {

DiffuseSurfaceEmitter::DiffuseSurfaceEmitter(const Mesh* mesh, const ColorRGB& radiance)
: radiance(radiance), mesh(mesh)
{
}

ColorRGB DiffuseSurfaceEmitter::ComputeEmission(const SurfacePoint& point, 
    const Vector3& outDir) const  
{
    auto shadingNormal = mesh->ComputeShadingNormal(point.primId, point.barycentricCoords);
    float cosine = Dot(outDir, shadingNormal);

    // The light only emits in the hemisphere defined by the shading normal.
    if (cosine <= 0) return ColorRGB{ 0, 0, 0 };

    return radiance;
}

SurfaceSample DiffuseSurfaceEmitter::WrapPrimaryToSurface(const Vector2& primarySample) const {
    return mesh->PrimarySampleToSurface(primarySample);
}

float DiffuseSurfaceEmitter::PrimaryToSurfaceJacobian(const SurfacePoint& sample) const {
    return mesh->ComputePrimaryToSurfaceJacobian(sample);
}

EmitterSample DiffuseSurfaceEmitter::WrapPrimaryToRay(const Vector2& primaryPos, 
    const Vector2& primaryDir) const
{
    auto surfaceSample = WrapPrimaryToSurface(primaryPos);

    // Wrap the primary sample to the hemisphere about the shading normal of the mesh.
    auto dirSample = WrapToCosHemisphere(primaryDir);

    auto shadingNormal = mesh->ComputeShadingNormal(
        surfaceSample.point.primId, surfaceSample.point.barycentricCoords);

    // Transform the hemisphere coordinates to world space.
    Vector3 tangent, binormal;
    ComputeBasisVectors(shadingNormal, tangent, binormal);
    Vector3 dir = shadingNormal * dirSample.direction.z
        + tangent * dirSample.direction.x
        + binormal * dirSample.direction.y;

    float cosine = Dot(shadingNormal, dir);
    CheckTrue(cosine >= 0);

    EmitterSample sample{
        surfaceSample,
        dir,
        dirSample.jacobian,
        cosine
    };

    return sample;
}

float DiffuseSurfaceEmitter::PrimaryToRayJacobian(const SurfacePoint& point, 
    const Vector3& dir) const
{
    auto shadingNormal = mesh->ComputeShadingNormal(point.primId, point.barycentricCoords);    
    float cosine = Dot(dir, shadingNormal);

    // The light only emits in the hemisphere defined by the shading normal.
    if (cosine <= 0) return 0.0f;

    return ComputeCosHemisphereJacobian(cosine);
}

} // namespace Ground