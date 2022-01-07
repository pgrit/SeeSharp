using System.IO;
using SeeSharp.Common;
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
        /// <param name="denoise">Whether to run Open Image Denoise on the flattened output image</param>
        /// <param name="interactive">If true, the image is displayed and continuously updated in the tev viewer.</param>
        static int Main(
            FileInfo scene,
            int samples = 8,
            int maxdepth = 5,
            int resx = 512,
            int resy = 512,
            string output = "Render.exr",
            bool flatten = true,
            string algo = "PT",
            bool denoise = true,
            bool interactive = false
        ) {
            if (scene == null) {
                Logger.Error("Please provide a scene filename via --scene [file.json]");
                return -1;
            }

            var sc = Scene.LoadFromFile(scene.FullName);
            var flags = interactive ? Image.FrameBuffer.Flags.SendToTev : Image.FrameBuffer.Flags.None;
            sc.FrameBuffer = new(resx, resy, output, flags);
            sc.Prepare();

            if (algo == "PT") {
                new PathTracer() {
                    MaxDepth = maxdepth,
                    TotalSpp = samples
                }.Render(sc);
            } else if (algo == "VCM") {
                new VertexConnectionAndMerging() {
                    MaxDepth = maxdepth,
                    NumIterations = samples,
                }.Render(sc);
            } else {
                Logger.Error($"Unknown rendering algorithm: {algo}. Use PT or VCM");
                return -1;
            }

            if (flatten && denoise)
                sc.FrameBuffer.GetLayer("denoised").Image.WriteToFile(output);
            else if (flatten)
                sc.FrameBuffer.Image.WriteToFile(output);
            else
                sc.FrameBuffer.WriteToFile();

            Logger.Log($"Done after {sc.FrameBuffer.MetaData["RenderTime"]}ms");

            return 0;
        }
    }
}
