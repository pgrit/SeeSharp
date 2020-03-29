#pragma once

#include <renderground/api/api.h>

extern "C" {

GROUND_API int AddUberMaterial(const UberShaderParams* params);

GROUND_API void AssignMaterial(int mesh, int material);

GROUND_API float EvaluateBsdf(const SurfacePoint* point,
    Vector3 outDir, Vector3 inDir, float wavelength, bool isOnLightSubpath);

GROUND_API BsdfSample WrapPrimarySampleToBsdf(const SurfacePoint* point,
    Vector3 outDir, float u, float v, float wavelength, bool isOnLightSubpath);

GROUND_API BsdfSample ComputePrimaryToBsdfJacobian(const SurfacePoint* point,
    Vector3 outDir, Vector3 inDir, float wavelength, bool isOnLightSubpath);

GROUND_API float ComputeEmission(const SurfacePoint* point, Vector3 outDir,
    float wavelength);

}