#include <gtest/gtest.h>

#include <renderground/renderground.h>

class DiffuseTests : public testing::Test {
protected:
    DiffuseTests() {}

    virtual ~DiffuseTests() {}

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

TEST_F(DiffuseTests, Albedo) {
    EXPECT_TRUE(loaded);

    Ray r {
        Vector3 { 0, 1.8f, 0 },
        Vector3 { 0, -1, 0 },
        0.0f
    };

    Hit h = TraceSingle(r);

    EXPECT_FLOAT_EQ(h.distance, 1.8f);

    Vector3 outDir { 0, 10, 0 };
    const int N = 16;
    ColorRGB albedo { 0, 0, 0};
    for (float u = 0.0f + FLT_EPSILON; u < 1.0f; u += 1.0f / N) {
        for (float v = 0.0f + FLT_EPSILON; v < 1.0f; v += 1.0f / N) {
            BsdfSample s = WrapPrimarySampleToBsdf(&h.point, outDir, u, v, false);
            auto bsdfValue = EvaluateBsdf(&h.point, outDir, s.direction, false);
            float cos = ComputeShadingCosine(&h.point, outDir, s.direction, false);

            EXPECT_FALSE(std::isinf(cos));
            EXPECT_FALSE(std::isnan(cos));

            EXPECT_FALSE(std::isinf(s.jacobian));
            EXPECT_FALSE(std::isnan(s.jacobian));

            EXPECT_FLOAT_EQ(bsdfValue.r, 1.0f / 3.1415926f);

            EXPECT_GT(s.jacobian, 0);
            EXPECT_LT(s.jacobian, 1);

            albedo = albedo + bsdfValue * (cos / s.jacobian);
        }
    }
    EXPECT_FLOAT_EQ(albedo.r, N * N);
}

TEST_F(DiffuseTests, AlbedoBelow) {
    EXPECT_TRUE(loaded);

    Ray r {
        Vector3 { 0, 1.8f, 0 },
        Vector3 { 0, -1, 0 },
        0.0f
    };

    Hit h = TraceSingle(r);

    EXPECT_FLOAT_EQ(h.distance, 1.8f);

    Vector3 outDir { 0, -10, 0 };
    const int N = 16;
    ColorRGB albedo { 0, 0, 0};
    for (float u = 0.0f + FLT_EPSILON; u < 1.0f; u += 1.0f / N) {
        for (float v = 0.0f + FLT_EPSILON; v < 1.0f; v += 1.0f / N) {
            BsdfSample s = WrapPrimarySampleToBsdf(&h.point, outDir, u, v, false);
            auto bsdfValue = EvaluateBsdf(&h.point, outDir, s.direction, false);
            float cos = ComputeShadingCosine(&h.point, outDir, s.direction, false);

            EXPECT_FALSE(std::isinf(cos));
            EXPECT_FALSE(std::isnan(cos));

            EXPECT_FALSE(std::isinf(s.jacobian));
            EXPECT_FALSE(std::isnan(s.jacobian));

            EXPECT_FLOAT_EQ(bsdfValue.r, 1.0f / 3.1415926f);

            EXPECT_GT(s.jacobian, 0);
            EXPECT_LT(s.jacobian, 1);

            albedo = albedo + bsdfValue * (cos / s.jacobian);
        }
    }
    EXPECT_FLOAT_EQ(albedo.r, N * N);
}