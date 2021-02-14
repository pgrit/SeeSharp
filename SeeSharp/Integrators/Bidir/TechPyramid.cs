using SeeSharp.Shading;
using System.Collections.Generic;
using SeeSharp.Image;

using TechniqueNames = System.Collections.Generic.Dictionary
    <(int cameraPathEdges, int lightPathEdges, int totalEdges), string>;
using System.Numerics;

namespace SeeSharp.Integrators.Bidir {
    public class TechPyramid {
        public TechPyramid(int width, int height, int minDepth, int maxDepth, bool merges,
                           bool connections = true, bool lightTracer = true) {
            // Generate the filenames
            techniqueNames = new TechniqueNames();
            for (int depth = minDepth; depth <= maxDepth; ++depth) {
                // Hitting the light
                techniqueNames.Add(key: (cameraPathEdges: depth,
                                         lightPathEdges: 0,
                                         totalEdges: depth),
                                   value: $"{depth}-hit");

                if (depth == 1) continue;

                // Light tracer
                if (lightTracer)
                    techniqueNames.Add(key: (cameraPathEdges: 0,
                                            lightPathEdges: depth - 1,
                                            totalEdges: depth),
                                       value: $"{depth}-light-tracer");

                // Next event estimation
                techniqueNames.Add(key: (cameraPathEdges: depth - 1,
                                         lightPathEdges: 0,
                                         totalEdges: depth),
                                   value: $"{depth}-next-event");

                // All connections
                for (int i = 1; i < depth - 1 && connections; ++i) {
                    techniqueNames.Add(key: (cameraPathEdges: i,
                                             lightPathEdges: depth - i - 1,
                                             totalEdges: depth),
                                       value: $"{depth}-connect-{i}");
                }

                // All merges
                for (int i = 1; i < depth && merges; ++i) {
                    techniqueNames.Add(key: (cameraPathEdges: i,
                                             lightPathEdges: depth - i,
                                             totalEdges: depth),
                                       value: $"{depth}-merge-{i}");
                }
            }

            // Create an image for every technique
            techniqueImages = new Dictionary<(int, int, int), Image<ColorRGB>>();
            foreach (var tech in techniqueNames) {
                techniqueImages[tech.Key] = new Image<ColorRGB>(width, height);
            }
        }

        public void Add(int cameraPathEdges, int lightPathEdges, int totalEdges,
                        Vector2 filmPoint, ColorRGB value) {
            var image = techniqueImages[(cameraPathEdges, lightPathEdges, totalEdges)];
            image.Splat(filmPoint.X, filmPoint.Y, value);
        }

        public void WriteToFiles(string basename) {
            foreach (var t in techniqueImages) {
                var name = techniqueNames[t.Key];
                var filename = basename + $"{name}.exr";
                var image = t.Value;

                Image<ColorRGB>.WriteToFile(image, filename);
            }
        }

        TechniqueNames techniqueNames;
        Dictionary<(int, int, int), Image<ColorRGB>> techniqueImages;
    }
}
