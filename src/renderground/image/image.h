#pragma once

#include <vector>
#include <atomic>
#include <string>

namespace ground {

inline float AtomicAddFloat(std::atomic<float> &f, float value) {
    float old = f.load(std::memory_order_consume);
    float desired = old + value;
    while (!f.compare_exchange_weak(old, desired,
        std::memory_order_release, std::memory_order_consume))
        desired = old + value;
    return desired;
}

class Image {
public:
    // TODO allow specifying border handling methods and interpolation.
    //      right now: clamping & bilinear always.

    // TODO the image will need to store some metadata about the meaning
    //      of the channels (corresponding wavelengths, tristimulus, ...)
    //      required to use them as textures, useful to convert to sRGB
    //      before saving to disk.

    Image(int w, int h, int channels);

    // Writes a value to the image. Writing is thread-safe (atomic)
    // but should not be used at the same time as "GetValue"
    void AddValue(float x, float y, const float* value);

    // Retrieves the pixel value, only thread-safe if no concurrent writes
    // (via "AddValue") can happen.
    void GetValue(float x, float y, float* out) const;

    const int width;
    const int height;
    const int numChannels;

private:
    std::vector<std::atomic<float>> data;

    int PixelToIndex(int row, int col) const;
};

void WriteImageToFile(const Image& img, const std::string& filename);

} // namespace ground
