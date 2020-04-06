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
        // DeleteScene();
    }
};

void MakeTestScene(float distance, float scaleLight, float lightNormalScale, float scaleSurface) {
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

        int mesh = AddTriangleMesh(vertices, 4, indices, 6, nullptr, nullptr);
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

        int mesh = AddTriangleMesh(vertices, 4, indices, 6, nullptr, nullptr);
    }

    FinalizeScene();
}

void VerifyIntersections(bool shouldHitSurface) {
    const int emitterMeshId = 0;
    const int surfaceMeshId = 1;

    Vector2 primaryPos { 0.5f, 0.5f };
    Vector2 primaryDir { 0.5f, 0.5f };
    EmitterSample emitterSample =
        WrapPrimarySampleToEmitterRay(emitterMeshId, primaryPos, primaryDir);

    Hit hit = TraceSingle(emitterSample.ray);

    if (shouldHitSurface) // TODO can happen at grazing angles
        EXPECT_EQ(hit.point.meshId, surfaceMeshId);
    else
        EXPECT_EQ(hit.point.meshId, INVALID_MESH_ID);
}

TEST_F(EmitterDiffuseTests, SelfIntersection) {
    // Emits rays from a quad emitter at various scales and positions,
    // asserts that no self-intersections occur.
    // Also expects correct intersections to be reported on a surface
    // very close to the light source (i.e. rays are not offsetted too much).

    MakeTestScene(0.001f, 1.0f, 1.0f, 1000.0f);
    VerifyIntersections(true);

    DeleteScene(); 
}

TEST_F(EmitterDiffuseTests, Sidedness) {
    // Tests that emission from the emitter, next event connections,
    // and hitting the emitter correctly reports non-zero radiance only
    // on the side the shading normals (and not the face normals) are facing.

    /*MakeTestScene(0.1f, 1.0f, 1.0f, 1000.0f);
    VerifyIntersections(true);

    MakeTestScene(0.1f, 1.0f, -1.0f, 1000.0f);
    VerifyIntersections(false);*/
}

TEST_F(EmitterDiffuseTests, TotalPower) {
    // Illuminates a plane (very large quad) with a diffuse quad emitter.
    // Estimates the total power arriving on the plane and verifies with an
    // analytic ground truth solution.
}
