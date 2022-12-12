using System;
using SeeSharp;
using SeeSharp.Benchmark;

var scene = Scene.LoadFromFile("Data/Scenes/CornellBox/CornellBox.json");
scene.FrameBuffer = new(512, 512, "");
scene.Prepare();
new SeeSharp.Integrators.PathTracer().Render(scene);
Console.WriteLine(scene.FrameBuffer.RenderTimeMs);

// GenericMaterial_Sampling.QuickTest();
// GenericMaterial_Sampling.BenchPerformance();
// GenericMaterial_Sampling.Benchmark();

// VectorBench.BenchComputeBasisVectors(10000000);
