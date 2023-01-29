using System;
using SeeSharp;
using SeeSharp.Benchmark;

var scene = Scene.LoadFromFile("Data/Scenes/CornellBox/CornellBox.json");
scene.FrameBuffer = new(512, 512, "");
scene.Prepare();
new SeeSharp.Integrators.PathTracer().Render(scene);
Console.WriteLine(scene.FrameBuffer.RenderTimeMs);

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
