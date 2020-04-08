#pragma once

#include <renderground/api/api.h>

extern "C" {

/// Creates a new HDR image buffer, initialized to black. Returns its ID.
/// \param  width   The width of the image (number of pixel columns).
/// \param  height  The height of the image (number of pixel rows).
GROUND_API int CreateImageRGB(int width, int height);

// Splats a value into the image buffer with the given ID.
// Thread-safe (uses atomic add).
GROUND_API void AddSplatRGB(int image, float x, float y, ColorRGB value);

GROUND_API void AddSplatRGBMulti(int image, const float* xs, const float* ys, const ColorRGB* values, int count);

// Writes an image to the filesystem.
GROUND_API void WriteImage(int image, const char* filename);

// Loads an image from the filesystem.
GROUND_API int LoadImage(const char* filename);

}