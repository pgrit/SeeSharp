#pragma once

#include <renderground/api/api.h>

extern "C" {

// Initializes a new cache that can hold up to "initialSize" path vertices.
// Returns the id of the newly created cache.
GROUND_API int CreatePathCache(int initialSize);

GROUND_API int AddPathVertex(int cacheId, PathVertex vertex);
GROUND_API PathVertex GetPathVertex(int cacheId, int vertexId);

GROUND_API void ClearPathCache(int cacheId);
GROUND_API void DeletePathCache(int cacheId);

}