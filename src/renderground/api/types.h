#pragma once

extern "C" {

struct Vector3 {
    float x, y, z;
};

struct Vector2 {
    float x, y;
};

struct Ray {
    Vector3 origin;
    Vector3 direction;
    float minDistance;
};

struct SurfacePoint {
    Vector3 position;
    Vector3 normal;
    Vector2 barycentricCoords;
    unsigned int meshId;
    unsigned int primId;
};

struct Hit {
    SurfacePoint point;
    float distance;
    float errorOffset;
};

struct SurfaceSample {
    SurfacePoint point;
    float jacobian;
};

struct BsdfSample {
    Vector3 direction;
    float jacobian;
    float reverseJacobian;
};

struct ColorRGB {
    float r, g, b;
};

struct UberShaderParams {
    int baseColorTexture;
    int emissionTexture;
};

struct PathVertex {
    SurfacePoint point; // TODO could be a "CompressedSurfacePoint"

    // Surface area pdf to sample this vertex from the previous one,
    // i.e., the actual density this vertex was sampled from
    float pdfFromAncestor;

    // Surface area pdf to sample the previous vertex from this one,
    // i.e., the reverse direction of the path.
    float pdfToAncestor;

    ColorRGB weight; // TODO support other spectral resolutions
};

// Stores primary space sample values for a camera sample query.
struct CameraSampleInfo {
    Vector2 filmSample;
    Vector2 lensSample;
    float time;
};

struct GeometryTerms {
    float cosineFrom;
    float cosineTo;
    float squaredDistance;
    float geomTerm;
};

}