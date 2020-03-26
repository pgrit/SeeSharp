#pragma once

#include <renderground/api/api.h>

extern "C" {

// Creates a new HDR image buffer, initialized to black. Returns its ID.
GROUND_API int CreateImage(int width, int height, int numChannels);

// Splats a value into the image buffer with the given ID.
// Thread-safe (uses atomic add).
GROUND_API void AddSplat(int image, float x, float y, const float* value);

GROUND_API void AddSplatMulti(int image, const float* xs, const float* ys, const float* values, int count);

// Writes an image to the filesystem.
GROUND_API void WriteImage(int image, const char* filename);

// Loads an image from the filesystem.
GROUND_API int LoadImage(const char* filename);

}