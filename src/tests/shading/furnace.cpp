#include <gtest/gtest.h>

#include <renderground/renderground.h>

class DiffuseFurnace : public testing::Test {
protected:
    DiffuseFurnace() {}

    virtual ~DiffuseFurnace() {}

    virtual void SetUp() {
        int frameBufferId = CreateImageRGB(imageWidth, imageHeight);
        InitScene();
        loaded = LoadSceneFromFile("../../data/scenes/furnacebox.json", frameBufferId);
        FinalizeScene();
    }

    virtual void TearDown() {
        DeleteScene();
    }

    bool loaded = false;
    int imageHeight = 512;
    int imageWidth = 512;
};

TEST_F(DiffuseFurnace, SinglePixel) {
    EXPECT_TRUE(loaded);

    Vector3 dir { 0.06147575f, -0.12802716f,  -0.98986346f };
    Vector3 org { 0, 1, 6.8f };
    Ray ray {
        org, dir, 0.0f
    };

    auto hit = TraceSingle(ray);

    EXPECT_EQ(hit.point.meshId, 0);
    EXPECT_GT(Dot(-ray.direction, hit.point.normal), 0.0f);

    float u = 0.893f;
    float v = 0.31f;
    auto bsdfSample = WrapPrimarySampleToBsdf(&hit.point,
        -ray.direction, u, v, false);

    auto bsdfValue = EvaluateBsdf(&hit.point, -ray.direction,
        bsdfSample.direction, false);

    float shadingCosine = ComputeShadingCosine(&hit.point, -ray.direction,
        bsdfSample.direction, false);
    EXPECT_GT(shadingCosine, 0.0f);
    EXPECT_LT(shadingCosine, 1.0f);
    float expectedCos = Dot(bsdfSample.direction, hit.point.normal);
    EXPECT_NEAR(shadingCosine, expectedCos, 0.001f);

    auto shnorm = ComputeShadingNormal(hit.point);
    EXPECT_GT(Dot(shnorm, hit.point.normal), 0.0f);
    EXPECT_FLOAT_EQ(Length(shnorm), 1.0f);
    EXPECT_NEAR(shnorm.x, hit.point.normal.x, 0.001f);
    EXPECT_NEAR(shnorm.y, hit.point.normal.y, 0.001f);
    EXPECT_NEAR(shnorm.z, hit.point.normal.z, 0.001f);

    float shcos = Dot(shnorm, bsdfSample.direction);
    EXPECT_NEAR(shcos, shadingCosine, 0.001f);

    auto emission = ColorRGB{ 1, 1, 1 };

    auto bsdfRay = SpawnRay(&hit, bsdfSample.direction);
    auto bsdfhit = TraceSingle(bsdfRay);

    EXPECT_EQ(bsdfhit.point.meshId, -1);

    if (bsdfhit.point.meshId == -1) {
        auto emission = ColorRGB{ 1, 1, 1 };

        auto value = emission * bsdfValue
            * (shadingCosine / bsdfSample.jacobian);

        EXPECT_NEAR(value.r, 1.0f, 0.001f);
        EXPECT_NEAR(value.g, 1.0f, 0.001f);
        EXPECT_NEAR(value.b, 1.0f, 0.001f);
    }
}

TEST_F(DiffuseFurnace, AllWhite) {
    EXPECT_TRUE(loaded);

    const uint64_t BaseSeed = 0xC030114Ui64;
    for(int y = 0; y < imageHeight; ++y) {
        for (int x = 0; x < imageWidth; ++x) {
            auto h1 = HashSeed(BaseSeed, (y * imageWidth + x));
            auto h2 = HashSeed(h1, 0);
            RNG rng(h2);

            CameraSampleInfo camSample;
            camSample.filmSample = Vector2 { x + rng.NextFloat(), y + rng.NextFloat() };

            auto ray = GenerateCameraRay(0, camSample);
            auto hit = TraceSingle(ray);

            ColorRGB value { 0, 0, 0};

            if (hit.point.meshId < 0xFFFFFFFF) {
                EXPECT_TRUE(false); // TODO something is broken in this test, never finding any intersections.
                // Estimate DI via BSDF importance sampling
                auto bsdfSample = WrapPrimarySampleToBsdf(&hit.point,
                    -ray.direction, rng.NextFloat(), rng.NextFloat(), false);
                auto bsdfValue = EvaluateBsdf(&hit.point, -ray.direction,
                    bsdfSample.direction, false);
                float shadingCosine = ComputeShadingCosine(&hit.point, -ray.direction,
                    bsdfSample.direction, false);

                auto bsdfRay = SpawnRay(&hit, bsdfSample.direction);
                auto bsdfhit = TraceSingle(bsdfRay);

                if (bsdfhit.point.meshId == -1) {
                    auto emission = ColorRGB{ 1, 1, 1 };

                    value = value + emission * bsdfValue
                        * (shadingCosine / bsdfSample.jacobian);
                }
            }
            else {
                value = ColorRGB{ 1, 1, 1 };
            }

            AddSplatRGB(0, camSample.filmSample.x, camSample.filmSample.y, value);

            EXPECT_FLOAT_EQ(value.r, 1.0f);
            EXPECT_FLOAT_EQ(value.g, 1.0f);
            EXPECT_FLOAT_EQ(value.b, 1.0f);
        }
    }
}