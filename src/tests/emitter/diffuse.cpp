#include <gtest/gtest.h>

#include <renderground/renderground.h>

#include "../testutil.h"

class EmitterDiffuseTests : public testing::Test {
protected:
    EmitterDiffuseTests() {}

    virtual ~EmitterDiffuseTests() {}

    virtual void SetUp() {
    }

    virtual void TearDown() {
        if (sceneIsActive) DeleteScene();
    }

    void MakeTestScene(float distance, float scaleLight, float lightNormalScale, float scaleSurface);
    void VerifyIntersections(bool shouldHitSurface);

    void VerifyPoint(SurfaceSample);
    void VerifyRay(EmitterSample);

    int emitterMesh = -1;
    int emitter = -1;
    int surfaceMesh = -1;

    bool sceneIsActive = false;

    bool lightIsFlipped = false;
};

void EmitterDiffuseTests::MakeTestScene(float distance, float scaleLight, 
    float lightNormalScale, float scaleSurface) 
{
    if (sceneIsActive)
        DeleteScene();

    InitScene();

    {
        // Create the light source
        float lo = -0.5f * scaleLight;
        float hi =  0.5f * scaleLight;
        float vertices[] = {
            lo, lo, 0.0f,
            hi, lo, 0.0f,
            hi, hi, 0.0f,
            lo, hi, 0.0f,
        };

        int indices[] = {
            0, 1, 2,
            0, 2, 3,
        };

        float normals[] = {
            0, 0, 1 * lightNormalScale,
            0, 0, 1 * lightNormalScale,
            0, 0, 1 * lightNormalScale,
            0, 0, 1 * lightNormalScale,
        };

        emitterMesh = AddTriangleMesh(vertices, 4, indices, 6, nullptr, normals);
        emitter = AttachDiffuseEmitter(emitterMesh, ColorRGB{ 10, 10, 10 });
    }

    {
        // Create the diffuse surface
        float lo = -0.5f * scaleSurface;
        float hi =  0.5f * scaleSurface;
        float vertices[] = {
            lo, lo, distance,
            hi, lo, distance,
            hi, hi, distance,
            lo, hi, distance,
        };

        int indices[] = {
            0, 1, 2,
            0, 2, 3
        };

        surfaceMesh = AddTriangleMesh(vertices, 4, indices, 6, nullptr, nullptr);
    }

    FinalizeScene();

    sceneIsActive = true;
    lightIsFlipped = lightNormalScale < 0;
}

void EmitterDiffuseTests::VerifyIntersections(bool shouldHitSurface) {
    EXPECT_TRUE(sceneIsActive);

    Vector2 primaryPos { 0.5f, 0.5f };
    Vector2 primaryDir { 0.5f, 0.5f };
    EmitterSample emitterSample =
        WrapPrimarySampleToEmitterRay(emitterMesh, primaryPos, primaryDir);

    Ray ray = SpawnRay(emitterSample.surface.point, emitterSample.direction);
    Hit hit = TraceSingle(ray);

    if (shouldHitSurface) // TODO can happen at grazing angles
        EXPECT_EQ(hit.point.meshId, surfaceMesh);
    else
        EXPECT_EQ(hit.point.meshId, INVALID_MESH_ID);
}

void EmitterDiffuseTests::VerifyPoint(SurfaceSample surfSample) {
    EXPECT_EQ(surfSample.point.meshId, emitterMesh);
    EXPECT_GT(surfSample.point.errorOffset, 0.0f);
    EXPECT_GT(surfSample.jacobian, 0.0f);

    EXPECT_GE(surfSample.point.barycentricCoords.x, 0.0f);
    EXPECT_LE(surfSample.point.barycentricCoords.x, 1.0f);
    EXPECT_GE(surfSample.point.barycentricCoords.y, 0.0f);
    EXPECT_LE(surfSample.point.barycentricCoords.y, 1.0f);
}

void EmitterDiffuseTests::VerifyRay(EmitterSample emitterSample) {
    VerifyPoint(emitterSample.surface);

    EXPECT_NEAR(Length(emitterSample.direction), 1.0f, 1e-3f);

    EXPECT_GT(emitterSample.jacobian, 0.0f);

    Vector3 normal{ 0, 0, 1 };
    float cos = Dot(normal, emitterSample.direction);
    if (lightIsFlipped) cos = -cos;

    EXPECT_GE(cos, 0.0f);
    EXPECT_LE(cos, 1.0f);
    EXPECT_NEAR(emitterSample.jacobian, cos / PI, 1e-3f);

    float jacobian = ComputePrimaryToEmitterRayJacobian(emitterSample.surface.point, emitterSample.direction);
    EXPECT_EQ(emitterSample.jacobian, jacobian);
}

TEST_F(EmitterDiffuseTests, SampledPoints) {
    // Samples a point on the surface of the emitter and asserts that the correct 
    // mesh was sampled. Also does some basic sanity checks on the returned values.

    MakeTestScene(0.001f, 1.0f, 1.0f, 1000.0f);
    
    auto surfSample = WrapPrimarySampleToEmitterSurface(emitter, 0.5f, 0.5f);

    VerifyPoint(surfSample);
}

TEST_F(EmitterDiffuseTests, SampledRay) {
    // Samples an emitted ray from the surface of the emitter and asserts that the correct 
    // mesh was sampled. Also does some basic sanity checks on the returned values.

    {
        MakeTestScene(0.001f, 1.0f, 1.0f, 1000.0f);

        Vector2 primaryPos{ 0.5, 0.5f };
        Vector2 primaryDir{ 0.5, 0.5f };
        auto emitterSample = WrapPrimarySampleToEmitterRay(emitter, primaryPos, primaryDir);

        VerifyRay(emitterSample);
    }

    {
        MakeTestScene(0.001f, 1.0f, -1.0f, 1000.0f);

        Vector2 primaryPos{ 0.5, 0.5f };
        Vector2 primaryDir{ 0.5, 0.5f };
        auto emitterSample = WrapPrimarySampleToEmitterRay(emitter, primaryPos, primaryDir);

        VerifyRay(emitterSample);
    }
}

TEST_F(EmitterDiffuseTests, SelfIntersection) {
    // Emits rays from a quad emitter at various scales and positions,
    // asserts that no self-intersections occur.
    // Also expects correct intersections to be reported on a surface
    // very close to the light source (i.e. rays are not offsetted too much).

    MakeTestScene(0.001f, 1.0f, 1.0f, 1000.0f);
    VerifyIntersections(true); 

    MakeTestScene(0.001f, 1.0f, -1.0f, 1000.0f);
    VerifyIntersections(false);
}

TEST_F(EmitterDiffuseTests, Sidedness) {
    // Tests that emission from the emitter, next event connections,
    // and hitting the emitter correctly reports non-zero radiance only
    // on the side the shading normals (and not the face normals) are facing.

    // TODO implement this
}

TEST_F(EmitterDiffuseTests, TotalPower) {
    // Illuminates a plane (very large quad) with a diffuse quad emitter.
    // Estimates the total power arriving on the plane and verifies with an
    // analytic ground truth solution.

    // TODO implement this
}
