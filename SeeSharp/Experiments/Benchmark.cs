using SeeSharp.Image;
using System.IO;
using SimpleImageIO;
using System.Collections.Generic;
using SeeSharp.Common;

namespace SeeSharp.Experiments {
    /// <summary>
    /// Conducts an experiment by rendering all images.
    /// </summary>
    public class Benchmark {
        public Benchmark(Experiment experiment, List<SceneConfig> sceneConfigs,
                         string workingDirectory, int width, int height,
                         FrameBuffer.Flags frameBufferFlags) {
            this.experiment = experiment;
            this.sceneConfigs = sceneConfigs;
            this.workingDirectory = workingDirectory;
            this.width = width;
            this.height = height;
            this.frameBufferFlags = frameBufferFlags;
        }

        /// <summary>
        /// Renders all scenes with all methods, generating one result directory per scene.
        /// If the reference images do not exist yet, they are also rendered. Each method's
        /// images are placed in a separate folder, using the method's name as the folder's name.
        /// </summary>
        public void Run(string format = ".exr", bool skipReference = false) {
            foreach (SceneConfig scene in sceneConfigs)
                RunScene(scene, format, skipReference);
        }

        void RunScene(SceneConfig sceneConfig, string format, bool skipReference) {
            string dir = Path.Join(workingDirectory, sceneConfig.Name);

            if (!skipReference) {
                string refFilename = Path.Join(dir, "Reference" + format);
                RgbImage refImg = sceneConfig.GetReferenceImage(width, height);
                refImg.WriteToFile(refFilename);
            }

            // Prepare a scene for rendering. We do it once to reduce overhead.
            Scene scene = sceneConfig.MakeScene();
            scene.FrameBuffer = MakeFrameBuffer("dummy");
            scene.Prepare();

            var methods = experiment.MakeMethods();
            foreach (var method in methods) {
                string path = Path.Join(dir, method.name);

                // Clear old files (if there are any)
                if (Directory.Exists(path)) {
                    var dirinfo = new System.IO.DirectoryInfo(path);
                    foreach (var file in dirinfo.GetFiles()) {
                        if (file.Extension == format)
                            file.Delete();
                    }
                }

                Logger.Log($"Rendering {sceneConfig.Name} with {method.name}");
                scene.FrameBuffer = MakeFrameBuffer(Path.Join(path, "Render" + format));
                method.integrator.MaxDepth = sceneConfig.MaxDepth;
                method.integrator.Render(scene);
                scene.FrameBuffer.WriteToFile();
            }
        }

        protected virtual FrameBuffer MakeFrameBuffer(string filename)
        => new FrameBuffer(width, height, filename, frameBufferFlags);

        protected int width, height;
        protected Experiment experiment;
        string workingDirectory;
        List<SceneConfig> sceneConfigs;
        FrameBuffer.Flags frameBufferFlags;
    }
}
