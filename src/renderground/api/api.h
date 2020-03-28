#pragma once

// Used to generate correct DLL linkage on Windows
#ifdef GROUND_DLL
    #ifdef GROUND_EXPORTS
        #define GROUND_API __declspec(dllexport)
    #else
        #define GROUND_API __declspec(dllimport)
    #endif
#else
    #define GROUND_API
#endif

extern "C" {

struct GROUND_API Vector3 {
    float x, y, z;
};

struct GROUND_API Vector2 {
    float x, y;
};

struct GROUND_API Ray {
    Vector3 origin;
    Vector3 direction;
    float minDistance;
};

struct GROUND_API SurfacePoint {
    Vector3 position;
    Vector3 normal;
    Vector2 barycentricCoords;
    int meshId;
    int primId;
};

struct GROUND_API Hit {
    SurfacePoint point;
    float distance;
    float errorOffset;
};

struct GROUND_API SurfaceSample {
    SurfacePoint point;
    float jacobian;
};

struct GROUND_API BsdfSample {
    Vector3 direction;
    float jacobian;
};

struct GROUND_API UberShaderParams {
    int baseColorTexture;
    int emissionTexture;
};

}