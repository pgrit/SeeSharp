#include <vector>
#include <chrono>
#include <iostream>

#include "renderground/renderground.h"

#include <tbb/parallel_for.h>

int main() {
    InitScene();

    float vertices[] = {
        0.0f, 0.0f, 0.0f,
        1.0f, 0.0f, 0.0f,
        1.0f, 1.0f, 0.0f,
        0.0f, 1.0f, 0.0f,
    };

    int indices[] = {
        0, 1, 2,
        0, 2, 3
    };

    AddTriangleMesh(vertices, 4, indices, 6);

    FinalizeScene();

    const int imageWidth = 512;
    const int imageHeight = 512;
    const int image = CreateImage(imageWidth, imageHeight, 1);

    float topLeft[] = { -1.0f, -1.0f, 5.0f };
    float diag[] = { 3.0f, 3.0f, 0.0f };

    auto startTime = std::chrono::high_resolution_clock::now();

#define SINGLE_RAY

#ifdef SINGLE_RAY
    tbb::parallel_for(tbb::blocked_range<int>(0, imageHeight), [&](tbb::blocked_range<int> r) {
        // for (int y = 0; y < imageHeight; ++y) {
        for (int y = r.begin(); y < r.end(); ++y) {
            for (int x = 0; x < imageWidth; ++x) {
                Vector3 org {
                    topLeft[0] + float(x) / float(imageWidth) * diag[0],
                    topLeft[1] + float(y) / float(imageHeight) * diag[1],
                    5.0f
                };
                Vector3 dir { 0.0f, 0.0f, -1.0f };

                Ray ray { org, dir };

                Hit hit = TraceSingle(ray);

                float value = hit.meshId;
                AddSplat(image, x, y, &value);
            }
        }
    });
#else
    std::vector<float> origins(imageWidth * imageHeight * 3);
    std::vector<float> dirs(imageWidth * imageHeight * 3);
    std::vector<Hit> hits(imageWidth * imageHeight);

    for (int y = 0; y < imageHeight; ++y) {
        for (int x = 0; x < imageWidth; ++x) {
            const int idx = (y * imageWidth + x) * 3;
            origins[idx + 0] = topLeft[0] + float(x) / float(imageWidth) * diag[0];
            origins[idx + 1] = topLeft[1] + float(y) / float(imageHeight) * diag[1];
            origins[idx + 2] = 5.0f;

            dirs[idx + 0] = 0.0f;
            dirs[idx + 1] = 0.0f;
            dirs[idx + 2] = -1.0f;
        }
    }

    TraceMulti(origins.data(), dirs.data(), hits.size(), hits.data());

    std::vector<float> xs(imageWidth * imageHeight);
    std::vector<float> ys(imageWidth * imageHeight);
    std::vector<float> values(imageWidth * imageHeight);
    for (int y = 0; y < imageHeight; ++y) {
        for (int x = 0; x < imageWidth; ++x) {
            const int idx = (y * imageWidth + x);
            xs[idx] = x;
            ys[idx] = y;
            values[idx] = hits[idx].meshId;
        }
    }

    AddSplatMulti(image, xs.data(), ys.data(), values.data(), hits.size());

#endif

    auto endTime = std::chrono::high_resolution_clock::now();
    auto deltaTime = std::chrono::duration_cast<std::chrono::milliseconds>(endTime - startTime).count();
    std::cout << deltaTime << "ms" << std::endl;

    WriteImage(image, "render.exr");

    return 0;
}