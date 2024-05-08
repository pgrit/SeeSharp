using System.Diagnostics;
using SeeSharp.Experiments;
using SeeSharp.Images;
using SeeSharp.Examples;

// Register the directory as a scene file provider.
// Asides from the geometry, it is also used as a reference image cache.
SceneRegistry.AddSource("Data/Scenes");

// Configure a benchmark to compare path tracing and VCM on the CornellBox
// at 512x512 resolution. Display images in tev during rendering (localhost, default port)
Benchmark benchmark = new(new PathVsVcm(), [
    SceneRegistry.LoadScene("CornellBox", maxDepth: 5),
    // SceneRegistry.LoadScene("CornellBox", maxDepth: 2).WithName("CornellBoxDirectIllum")
], "Results/PathVsVcm", 512, 512, FrameBuffer.Flags.SendToTev);

// Render the images
benchmark.Run();

// Optional, but usually a good idea: assemble the rendering results in an overview
// figure using a Python script.
Process.Start("python", "./SeeSharp.Examples/MakeFigure.py Results/PathVsVcm PathTracer Vcm")
    .WaitForExit();

// For our README file, we further convert the pdf to png with ImageMagick
Process.Start("magick", "-density 300 ./Results/PathVsVcm/Overview.pdf ExampleFigure.png")
    .WaitForExit();
