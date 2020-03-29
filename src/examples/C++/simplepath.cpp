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

    const int quadId = AddTriangleMesh(vertices, 4, indices, 6, nullptr, nullptr);

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

    const int lightId = AddTriangleMesh(vertLight, 4, idxLight, 6, nullptr, nullptr);

    FinalizeScene();

    // Setup the materials
    const auto blackTexture = CreateImage(1, 1, 1);
    float black = 0.0f;
    AddSplat(blackTexture, 0, 0, &black);

    const auto emitTexture = CreateImage(1, 1, 1);
    float emission = 10.0f;
    AddSplat(emitTexture, 0, 0, &emission);

    const auto reflectTexture = CreateImage(1, 1, 1);
    float reflectance = 0.85f;
    AddSplat(reflectTexture, 0, 0, &reflectance);

    UberShaderParams lightMaterialParams {
        blackTexture,
        emitTexture
    };
    const auto lightMaterial = AddUberMaterial(&lightMaterialParams);
    AssignMaterial(lightId, lightMaterial);

    UberShaderParams diffuseMaterialParams {
        reflectTexture,
        -1
    };
    const auto diffuseMaterial = AddUberMaterial(&diffuseMaterialParams);
    AssignMaterial(quadId, diffuseMaterial);

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

    // For now: monochrome rendering at 500nm
    const float wavelength = 500.0f;

    const uint64_t BaseSeed = 0xC030114Ui64;

    // tbb::parallel_for(tbb::blocked_range<int>(0, imageHeight),
        // [&](tbb::blocked_range<int> r) {
    const int totalSpp = 8;
    for (int sampleIdx = 0; sampleIdx < totalSpp; ++sampleIdx) {
        for(int y = 0; y < imageHeight; ++y) {
        // for (int y = r.begin(); y < r.end(); ++y) {
            for (int x = 0; x < imageWidth; ++x) {
                RNG rng(HashSeed(BaseSeed, (y * imageWidth + x) * totalSpp + sampleIdx));

                CameraSampleInfo camSample;
                camSample.filmSample = Vector2 { x + rng.NextFloat(), y + rng.NextFloat() };

                auto ray = GenerateCameraRay(camId, camSample);
                auto hit = TraceSingle(ray);

                float value = 0.0f;
                if (hit.point.meshId < 0) continue;

                // Estimate DI via next event shadow ray
                auto lightSample = WrapPrimarySampleToSurface(lightMesh, rng.NextFloat(), rng.NextFloat());
                if (!IsOccluded(&hit, lightSample.point.position)) {
                    Vector3 lightDir = hit.point.position - lightSample.point.position;
                    float emission = ComputeEmission(&lightSample.point,
                        lightDir, wavelength);

                    float bsdfValue = EvaluateBsdf(&hit.point, -ray.direction,
                        lightDir, wavelength, false);
                    auto geometryTerms = ComputeGeometryTerms(&hit.point, &lightSample.point);

                    // Compute MIS weights
                    float pdfNextEvt = lightSample.jacobian;
                    float pdfBsdf = ComputePrimaryToBsdfJacobian(&hit.point, -ray.direction,
                        lightDir, wavelength, false).jacobian * geometryTerms.cosineTo / geometryTerms.squaredDistance;
                    float pdfRatio = pdfBsdf / pdfNextEvt;
                    float misWeight = 1 / (pdfRatio * pdfRatio + 1);

                    value += misWeight * emission * bsdfValue
                        * geometryTerms.geomTerm / lightSample.jacobian;
                }

                // Estimate DI via BSDF importance sampling
                auto bsdfSample = WrapPrimarySampleToBsdf(&hit.point,
                    -ray.direction, rng.NextFloat(), rng.NextFloat(), wavelength, false);
                float bsdfValue = EvaluateBsdf(&hit.point, -ray.direction,
                    bsdfSample.direction, wavelength, false);

                auto bsdfRay = SpawnRay(&hit, bsdfSample.direction);
                auto bsdfhit = TraceSingle(bsdfRay);

                if (bsdfhit.point.meshId == lightMesh) { // The light source was hit.
                    float emission = ComputeEmission(&bsdfhit.point,
                        -bsdfRay.direction, wavelength);

                    auto geometryTerms = ComputeGeometryTerms(&hit.point, &bsdfhit.point);

                    // Compute MIS weights
                    float pdfNextEvt = ComputePrimaryToSurfaceJacobian(&bsdfhit.point);
                    float pdfBsdf = bsdfSample.jacobian * geometryTerms.cosineTo/ geometryTerms.squaredDistance;
                    float pdfRatio = pdfNextEvt / pdfBsdf;
                    float misWeight = 1 / (pdfRatio * pdfRatio + 1);

                    value += misWeight * emission * bsdfValue
                        * geometryTerms.cosineFrom / bsdfSample.jacobian;
                }

                value /= totalSpp;
                AddSplat(frameBuffer,
                    camSample.filmSample.x, camSample.filmSample.y, &value);
            }
        }
    }
    // });

    WriteImage(frameBuffer, "render.exr");

    auto endTime = std::chrono::high_resolution_clock::now();
    auto deltaTime = std::chrono::duration_cast<std::chrono::milliseconds>(endTime - startTime).count();
    std::cout << deltaTime << "ms" << std::endl;

    return 0;
}