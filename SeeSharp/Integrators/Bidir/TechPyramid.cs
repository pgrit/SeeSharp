using TechniqueNames = System.Collections.Generic.Dictionary
    <(int cameraPathEdges, int lightPathEdges, int totalEdges), string>;

namespace SeeSharp.Integrators.Bidir;

/// <summary>
/// Stores individual renderings of different MIS techniques
/// </summary>
public class TechPyramid {
    /// <summary>
    /// Initializes a technique pyramid for VCM
    /// </summary>
    /// <param name="width">Width of the image in pixels</param>
    /// <param name="height">Height of the image in pixels</param>
    /// <param name="minDepth">Minimum path length to track</param>
    /// <param name="maxDepth">Maximum path length to track</param>
    /// <param name="merges">If false, ignores merging techniques</param>
    /// <param name="connections">If false, ignores connections</param>
    /// <param name="lightTracer">If false, ignores light tracing</param>
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
        techniqueImages = new Dictionary<(int, int, int), RgbImage>();
        foreach (var tech in techniqueNames) {
            techniqueImages[tech.Key] = new RgbImage(width, height);
        }
    }

    /// <summary>
    /// Logs a sample to the pyramid. Identifies the technique based on the edge counts.
    /// </summary>
    /// <param name="cameraPathEdges">Number of edges along the camera subpath</param>
    /// <param name="lightPathEdges">Number of edges along the light subpath</param>
    /// <param name="totalEdges">Number of edges in the combined path</param>
    /// <param name="filmPoint">Position on the image that this path contributes to</param>
    /// <param name="value">The contribution</param>
    public void Add(int cameraPathEdges, int lightPathEdges, int totalEdges,
                    Pixel filmPoint, RgbColor value) {
        var image = techniqueImages[(cameraPathEdges, lightPathEdges, totalEdges)];
        image.AtomicAdd(filmPoint.Col, filmPoint.Row, value);
    }

    /// <summary>
    /// Multiplies all pixels in all images by the given factor.
    /// </summary>
    public void Normalize(float scalingFactor) {
        foreach (var t in techniqueImages) {
            t.Value.Scale(scalingFactor);
        }
    }

    /// <summary>
    /// Writes the images into separate files
    /// </summary>
    /// <param name="basename">First portion of the filenames, will be extended with a per-tech suffix</param>
    public void WriteToFiles(string basename) {
        foreach (var t in techniqueImages) {
            var name = techniqueNames[t.Key];
            var filename = basename + $"{name}.exr";
            t.Value.WriteToFile(filename);
        }
    }

    TechniqueNames techniqueNames;
    Dictionary<(int, int, int), RgbImage> techniqueImages;
}
