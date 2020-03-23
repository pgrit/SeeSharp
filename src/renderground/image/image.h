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
    Image(int w, int h, int channels);

    void AddValue(float x, float y, const float* value);

    // Retrieves the pixel value, atomically on a per-channel level.
    // In a concurrent setting, values might be inconsistent.
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
