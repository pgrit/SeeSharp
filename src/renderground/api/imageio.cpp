#include "api/imageio.h"
#include "image/image.h"

#include <tbb/parallel_for.h>

std::vector<std::unique_ptr<ground::Image>> globalImages;

extern "C" {

GROUND_API int CreateImage(int width, int height, int numChannels) {
    globalImages.emplace_back(new ground::Image(width, height, numChannels));
    return int(globalImages.size()) - 1;
}

GROUND_API void AddSplat(int image, float x, float y, const float* value) {
    // TODO check that the image id is correct (Debug mode?)
    globalImages[image]->AddValue(x, y, value);
}

GROUND_API void AddSplatMulti(int image, const float* xs, const float* ys, const float* values, int num) {
    auto& img = globalImages[image];
    tbb::parallel_for(tbb::blocked_range<int>(0, num),
        [&](tbb::blocked_range<int> r) {
        for (int i = r.begin(); i < r.end(); ++i) {
            img->AddValue(xs[i], ys[i], values + (i * img->numChannels));
        }
    });
}

GROUND_API void WriteImage(int image, const char* filename) {
    ground::WriteImageToFile(*(globalImages[image]), filename);
}

}