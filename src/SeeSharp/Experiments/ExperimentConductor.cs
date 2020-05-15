using GroundWrapper;
using Integrators;
using System.IO;

namespace Experiments {
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
        public void Run() {
            var scene = factory.MakeScene();

            RenderReference(false);

            var methods = factory.MakeMethods();
            foreach (var method in methods) {
                var name = method.Key;
                var integrator = method.Value;

                string path = Path.Join(workingDirectory, name);
                Render(path, "render.exr", integrator, scene);
            }
        }

        public void RenderReference(bool force) {
            bool exists = File.Exists(Path.Join(workingDirectory, "reference.exr"));
            if (!exists || force) {
                var scene = factory.MakeScene();
                var integrator = factory.MakeReferenceIntegrator();
                Render(workingDirectory, "reference.exr", integrator, scene);
            }
        }

        private double Render(string dir, string filename, Integrator integrator, Scene scene) {
            scene.FrameBuffer = new Image(width, height);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            integrator.Render(scene);
            stopwatch.Stop();

            scene.FrameBuffer.WriteToFile(Path.Join(dir, filename));

            return stopwatch.ElapsedMilliseconds / 1000.0;
        }

        ExperimentFactory factory;
        string workingDirectory;
        int width, height;
    }
}
