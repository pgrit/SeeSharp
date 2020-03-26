#pragma once

#include <renderground/api/api.h>

extern "C" {

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

// Intersects the scene with a single ray.
GROUND_API Hit TraceSingle(Ray ray);

// Intersects the scene with multiple rays (in parallel, using tbb)
// The results are written to the passed buffer, assuming it is of correct size.
GROUND_API void TraceMulti(const Ray* rays, int num, Hit* hits);

}