#ifndef RENDERGROUND_RENDERGROUND_H
#define RENDERGROUND_RENDERGROUND_H

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

///////////////////////////////////////////////////////////////////////////
// Raytracing kernel API

// Initializes a new, empty scene
GROUND_API void InitScene();

// Adds a triangle mesh to the current scene.
// Vertices should be an array of 3D vectors: "x1, y1, z1, x2, y2, z2, ..." and so on
GROUND_API int AddTriangleMesh(const float* vertices, int numVerts,
    const int* indices, int numIdx);

// Transforms 2D random numbers "u,v" in [0,1] to a point on the surface
// of the given triangle mesh.
GROUND_API void SampleTriangleMesh(float u, float v);

// Builds acceleration structures to prepare the scene for ray tracing.
GROUND_API void FinalizeScene();

struct GROUND_API Hit {
    int meshId;
};

// Intersects the scene with a single ray.
GROUND_API Hit TraceSingle(const float* pos, const float* dir);

///////////////////////////////////////////////////////////////////////////
// Image I/O kernel API

// Creates a new HDR image buffer, initialized to black. Returns its ID.
GROUND_API int CreateImage(int width, int height, int numChannels);

// Splats a value into the image buffer with the given ID.
// Thread-safe (uses atomic add).
GROUND_API void AddSplat(int image, float x, float y, const float* value);

// Writes an image to the filesystem.
GROUND_API void WriteImage(int image, const char* filename);

// Loads an image from the filesystem.
GROUND_API int LoadImage(const char* filename);

///////////////////////////////////////////////////////////////////////////
// Shading system API

GROUND_API void InitShadingSystem(int spectralResolution);

struct GROUND_API UberShaderParams {
    int baseColorTexture;
    int emissionTexture;
};

GROUND_API int AddUberMaterial(const UberShaderParams* params);

GROUND_API void AssignMaterial(int mesh, int material);

GROUND_API void EvaluateBsdf(const Hit* hit);
GROUND_API void SampleBsdf(const Hit* hit, float u, float v);
GROUND_API void ComputeEmission(const Hit* hit);

}

#endif // RENDERGROUND_RENDERGROUND_H