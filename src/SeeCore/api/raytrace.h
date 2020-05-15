#pragma once

#include <SeeCore/api/api.h>

extern "C" {

// Initializes a new, empty scene
SEE_CORE_API void InitScene();

SEE_CORE_API void DeleteScene();

// Adds a triangle mesh to the current scene.
// Vertices should be an array of 3D vectors: "x1, y1, z1, x2, y2, z2, ..." and so on
SEE_CORE_API int AddTriangleMesh(const float* vertices, int numVerts, const int* indices, int numIdx);

// Builds acceleration structures to prepare the scene for ray tracing.
SEE_CORE_API void FinalizeScene();

// Intersects the scene with a single ray.
SEE_CORE_API Hit TraceSingle(Ray ray);

}