using SeeSharp;
using SeeSharp.Integrators;
using SeeSharp.Image;
using System;
using System.IO;

namespace SeeSharp.Experiments {
    public class ExperimentConductor {
        public ExperimentConductor(ExperimentFactory experimentFactory,
                                   string workingDirectory,
                                   int width, int height) {
            factory = experimentFactory;
            this.workingDirectory = workingDirectory;
            this.width = width;
            this.height = height;
        }

        /// <summary>
        /// Renders the scene with all methods. If the reference image does not exist yet,
        /// it is also rendered.
        /// Each method's images are placed in a separate folder, using the method's name
        /// as the folder's name.
        /// </summary>
        public void Run(bool forceReference = false) {
            var scene = factory.MakeScene();

            // Render the reference image (only if it does not exist or forced)
            scene.FrameBuffer = new FrameBuffer(width, height, Path.Join(workingDirectory, "reference.exr"));
            scene.Prepare();
            RenderReference(scene, forceReference);

            var methods = factory.MakeMethods();
            foreach (var method in methods) {
                string path = Path.Join(workingDirectory, method.name);

                // Clear old files (if there are any)
                if (Directory.Exists(path)) {
                    var dirinfo = new System.IO.DirectoryInfo(path);
                    foreach (var file in dirinfo.GetFiles()) {
                        if (file.Extension == ".exr")
                            file.Delete();
                    }
                }

                // Render
                Console.WriteLine($"Starting {method.name}...");
                var timeSeconds = Render(path, "render.exr", method.integrator, scene);
                Console.WriteLine($"{method.name} done after {timeSeconds} seconds");
            }
        }

        private void RenderReference(Scene scene, bool force) {
            string filepath = Path.Join(workingDirectory, "reference.exr");
            bool exists = File.Exists(filepath);
            if (!exists || force) {
                var integrator = factory.MakeReferenceIntegrator();
                scene.FrameBuffer = new FrameBuffer(width, height, filepath, FrameBuffer.Flags.SendToTev);

                Console.WriteLine($"Starting reference...");
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                integrator.Render(scene);

                stopwatch.Stop();
                var timeSeconds = stopwatch.ElapsedMilliseconds / 1000.0;
                Console.WriteLine($"Reference done after {timeSeconds} seconds");

                scene.FrameBuffer.WriteToFile();
            }
        }

        protected virtual FrameBuffer MakeFrameBuffer(string filename)
        => new FrameBuffer(width, height, filename, factory.FrameBufferFlags);

        private double Render(string dir, string filename, Integrator integrator, Scene scene) {
            scene.FrameBuffer = MakeFrameBuffer(Path.Join(dir, filename));
            integrator.Render(scene);
            scene.FrameBuffer.WriteToFile();
            return scene.FrameBuffer.RenderTimeMs / 1000.0;
        }

        protected int width, height;
        protected ExperimentFactory factory;
        string workingDirectory;
    }
}
