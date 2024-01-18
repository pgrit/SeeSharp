using System;
using SeeSharp;
using SeeSharp.Benchmark;
using SeeSharp.Experiments;

// Measure PT performance. Start with a dry run to give JIT a chance to do its magic
var scene = Scene.LoadFromFile("Data/Scenes/CornellBox/CornellBox.json");
// var scene = SceneRegistry.LoadScene("StillLife").MakeScene();

scene.FrameBuffer = new(512, 512, "");
scene.Prepare();
new SeeSharp.Integrators.PathTracer(){
    TotalSpp = 32
}.Render(scene);

int num = 2;
long total = 0;
for (int i = 0; i < num; ++i) {
    scene.FrameBuffer = new(512, 512, "");
    new SeeSharp.Integrators.PathTracer() {
        TotalSpp = 32
    }.Render(scene);
    total += scene.FrameBuffer.RenderTimeMs;
}
Console.WriteLine(total / (double)num);

// scene.FrameBuffer.WriteToFile("test.exr");

GenericMaterial_Sampling.QuickTest();

Console.WriteLine("Warmup run");
GenericMaterial_Sampling.BenchPerformance(100000);
GenericMaterial_Sampling.BenchPerformanceComponentPdfs(100000);
Console.WriteLine("=======================");
GenericMaterial_Sampling.BenchPerformance(500000);
GenericMaterial_Sampling.BenchPerformanceComponentPdfs(500000);

GenericMaterial_Sampling.Fresnel(500000);

GenericMaterial_Sampling.Benchmark();

VectorBench.BenchComputeBasisVectors(10000000);
