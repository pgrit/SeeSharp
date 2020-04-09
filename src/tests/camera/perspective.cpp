#include <gtest/gtest.h>

#include <renderground/renderground.h>

#include "../testutil.h"

class PerspectiveCameraTests : public testing::Test {
protected:
    PerspectiveCameraTests() {}

    virtual ~PerspectiveCameraTests() {}

    virtual void SetUp() {
    }

    virtual void TearDown() {
        DeleteScene();
    }
};

TEST_F(PerspectiveCameraTests, RayDirections) {
    // Asserts that rays emitted from the camera are correct.

    int frameBufferId = CreateImageRGB(3, 3);
    
    // Expected camera orientation:
    // z is forward
    // x is to the right
    // y is up
    int camTransform = CreateTransform(
        Vector3{ 0, 0, 0 },  // pos
        Vector3{ 0, 0, 0 },  // rot
        Vector3{ 1, 1, 1 }); // scale

    float fov = 90.0f;

    int camId = CreatePerspectiveCamera(camTransform, fov, frameBufferId);

    //////////////////////////////////////////////////
    // Check the bottom left corner
    {
        CameraSampleInfo sampleTopLeft{
            Vector2{0.0f, 0.0f}, // film sample (only used value for the perspective camera)
            Vector2{},
            0.0f
        };
        Ray ray = GenerateCameraRay(camId, sampleTopLeft);

        EXPECT_FLOAT_EQ(ray.origin.x, 0.0f);
        EXPECT_FLOAT_EQ(ray.origin.y, 0.0f);
        EXPECT_FLOAT_EQ(ray.origin.z, 0.0f);

        const float c = std::cos(PI / 4.0f);
        const float len = std::sqrtf(c * c * 3);
        const float expectedXYZ = c / len;

        EXPECT_NEAR(ray.direction.x, -expectedXYZ, 1e-3f);
        EXPECT_NEAR(ray.direction.y, -expectedXYZ, 1e-3f);
        EXPECT_NEAR(ray.direction.z,  expectedXYZ, 1e-3f);
    }

    //////////////////////////////////////////////////
    // Check the left center
    {
        CameraSampleInfo sampleTopLeft{
            Vector2{0.0f, 1.5f}, // film sample (only used value for the perspective camera)
            Vector2{},
            0.0f
        };
        Ray ray = GenerateCameraRay(camId, sampleTopLeft);

        EXPECT_FLOAT_EQ(ray.origin.x, 0.0f);
        EXPECT_FLOAT_EQ(ray.origin.y, 0.0f);
        EXPECT_FLOAT_EQ(ray.origin.z, 0.0f);

        const float c = std::cos(PI / 4.0f);
        const float len = std::sqrtf(c * c * 2);
        const float expectedXZ = c / len;

        EXPECT_NEAR(ray.direction.x, -expectedXZ, 1e-3f);
        EXPECT_NEAR(ray.direction.y,        0.0f, 1e-3f);
        EXPECT_NEAR(ray.direction.z,  expectedXZ, 1e-3f);
    }

    //////////////////////////////////////////////////
    // Check the top right corner
    {
        CameraSampleInfo sampleTopLeft{
            Vector2{3.0f, 3.0f}, // film sample (only used value for the perspective camera)
            Vector2{},
            0.0f
        };
        Ray ray = GenerateCameraRay(camId, sampleTopLeft);

        EXPECT_FLOAT_EQ(ray.origin.x, 0.0f);
        EXPECT_FLOAT_EQ(ray.origin.y, 0.0f);
        EXPECT_FLOAT_EQ(ray.origin.z, 0.0f);

        const float c = std::cos(PI / 4.0f);
        const float len = std::sqrtf(c * c * 3);
        const float expectedXYZ = c / len;

        EXPECT_NEAR(ray.direction.x, expectedXYZ, 1e-3f);
        EXPECT_NEAR(ray.direction.y, expectedXYZ, 1e-3f);
        EXPECT_NEAR(ray.direction.z, expectedXYZ, 1e-3f);
    }
}

TEST_F(PerspectiveCameraTests, Rotations) {
    // Asserts the directions are rotated correctly based on the given transform

    int frameBufferId = CreateImageRGB(3, 3);

    int camTransform = CreateTransform(
        Vector3{ 0, 0, 0 },  // pos
        Vector3{ 0, 0, 0 },  // rot
        Vector3{ 1, 1, 1 }); // scale

    int camId = CreatePerspectiveCamera(camTransform, 45.0f, frameBufferId);

    // TODO implement this
}

