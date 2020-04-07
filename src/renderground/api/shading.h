#pragma once

#include <renderground/api/api.h>

extern "C" {

GROUND_API int AddUberMaterial(const UberShaderParams* params);

GROUND_API void AssignMaterial(int mesh, int material);

GROUND_API ColorRGB EvaluateBsdf(const SurfacePoint* point,
    Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);

GROUND_API float ComputeShadingCosine(const SurfacePoint* point,
    Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);

GROUND_API BsdfSample WrapPrimarySampleToBsdf(const SurfacePoint* point,
    Vector3 outDir, float u, float v, bool isOnLightSubpath);

GROUND_API BsdfSample ComputePrimaryToBsdfJacobian(const SurfacePoint* point,
    Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);

/// Attaches a diffuse emitter to a mesh.
/// The mesh must not already have any emitter attached to it.
///
/// \param  meshId      The ID of the mesh to attach the emitter to.
/// \param  radiance    Emitted radiance by any surface point in any direction, in RGB
///
/// \returns The unique ID of the newly created emitter.
GROUND_API int AttachDiffuseEmitter(int meshId, ColorRGB radiance);

GROUND_API ColorRGB ComputeEmission(const SurfacePoint* point, Vector3 outDir);

GROUND_API int GetNumberEmitters();

/// Returns the ID of the mesh to which the given emitter is attached.
GROUND_API int GetEmitterMesh(int emitterId);

/// Wraps primary sample space to the surface of an emitter.
GROUND_API SurfaceSample WrapPrimarySampleToEmitterSurface(int emitterId, float u, float v);

/// Computes the jacobian of the mapping from primary sample space to an emitter's surface.
GROUND_API float ComputePrimaryToEmitterSurfaceJacobian(const SurfacePoint* point);

GROUND_API EmitterSample WrapPrimarySampleToEmitterRay(int emitterId,
    Vector2 primaryPos, Vector2 primaryDir);

GROUND_API float ComputePrimaryToEmitterRayJacobian(SurfacePoint origin,
    Vector3 direction);

}