#include <vector>
#include <chrono>
#include <iostream>

#include <renderground/renderground.h>

#include <tbb/parallel_for.h>

class PathTracer {
public:
    const int imageWidth = 1024;
    const int imageHeight = 1024;
    const int totalSpp = 2;
    const int maxDepth = 2;
    const uint64_t BaseSeed = 0xC030114Ui64;

private:
    int frameBuffer;
    int lightMesh;
    int camId;

public:
    PathTracer(const std::string& filename) {
        frameBuffer = CreateImageRGB(imageWidth, imageHeight);
        LoadScene(filename);
        int numEmitter = GetNumberEmitters();
        lightMesh = GetEmitterMesh(0); // TODO get them all and build a light selection structure
        camId = 0;
    }

    void Render() {
        tbb::parallel_for(tbb::blocked_range<int>(0, imageHeight), [&](tbb::blocked_range<int> r) {
            for (int y = r.begin(); y < r.end(); ++y) {
                for (int x = 0; x < imageWidth; ++x) {
                    RenderPixel(x, y);
                }
            }
        });

        WriteImage(frameBuffer, "render.exr");
    }

    void RenderPixel(int x, int y) {
        for (int sampleIdx = 0; sampleIdx < totalSpp; ++sampleIdx) {
            const auto seed = HashSeed(HashSeed(BaseSeed, (y * imageWidth + x)), sampleIdx);
            RNG rng(seed);

            // Generate a ray from the camera
            CameraSampleInfo camSample;
            camSample.filmSample = Vector2{ x + rng.NextFloat(), y + rng.NextFloat() };
            auto ray = GenerateCameraRay(camId, camSample);

            auto value = EstimateIncidentRadiance(ray, rng, 1, nullptr, 0.0f);
            value = value * (1.0f / totalSpp);

            AddSplatRGB(frameBuffer, camSample.filmSample.x, camSample.filmSample.y, value);
        }
    }

    ColorRGB EstimateIncidentRadiance(const Ray& ray, RNG& rng, int depth, const Hit* previousHit, float previousPdf) {
        if (depth >= maxDepth) 
            return ColorRGB{ 0, 0, 0 };

        auto hit = TraceSingle(ray);
        if (hit.point.meshId == INVALID_MESH_ID) 
            return ColorRGB{ 0, 0, 0 };

        ColorRGB value{ 0, 0, 0 };
        if (hit.point.meshId == lightMesh) {
            float misWeight = 1.0f;
            if (depth > 1) { // directly visible emitters are not explicitely connected
                // Compute the surface area PDFs.
                auto geometryTerms = ComputeGeometryTerms(&previousHit->point, &hit.point);
                float pdfNextEvt = ComputePrimaryToEmitterSurfaceJacobian(&hit.point);
                float pdfBsdf = previousPdf * geometryTerms.cosineTo / geometryTerms.squaredDistance;

                // Compute MIS weights
                float pdfRatio = pdfNextEvt / pdfBsdf;
                misWeight = 1 / (pdfRatio * pdfRatio + 1);
            }

            auto emission = ComputeEmission(&hit.point, -ray.direction);
            value = value + misWeight * emission;
        }

        // Estimate DI via next event shadow ray
        auto lightSample = WrapPrimarySampleToEmitterSurface(0, rng.NextFloat(), rng.NextFloat());
        if (!IsOccluded(&hit.point, lightSample.point.position)) {
            Vector3 lightDir = hit.point.position - lightSample.point.position;
            auto emission = ComputeEmission(&lightSample.point,
                lightDir);

            auto bsdfValue = EvaluateBsdf(&hit.point, -ray.direction, lightDir, false);
            float shadingCosine = ComputeShadingCosine(&hit.point, -ray.direction, lightDir, false);
            auto geometryTerms = ComputeGeometryTerms(&hit.point, &lightSample.point);

            // Compute MIS weights
            float pdfNextEvt = lightSample.jacobian;
            float pdfBsdf = ComputePrimaryToBsdfJacobian(&hit.point, -ray.direction,
                lightDir, false).jacobian * geometryTerms.cosineTo / geometryTerms.squaredDistance;
            float pdfRatio = pdfBsdf / pdfNextEvt;
            float misWeight = 1 / (pdfRatio * pdfRatio + 1);

            if (geometryTerms.cosineFrom > 0) {
                value = value + misWeight * emission * bsdfValue
                    * (geometryTerms.geomTerm / lightSample.jacobian)
                    * (shadingCosine / geometryTerms.cosineFrom);
            }
        }

        // Continue path via BSDF importance sampling
        auto bsdfSample = WrapPrimarySampleToBsdf(&hit.point,
            -ray.direction, rng.NextFloat(), rng.NextFloat(), false);
        auto bsdfValue = EvaluateBsdf(&hit.point, -ray.direction,
            bsdfSample.direction, false);
        float shadingCosine = ComputeShadingCosine(&hit.point, -ray.direction,
            bsdfSample.direction, false);
        auto bsdfSampleWeight = bsdfSample.jacobian == 0.0f ? ColorRGB{ 0,0,0 } : bsdfValue * (shadingCosine / bsdfSample.jacobian);

        auto bsdfRay = SpawnRay(hit.point, bsdfSample.direction);
        return value + bsdfSampleWeight * EstimateIncidentRadiance(bsdfRay, rng, depth + 1, &hit, bsdfSample.jacobian);
    }

private:
    void LoadScene(const std::string& filename) {
        InitScene();
        LoadSceneFromFile("../../data/scenes/cbox.json", 0);
        FinalizeScene();
    }
};

int main() {
    PathTracer integrator("../../data/scenes/cbox.json");

    auto startTime = std::chrono::high_resolution_clock::now();

    integrator.Render();

    auto endTime = std::chrono::high_resolution_clock::now();
    auto deltaTime = std::chrono::duration_cast<std::chrono::milliseconds>(endTime - startTime).count();
    std::cout << deltaTime << "ms" << std::endl;

    return 0;
}