using System;
using System.IO;
using SeeSharp.Integrators;
using SeeSharp.Integrators.Bidir;

namespace SeeSharp.PreviewRender {
    class Program {
        /// <summary>
        /// SeeSharp preview renderer
        /// </summary>
        /// <param name="scene">Path to the .json scene file to render</param>
        /// <param name="samples">Number of samples per pixel to render</param>
        /// <param name="maxdepth">Maximum path length (number of edges)</param>
        /// <param name="resx">Width of the rendered image in pixels</param>
        /// <param name="resy">Height of the rendered image in pixels</param>
        /// <param name="output">Name of the output file</param>
        /// <param name="flatten">Set to false to write a multi-layer file with AOVs</param>
        /// <param name="algo">One of: PT, VCM</param>
        static int Main(
            FileInfo scene,
            int samples = 8,
            int maxdepth = 5,
            int resx = 512,
            int resy = 512,
            string output = "Render.exr",
            bool flatten = true,
            string algo = "PT"
        ) {
            if (scene == null) {
                Console.WriteLine("Please provide a scene filename via --scene [file.json]");
                return -1;
            }

            var sc = Scene.LoadFromFile(scene.FullName);
            sc.FrameBuffer = new(resx, resy, output);
            sc.Prepare();

            if (algo == "PT") {
                new PathTracer() {
                    MaxDepth = (uint)maxdepth,
                    TotalSpp = samples
                }.Render(sc);
            } else if (algo == "VCM") {
                new VertexConnectionAndMerging() {
                    MaxDepth = maxdepth,
                    NumIterations = samples
                }.Render(sc);
            } else {
                Console.WriteLine($"Unknown rendering algorithm: {algo}");
            }

            if (flatten)
                sc.FrameBuffer.Image.WriteToFile(output);
            else
                sc.FrameBuffer.WriteToFile();

            return 0;
        }
    }
}
