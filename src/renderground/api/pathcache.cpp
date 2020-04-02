#include "api/pathcache.h"
#include "api/internal.h"
#include "utility/pathcache.h"

std::vector<std::unique_ptr<ground::PathCache>> globalPathCaches;

extern "C" {

GROUND_API int CreatePathCache(int initialSize) {
    globalPathCaches.emplace_back(new ground::PathCache(initialSize));
    return globalPathCaches.size() - 1;
}

GROUND_API int AddPathVertex(int cacheId, PathVertex vertex) {
    ApiCheck(cacheId < globalPathCaches.size());
    return globalPathCaches[cacheId]->Add(vertex);
}

GROUND_API PathVertex GetPathVertex(int cacheId, int vertexId) {
    ApiCheck(cacheId < globalPathCaches.size());
    return (*globalPathCaches[cacheId])[vertexId];
}

GROUND_API void ClearPathCache(int cacheId) {
    ApiCheck(cacheId < globalPathCaches.size());
    globalPathCaches[cacheId]->Clear();
}

GROUND_API void DeletePathCache(int cacheId) {
    ApiCheck(cacheId < globalPathCaches.size());
    globalPathCaches[cacheId].release();
    // TODO we should keep track that this entity was deleted, and
    //      add checks that it is not accessed inadvertently
}

}