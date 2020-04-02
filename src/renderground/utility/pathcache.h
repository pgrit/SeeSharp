#pragma once

#include "api/types.h"

#include <vector>
#include <atomic>

namespace ground
{

class PathCache {
public:
    PathCache(int initialSize);

    int Add(const PathVertex& vertex);

    PathVertex& operator[] (int idx) {
        return vertexCache[idx];
    }

    void Clear();

private:
    std::vector<PathVertex> vertexCache;
    std::atomic<int> next;

    // Tracks the number of vertices that had to be discarded
    std::atomic<int> overflow;
};

} // namespace ground
