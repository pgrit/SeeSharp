#include <gtest/gtest.h>

#include <renderground/renderground.h>

class ShadingNormalTests : public testing::Test {
protected:
    ShadingNormalTests() {}

    virtual ~ShadingNormalTests() {}

    virtual void SetUp() {
        int frameBufferId = CreateImageRGB(1, 1);
        InitScene();
        loaded = LoadSceneFromFile("../../data/scenes/simpledi.json", frameBufferId);
        FinalizeScene();
    }

    virtual void TearDown() {
        DeleteScene();
    }

    bool loaded = false;
};

TEST_F(ShadingNormalTests, CorrectCosineSigns) {
    EXPECT_TRUE(loaded);

    Ray r {
        Vector3 { 0, 1.8f, 0 },
        Vector3 { 0, -1, 0 },
        0.0f
    };

    Hit h = TraceSingle(r);

    EXPECT_FLOAT_EQ(h.distance, 1.8f);

    EXPECT_FLOAT_EQ(h.point.normal.x, 0);
    EXPECT_FLOAT_EQ(h.point.normal.y, 1);
    EXPECT_FLOAT_EQ(h.point.normal.z, 0);

    float shadingCosAbove = ComputeShadingCosine(&h.point, Vector3 { 0, 1, 0 }, Vector3 { 0, 1, 0 }, false);
    EXPECT_FLOAT_EQ(shadingCosAbove, 1.0f);

    float shadingCosAboveT = ComputeShadingCosine(&h.point, Vector3 { 0, 1, 0 }, Vector3 { 0, -1, 0 }, false);
    EXPECT_FLOAT_EQ(shadingCosAboveT, -1.0f);

    float shadingCosBelow = ComputeShadingCosine(&h.point, Vector3 { 0, -1, 0 }, Vector3 { 0, -1, 0 }, false);
    EXPECT_FLOAT_EQ(shadingCosBelow, 1.0f);

    float shadingCosBelowT = ComputeShadingCosine(&h.point, Vector3 { 0, -1, 0 }, Vector3 { 0, 1, 0 }, false);
    EXPECT_FLOAT_EQ(shadingCosBelowT, -1.0f);
}

TEST_F(ShadingNormalTests, CorrectSamplingSigns) {
    EXPECT_TRUE(loaded);

    Ray r {
        Vector3 { 0, 1.8f, 0 },
        Vector3 { 0, -1, 0 },
        0.0f
    };

    Hit h = TraceSingle(r);

    EXPECT_FLOAT_EQ(h.distance, 1.8f);

    EXPECT_FLOAT_EQ(h.point.normal.x, 0);
    EXPECT_FLOAT_EQ(h.point.normal.y, 1);
    EXPECT_FLOAT_EQ(h.point.normal.z, 0);

    BsdfSample sampleAbove = WrapPrimarySampleToBsdf(&h.point, Vector3 { 0, 1, 0 }, 0.5f, 0.5f, false);
    EXPECT_GT(sampleAbove.direction.y, 0);

    BsdfSample sampleBelow = WrapPrimarySampleToBsdf(&h.point, Vector3 { 0, -1, 0 }, 0.5f, 0.5f, false);
    EXPECT_LT(sampleBelow.direction.y, 0);
}

TEST_F(ShadingNormalTests, NormalizedCosines) {
    EXPECT_TRUE(loaded);

    Ray r {
        Vector3 { 0, 1.8f, 0 },
        Vector3 { 0, -1, 0 },
        0.0f
    };

    Hit h = TraceSingle(r);

    EXPECT_FLOAT_EQ(h.distance, 1.8f);

    EXPECT_FLOAT_EQ(h.point.normal.x, 0);
    EXPECT_FLOAT_EQ(h.point.normal.y, 1);
    EXPECT_FLOAT_EQ(h.point.normal.z, 0);

    float shadingCosLarge = ComputeShadingCosine(&h.point, Vector3 { 0, 1, 0 }, Vector3 { 0, 10, 0 }, false);
    EXPECT_FLOAT_EQ(shadingCosLarge, 1.0f);

    float shadingCosTiny = ComputeShadingCosine(&h.point, Vector3 { 0, 1, 0 }, Vector3 { 0, 0.0001f, 0 }, false);
    EXPECT_FLOAT_EQ(shadingCosTiny, 1.0f);
}