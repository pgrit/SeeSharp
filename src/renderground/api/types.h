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
    float errorOffset;
};

#define INVALID_MESH_ID ((unsigned int) -1)

struct Hit {
    SurfacePoint point;
    float distance;
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

    int ancestorId;
};

/** Stores primary space sample values for a camera sample query.

The primary sample space of the film (\see filmSample) is the pixel raster of the rendered image.
The coordinate system spans the image plane as follows:
(0,0) is the bottom left corner of the bottom left pixel.
The x axis points to the right, hence (1,0) is the bottom right corner of the bottom right pixel.
The y axis points upwards, hence (0,1) is the top left corner of the top left pixel.

*/
struct CameraSampleInfo {
    Vector2 filmSample; // TODO should probably be renamed to "filmPOSITION"
    Vector2 lensSample; // TODO should probably be more verbose: lensPrimarySample
    float time; // TODO decide on conventions once this is actually used.
};

struct GeometryTerms {
    float cosineFrom;
    float cosineTo;
    float squaredDistance;
    float geomTerm;
};

struct EmitterSample {
    SurfaceSample surface;
    Vector3 direction;
    float jacobian;
    float shadingCosine;
};

}