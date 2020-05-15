#pragma once

#include "api/api.h"

extern "C" {

SEE_CORE_API void WriteImageToExr(float* data, int width, int height, int numChannels,
                                const char* filename);

SEE_CORE_API int CacheExrImage(int* width, int* height, const char* filename);
SEE_CORE_API void CopyCachedImage(int id, float* out);

} // extern "C"