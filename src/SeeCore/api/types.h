#pragma once

extern "C" {

struct Vector3 {
    float x, y, z;
};

struct Ray {
    Vector3 origin;
    Vector3 direction;
    float minDistance;
};

#define INVALID_MESH_ID ((unsigned int) -1)

struct Hit {
    // SurfacePoint point;
    unsigned int meshId;
    unsigned int primId;
    float u, v;
    float distance;
};

}