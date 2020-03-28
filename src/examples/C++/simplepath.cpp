#include <vector>
#include <chrono>
#include <iostream>

#include <renderground/renderground.h>

#include <tbb/parallel_for.h>

int SetupSceneGeometry() {
    InitScene();

    // Illuminated diffuse quad
    float vertices[] = {
        -1.0f, -1.0f, 0.0f,
         1.0f, -1.0f, 0.0f,
         1.0f,  1.0f, 0.0f,
        -1.0f,  1.0f, 0.0f,
    };

    int indices[] = {
        0, 1, 2,
        0, 2, 3
    };

    const int quadId = AddTriangleMesh(vertices, 4, indices, 6);

    // Light source
    float vertLight[] = {
        -0.1f, -0.1f, -1.0f,
         0.1f, -0.1f, -1.0f,
         0.1f,  0.1f, -1.0f,
        -0.1f,  0.1f, -1.0f,
    };

    int idxLight[] = {
        0, 1, 2,
        0, 2, 3
    };

    const int lightId = AddTriangleMesh(vertLight, 4, idxLight, 6);

    FinalizeScene();

    return lightId;
}

int SetupCamera(int frameBuffer) {
    Vector3 pos { 0, 0, -5 };
    Vector3 rot { 0, 0, 0};
    Vector3 scale { 1, 1, 1};
    auto camTransform = CreateTransform(pos, rot, scale);

    return CreatePerspectiveCamera(camTransform, 45.0f, frameBuffer);
}

int main() {
    auto startTime = std::chrono::high_resolution_clock::now();

    const int lightMesh = SetupSceneGeometry();

    const int imageWidth = 800;
    const int imageHeight = 600;
    const int frameBuffer = CreateImage(imageWidth, imageHeight, 1);

    const int camId = SetupCamera(frameBuffer);

    // tbb::parallel_for(tbb::blocked_range<int>(0, imageHeight),
        // [&](tbb::blocked_range<int> r) {
        for(int y = 0; y < imageHeight; ++y) {
        // for (int y = r.begin(); y < r.end(); ++y) {
            for (int x = 0; x < imageWidth; ++x) {
                CameraSampleInfo camSample;
                camSample.filmSample = Vector2 { x + 0.5f, y + 0.5f };

                auto ray = GenerateCameraRay(camId, camSample);
                auto hit = TraceSingle(ray);

                float value = 0.0f;
                if (hit.point.meshId < 0) continue;

                // Estimate DI via next event shadow ray
                auto lightSample = WrapPrimarySampleToSurface(lightMesh, 0.5f, 0.5f);
                if (!IsOccluded(&hit, lightSample.point.position)) {
                    float emission = 0.0f;
                    ComputeEmission(&lightSample.point, &emission);

                    auto geometryTerms = ComputeGeometryTerms(&hit.point, &lightSample.point);

                    // value = emission * geometryTerms.geomTerm / lightSample.jacobian;
                }

                // Estimate DI via BSDF importance sampling
                float bsdfValue = 0.0f;
                auto bsdfSample = WrapPrimarySampleToBsdf(&hit.point, 0.5f, 0.5f, &bsdfValue);
                auto bsdfRay = SpawnRay(&hit, bsdfSample.direction);
                auto bsdfhit = TraceSingle(bsdfRay);
                if (bsdfhit.point.meshId == lightMesh) {
                    // The light source was hit.
                    float emission = 0.0f;
                    ComputeEmission(&bsdfhit.point, &emission);

                    auto geometryTerms = ComputeGeometryTerms(&hit.point, &bsdfhit.point);

                    value = emission * bsdfValue * geometryTerms.cosineFrom / bsdfSample.jacobian;
                }

                // Combine with balance heuristic MIS

                AddSplat(frameBuffer,
                    camSample.filmSample.x, camSample.filmSample.y, &value);
            }
        }
    // });

    WriteImage(frameBuffer, "render.exr");

    auto endTime = std::chrono::high_resolution_clock::now();
    auto deltaTime = std::chrono::duration_cast<std::chrono::milliseconds>(endTime - startTime).count();
    std::cout << deltaTime << "ms" << std::endl;

    return 0;
}