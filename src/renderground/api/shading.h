#pragma once

#include <renderground/api/api.h>

extern "C" {

GROUND_API int AddUberMaterial(const UberShaderParams* params);

GROUND_API void AssignMaterial(int mesh, int material);

GROUND_API ColorRGB EvaluateBsdf(const SurfacePoint* point,
    Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);

GROUND_API BsdfSample WrapPrimarySampleToBsdf(const SurfacePoint* point,
    Vector3 outDir, float u, float v, bool isOnLightSubpath);

GROUND_API BsdfSample ComputePrimaryToBsdfJacobian(const SurfacePoint* point,
    Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);

GROUND_API ColorRGB ComputeEmission(const SurfacePoint* point, Vector3 outDir);

GROUND_API int GetNumberEmitters();

GROUND_API int GetEmitterMesh(int emitterId);

}