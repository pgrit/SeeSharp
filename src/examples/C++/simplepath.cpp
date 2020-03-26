#include <vector>
#include <chrono>
#include <iostream>

#include <renderground/renderground.h>

#include <tbb/parallel_for.h>

void SetupSceneGeometry() {
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
}

int SetupCamera(int frameBuffer) {
    Vector3 pos { 0, 0, 0 };
    Vector3 rot { 0, 0, 0};
    Vector3 scale { 1, 1, 1};
    auto camTransform = CreateTransform(pos, rot, scale);

    return CreatePerspectiveCamera(camTransform, 45.0f, frameBuffer);
}

int main() {
    auto startTime = std::chrono::high_resolution_clock::now();

    SetupSceneGeometry();

    const int imageWidth = 512;
    const int imageHeight = 512;
    const int frameBuffer = CreateImage(imageWidth, imageHeight, 1);

    const int camId = SetupCamera(frameBuffer);

    tbb::parallel_for(tbb::blocked_range<int>(0, imageHeight),
        [&](tbb::blocked_range<int> r) {
        for (int y = r.begin(); y < r.end(); ++y) {
            for (int x = 0; x < imageWidth; ++x) {
                CameraSampleInfo camSample;
                camSample.filmSample = Vector2 { x + 0.5f, y + 0.5f };

                Ray ray = GenerateCameraRay(camId, camSample);
                Hit hit = TraceSingle(ray);

                float value = float(hit.meshId);
                AddSplat(frameBuffer, camSample.filmSample.x, camSample.filmSample.y, &value);
            }
        }
    });

    WriteImage(frameBuffer, "render.exr");

    auto endTime = std::chrono::high_resolution_clock::now();
    auto deltaTime = std::chrono::duration_cast<std::chrono::milliseconds>(endTime - startTime).count();
    std::cout << deltaTime << "ms" << std::endl;

    return 0;
}