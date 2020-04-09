#include "utility/pathcache.h"

#include <cassert>
#include <iostream>

namespace ground
{

PathCache::PathCache(int initialSize)
: vertexCache(initialSize)
{
    next = 0;
    overflow = 0;
    Clear();
}

int PathCache::Add(const PathVertex& vertex) {
    auto idx = next++;

    if (idx >= vertexCache.size()) {
        ++overflow;
        return -1;
    }

    vertexCache[idx] = vertex;
    return idx;
}

void PathCache::Clear() {
    if (overflow > 0) {
        vertexCache.resize(vertexCache.size() + overflow * 2);

        // TODO should put this in some simple logging tool with verbosity levels.
        std::cout << "Info: A path cache overflow discarded a total of "
                  << overflow << " vertices. "
                  << "Allocating additional memory (twice that number). "
                  << "The total size is now: " << vertexCache.size() << std::endl;
    }

    next = 0;
    overflow = 0;
}

} // namespace ground
