#include <gtest/gtest.h>

#include <renderground/renderground.h>

class VertexAttributes : public testing::Test {
protected:
    VertexAttributes() {}

    virtual ~VertexAttributes() {}

    virtual void SetUp() {
        int frameBufferId = CreateImageRGB(imageWidth, imageHeight);
    }

    virtual void TearDown() {
        DeleteScene();
    }

    bool loaded = false;
    int imageHeight = 512;
    int imageWidth = 512;
};

TEST_F(VertexAttributes, DefaultNormals) {
    InitScene();

    float vertices[] = {
        0.0f, 0.0f, 0.0f,
        1.0f, 0.0f, 0.0f,
        1.0f, 1.0f, 0.0f,
        0.0f, 1.0f, 0.0f,
    };

    int indices[] = {
        0, 1, 2,
        0, 2, 3
    };

    int mesh = AddTriangleMesh(vertices, 4, indices, 6, nullptr, nullptr);

    FinalizeScene();

    Ray r {
        Vector3 { 0.5, 0.75, -1.0f },
        Vector3 { 0, 0, 1 },
        0.0f
    };

    Hit h = TraceSingle(r);

    EXPECT_EQ(h.point.meshId, mesh);
    EXPECT_EQ(h.point.primId, 1);

    EXPECT_FLOAT_EQ(h.point.normal.x, 0.0f);
    EXPECT_FLOAT_EQ(h.point.normal.y, 0.0f);
    EXPECT_FLOAT_EQ(h.point.normal.z, 1.0f);

    auto n = ComputeShadingNormal(h.point);

    EXPECT_FLOAT_EQ(n.x, 0.0f);
    EXPECT_FLOAT_EQ(n.y, 0.0f);
    EXPECT_FLOAT_EQ(n.z, 1.0f);
}

TEST_F(VertexAttributes, ShadingNormals) {
    InitScene();

    float vertices[] = {
        0.0f, 0.0f, 0.0f,
        1.0f, 0.0f, 0.0f,
        1.0f, 1.0f, 0.0f,

        0.0f, 0.0f, 0.0f,
        1.0f, 1.0f, 0.0f,
        0.0f, 1.0f, 0.0f,
    };

    int indices[] = {
        0, 1, 2,
        3, 4, 5
    };

    float normals[] = {
        0.0f, 0.0f, 1.0f,
        0.0f, 0.0f, 1.0f,
        0.0f, 0.0f, 1.0f,

        1.0f, 1.0f, 0.0f,
        1.0f, 1.0f, 0.0f,
        1.0f, 1.0f, 0.0f,
    };

    int mesh = AddTriangleMesh(vertices, 6, indices, 6, nullptr, normals);

    FinalizeScene();

    Ray r {
        Vector3 { 0, 1, -1.0f },
        Vector3 { 0, 0, 1 },
        0.0f
    };

    Hit h = TraceSingle(r);

    EXPECT_FLOAT_EQ(h.point.position.x, 0.0f);
    EXPECT_FLOAT_EQ(h.point.position.y, 1.0f);

    EXPECT_FLOAT_EQ(h.point.barycentricCoords.x, 0.0f);
    EXPECT_FLOAT_EQ(h.point.barycentricCoords.y, 1.0f);

    EXPECT_EQ(h.point.meshId, mesh);
    EXPECT_EQ(h.point.primId, 1);

    EXPECT_FLOAT_EQ(h.point.normal.x, 0.0f);
    EXPECT_FLOAT_EQ(h.point.normal.y, 0.0f);
    EXPECT_FLOAT_EQ(h.point.normal.z, 1.0f);

    auto n = ComputeShadingNormal(h.point);

    EXPECT_FLOAT_EQ(Length(n), 1.0f);

    Vector3 v { 1, 1, 0 };
    v = Normalize(v);

    EXPECT_FLOAT_EQ(n.x, v.x);
    EXPECT_FLOAT_EQ(n.y, v.y);
    EXPECT_FLOAT_EQ(n.z, v.z);

    /////////////////////////////////////////////////////
    {
        Ray r {
            Vector3 { 0.5, 0.75, -1.0f },
            Vector3 { 0, 0, 1 },
            0.0f
        };

        Hit h = TraceSingle(r);

        EXPECT_FLOAT_EQ(h.point.position.x, 0.5f);
        EXPECT_FLOAT_EQ(h.point.position.y, 0.75f);

        EXPECT_EQ(h.point.meshId, mesh);
        EXPECT_EQ(h.point.primId, 1);

        EXPECT_FLOAT_EQ(h.point.normal.x, 0.0f);
        EXPECT_FLOAT_EQ(h.point.normal.y, 0.0f);
        EXPECT_FLOAT_EQ(h.point.normal.z, 1.0f);

        auto n = ComputeShadingNormal(h.point);

        EXPECT_FLOAT_EQ(Length(n), 1.0f);

        Vector3 v { 1, 1, 0 };
        v = Normalize(v);

        EXPECT_FLOAT_EQ(n.x, v.x);
        EXPECT_FLOAT_EQ(n.y, v.y);
        EXPECT_FLOAT_EQ(n.z, v.z);
    }
}

TEST_F(VertexAttributes, UVCoordinates) {
    InitScene();

    float vertices[] = {
        0.0f, 0.0f, 0.0f,
        1.0f, 0.0f, 0.0f,
        1.0f, 1.0f, 0.0f
    };

    int indices[] = {
        0, 1, 2
    };

    // TODO check that default uv values are equal to barycentric coordinates

    AddTriangleMesh(vertices, 4, indices, 6, nullptr, nullptr);

    FinalizeScene();
}

TEST_F(VertexAttributes, DefaultUVs) {
    InitScene();

    float vertices[] = {
        0.0f, 0.0f, 0.0f,
        1.0f, 0.0f, 0.0f,
        1.0f, 1.0f, 0.0f
    };

    int indices[] = {
        0, 1, 2
    };

    // TODO set some uvs and test if they are correctly interpolated
    //      1) in the center
    //      2) in each corner

    AddTriangleMesh(vertices, 4, indices, 6, nullptr, nullptr);

    FinalizeScene();
}