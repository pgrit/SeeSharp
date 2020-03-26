#pragma once

#include <renderground/api/api.h>

extern "C" {

// Creates a transformation and returns its id for use with other functions
GROUND_API int CreateTransform(Vector3 translation, Vector3 eulerAngles, Vector3 scale);

}
