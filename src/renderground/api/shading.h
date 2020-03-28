#pragma once

#include <renderground/api/api.h>

extern "C" {

GROUND_API void InitShadingSystem(int spectralResolution);

GROUND_API int AddUberMaterial(const UberShaderParams* params);

GROUND_API void AssignMaterial(int mesh, int material);

GROUND_API void EvaluateBsdf(const SurfacePoint* point, float* value);

GROUND_API BsdfSample WrapPrimarySampleToBsdf(const SurfacePoint* point,
    float u, float v, float* value);

GROUND_API void ComputeEmission(const SurfacePoint* point, float* value);

}