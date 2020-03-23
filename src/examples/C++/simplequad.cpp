#include <vector>
#include <chrono>
#include <iostream>

#include "renderground/renderground.h"

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

    for (int y = 0; y < imageHeight; ++y) {
        for (int x = 0; x < imageWidth; ++x) {
            float org[] = {
                topLeft[0] + float(x) / float(imageWidth) * diag[0],
                topLeft[1] + float(y) / float(imageHeight) * diag[1],
                5.0f
            };
            float dir[] = { 0.0f, 0.0f, -1.0f };

            Hit hit = TraceSingle(org, dir);

            float value = hit.meshId;
            AddSplat(image, x, y, &value);
        }
    }

    auto endTime = std::chrono::high_resolution_clock::now();
    auto deltaTime = std::chrono::duration_cast<std::chrono::milliseconds>(endTime - startTime).count();
    std::cout << deltaTime << "ms" << std::endl;

    WriteImage(image, "render.exr");

    return 0;
}