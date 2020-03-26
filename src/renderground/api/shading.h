#pragma once

#include "renderground/api/api.h"

extern "C" {

GROUND_API void InitShadingSystem(int spectralResolution);

struct GROUND_API UberShaderParams {
    int baseColorTexture;
    int emissionTexture;
};

GROUND_API int AddUberMaterial(const UberShaderParams* params);

GROUND_API void AssignMaterial(int mesh, int material);

GROUND_API void EvaluateBsdf(const Hit* hit, float* value);
GROUND_API void SampleBsdf(const Hit* hit, float u, float v, float* value);
GROUND_API void ComputeEmission(const Hit* hit, float* value);

}