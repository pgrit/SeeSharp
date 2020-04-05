#pragma once

#include <renderground/api/api.h>

extern "C" {

// Initializes a new, empty scene
GROUND_API void InitScene();

GROUND_API void DeleteScene();

// Adds a triangle mesh to the current scene.
// Vertices should be an array of 3D vectors: "x1, y1, z1, x2, y2, z2, ..." and so on
GROUND_API int AddTriangleMesh(const float* vertices, int numVerts,
    const int* indices, int numIdx, const float* texCoords, const float* shadingNormals);

// Transforms 2D random numbers "u,v" in [0,1] to a point on the surface
// of the given triangle mesh.
GROUND_API SurfaceSample WrapPrimarySampleToSurface(int meshId, float u, float v);

// Computes the jacobian of the transform from primary sample space to
// the surface of the triangle mesh on which the given point lies.
GROUND_API float ComputePrimaryToSurfaceJacobian(const SurfacePoint* point);

GROUND_API EmitterSample WrapPrimarySampleToEmitterRay(SurfacePoint origin,
    Vector2 primaryPos, Vector2 primaryDir);

GROUND_API Vector2 ComputePrimaryToEmitterRayJacobian(SurfacePoint origin, Vector3 direction);

// Builds acceleration structures to prepare the scene for ray tracing.
GROUND_API void FinalizeScene();

// Intersects the scene with a single ray.
GROUND_API Hit TraceSingle(Ray ray);

GROUND_API Vector3 ComputeShadingNormal(SurfacePoint point);

// Intersects the scene with multiple rays (in parallel, using tbb)
// The results are written to the passed buffer, assuming it is of correct size.
GROUND_API void TraceMulti(const Ray* rays, int num, Hit* hits);

// Checks wether the point "to" is visible from the surface point "from".
GROUND_API bool IsOccluded(const Hit* from, Vector3 to);

// Creates and returns a ray starting at the surface point "from" with proper
// offsets for self-intersection handling.
GROUND_API Ray SpawnRay(const Hit* from, Vector3 direction);

GROUND_API GeometryTerms ComputeGeometryTerms(const SurfacePoint* from, const SurfacePoint* to);

}