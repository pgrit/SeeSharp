#pragma once

#include <SeeCore/api/api.h>

extern "C" {

// Initializes a new, empty scene
SEE_CORE_API int InitScene();

// Adds a triangle mesh to the current scene.
// Vertices should be an array of 3D vectors: "x1, y1, z1, x2, y2, z2, ..." and so on
SEE_CORE_API int AddTriangleMesh(int scene, const float* vertices, int numVerts, const int* indices, int numIdx);

// Builds acceleration structures to prepare the scene for ray tracing.
SEE_CORE_API void FinalizeScene(int scene);

// Intersects the scene with a single ray.
SEE_CORE_API Hit TraceSingle(int scene, Ray ray);

}