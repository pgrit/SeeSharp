#include <gtest/gtest.h>

#include <renderground/renderground.h>

#include "../testutil.h"

class PathCacheTests : public testing::Test {
protected:
    PathCacheTests() {}

    virtual ~PathCacheTests() {}

    virtual void SetUp() {
    }

    virtual void TearDown() {
    }
};

TEST_F(PathCacheTests, OverflowDetection) {
    int initialSize = 8;
    int cacheId = CreatePathCache(initialSize);

    PathVertex dummy;
    for (int i = 0; i < initialSize; ++i) {
        dummy.ancestorId = i;
        int newId = AddPathVertex(cacheId, dummy);

        // vertices should be added in sequential order 
        EXPECT_EQ(newId, i);
    }

    // Trying to add more vertices beyond the capacity should be handled gracefully
    int numOverflow = 4;
    for (int i = 0; i < numOverflow; ++i)
        EXPECT_LT(AddPathVertex(cacheId, dummy), 0);
    
    // Assert that the indices we have written to the vertices are correct still
    for (int i = 0; i < initialSize; ++i) {
        EXPECT_EQ(GetPathVertex(cacheId, i).ancestorId, i);
    }

    // Clearing the cache should now add space for numOverflow * 2 additional vertices
    ClearPathCache(cacheId);

    for (int i = 0; i < initialSize + 2 * numOverflow; ++i) {
        dummy.ancestorId = i;
        int newId = AddPathVertex(cacheId, dummy);

        // vertices should be added in sequential order 
        EXPECT_EQ(newId, i);
    }
}