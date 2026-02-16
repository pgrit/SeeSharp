using System;
using SeeSharp.Benchmark;
using SeeSharp.Experiments;
using SeeSharp.SceneManagement;
using SeeSharp.Integrators;
using SeeSharp.Integrators.Bidir;

SceneRegistry.AddSourceRelativeToScript("../data/scenes");

BenchRender("PathTracer - 16spp", new PathTracer() {
    NumIterations = 16,
});

BenchRender("BDPT - 8spp", new VertexCacheBidir() {
    NumIterations = 8,
});

BenchRender("VCM - 8spp", new VertexConnectionAndMerging() {
    NumIterations = 8,
});

void BenchRender(string name, Integrator integrator) {
    var scene =
        // SceneRegistry.LoadScene("StillLife").SceneLoader.Scene;
        SceneRegistry.Find("CornellBox").SceneLoader.Scene;

    // Dry run to eliminate JIT overhead
    scene.FrameBuffer = new(512, 512, "");
    scene.Prepare();
    integrator.Render(scene);

    int num = 2;
    long total = 0;
    for (int i = 0; i < num; ++i) {
        scene.FrameBuffer = new(512, 512, "");
        integrator.Render(scene);
        total += scene.FrameBuffer.RenderTimeMs;
    }
    Console.WriteLine($"{name}: {total / (double)num}");

    scene.FrameBuffer.WriteToFile(name + ".exr");
}

GenericMaterial_Sampling.QuickTest();

Console.WriteLine("Warmup run");
GenericMaterial_Sampling.BenchPerformance(100000);
GenericMaterial_Sampling.BenchPerformanceComponentPdfs(100000);
Console.WriteLine("=======================");
GenericMaterial_Sampling.BenchPerformance(500000);
GenericMaterial_Sampling.BenchPerformanceComponentPdfs(500000);

GenericMaterial_Sampling.Benchmark();

VectorBench.BenchComputeBasisVectors(10000000);