TEST_F(PerspectiveCameraTests, Scale) {
    // Asserts applying a scaling transform has no effect other than flipping axes

    int frameBufferId = CreateImageRGB(3, 3);

    int camTransform = CreateTransform(
        Vector3{ 0, 0, 0 },  // pos
        Vector3{ 0, 0, 0 },  // rot
        Vector3{ 1, 1, 1 }); // scale

    int camId = CreatePerspectiveCamera(camTransform, 45.0f, frameBufferId);

    // TODO implement this
}

TEST_F(PerspectiveCameraTests, Location) {
    // Asserts that the position is correctly modified by the transform

    int frameBufferId = CreateImageRGB(3, 3);

    int camTransform = CreateTransform(
        Vector3{ 0, 0, 0 },  // pos
        Vector3{ 0, 0, 0 },  // rot
        Vector3{ 1, 1, 1 }); // scale

    int camId = CreatePerspectiveCamera(camTransform, 45.0f, frameBufferId);

    // TODO implement this
}

TEST_F(PerspectiveCameraTests, WorldToRaster) {
    // Asserts that points in world space are mapped to the correct pixels.

    int frameBufferId = CreateImageRGB(3, 3);

    int camTransform = CreateTransform(
        Vector3{ 0, 0, 0 },  // pos
        Vector3{ 0, 0, 0 },  // rot
        Vector3{ 1, 1, 1 }); // scale

    const float fov = 90.0f;

    int camId = CreatePerspectiveCamera(camTransform, fov, frameBufferId);

    // Test a point in the very center
    Vector2 rasterPos = MapWorldSpaceToCameraFilm(camId, Vector3{ 0, 0, 10 });
    EXPECT_NEAR(rasterPos.x, 1.5f, 1e-4f);
    EXPECT_NEAR(rasterPos.y, 1.5f, 1e-4f);

    // Test a point in the bottom left corner
    rasterPos = MapWorldSpaceToCameraFilm(camId, Vector3{ -10, -10, 10 });
    EXPECT_NEAR(rasterPos.x, 0.0f, 1e-4f);
    EXPECT_NEAR(rasterPos.y, 0.0f, 1e-4f);

    // Test a point in the top right corner
    rasterPos = MapWorldSpaceToCameraFilm(camId, Vector3{ 10, 10, 10 });
    EXPECT_NEAR(rasterPos.x, 3.0f, 1e-4f);
    EXPECT_NEAR(rasterPos.y, 3.0f, 1e-4f);

    // Test a point in the top left corner
    rasterPos = MapWorldSpaceToCameraFilm(camId, Vector3{ -10, 10, 10 });
    EXPECT_NEAR(rasterPos.x, 0.0f, 1e-4f);
    EXPECT_NEAR(rasterPos.y, 3.0f, 1e-4f);

    // Test a point in the bottom right corner
    rasterPos = MapWorldSpaceToCameraFilm(camId, Vector3{ 10, -10, 10 });
    EXPECT_NEAR(rasterPos.x, 3.0f, 1e-4f);
    EXPECT_NEAR(rasterPos.y, 0.0f, 1e-4f);
}

TEST_F(PerspectiveCameraTests, ClippingPlanes) {
    // Test behaviour for points before the near plane or after the far plane

    int frameBufferId = CreateImageRGB(3, 3);

    int camTransform = CreateTransform(
        Vector3{ 0, 0, 0 },  // pos
        Vector3{ 0, 0, 0 },  // rot
        Vector3{ 1, 1, 1 }); // scale

    const float fov = 90.0f;

    int camId = CreatePerspectiveCamera(camTransform, fov, frameBufferId);

    // Test a point in the very center
    // typical distance
    Vector2 rasterPos = MapWorldSpaceToCameraFilm(camId, Vector3{ 0, 0, 10 });
    EXPECT_NEAR(rasterPos.x, 1.5f, 1e-4f);
    EXPECT_NEAR(rasterPos.y, 1.5f, 1e-4f);

    // very far away
    rasterPos = MapWorldSpaceToCameraFilm(camId, Vector3{ 0, 0, 1e19f });
    EXPECT_NEAR(rasterPos.x, 1.5f, 1e-4f);
    EXPECT_NEAR(rasterPos.y, 1.5f, 1e-4f);

    // very close
    rasterPos = MapWorldSpaceToCameraFilm(camId, Vector3{ 0, 0, 1e-19f });
    EXPECT_NEAR(rasterPos.x, 1.5f, 1e-4f);
    EXPECT_NEAR(rasterPos.y, 1.5f, 1e-4f);
}