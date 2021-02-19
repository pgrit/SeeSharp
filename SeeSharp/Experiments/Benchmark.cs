using System.Collections.Generic;
using System.IO;

namespace SeeSharp.Experiments {
    public class Benchmark {
        public string DirectoryName = "Results";

        public Benchmark(Dictionary<string, ExperimentFactory> experiments,
                         int imageWidth, int imageHeight) {
            this.Experiments = experiments;
            this.imageWidth = imageWidth;
            this.imageHeight = imageHeight;
        }

        public void Run(List<string> sceneFilter = null, bool forceReference = false, string format = ".exr",
                        bool skipReference = false) {
            foreach (var experiment in Experiments) {
                if (sceneFilter != null && !sceneFilter.Contains(experiment.Key))
                    continue;

                var conductor = new ExperimentConductor(experiment.Value,
                    Path.Join(DirectoryName, experiment.Key), imageWidth, imageHeight);
                conductor.Run(forceReference, format, skipReference);
            }
        }

        public Dictionary<string, ExperimentFactory> Experiments;
        int imageWidth, imageHeight;
    }
}