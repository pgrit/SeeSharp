using SeeSharp.Experiments;
using System.Collections.Generic;
using System.Diagnostics;

namespace SeeSharp.Examples {
    class Program {
        static void Main(string[] args) {
            // The Benchmark class can be used to run multiple experiments,
            // for example to render different test scenes or different configurations.
            Benchmark bench = new(new Dictionary<string, ExperimentFactory>() {
                { "PathVsVcm", new PathVsVcm() },
            }, 512, 512) { DirectoryName = "Results" };
            bench.Run(format: ".exr");

            // Optional, but usually a good idea: assemble the rendering results in an overview
            // figure using a Python script.
            Process.Start("python", "./SeeSharp.Examples/MakeFigure.py Results/PathVsVcm PathTracer Vcm")
                .WaitForExit();

            // For our README file, we further convert the pdf to png with ImageMagick
            Process.Start("magick", "-density 300 ./Results/PathVsVcm/Overview.pdf ExampleFigure.png")
                .WaitForExit();
        }
    }
}
