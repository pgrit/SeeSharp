namespace SeeSharp.Experiments;

/// <summary>
/// Conducts an experiment by rendering all images.
/// </summary>
public class Benchmark {
    /// <summary>
    /// Sets up a new benchmark that runs an experiment on different scenes
    /// </summary>
    /// <param name="experiment">The experiment (list of methods) to run</param>
    /// <param name="sceneConfigs">The scene configurations</param>
    /// <param name="workingDirectory">
    ///     Directory to which the rendered images and other data will be written
    /// </param>
    /// <param name="width">Width of the rendered images in pixels</param>
    /// <param name="height">Height of the rendered images in pixels</param>
    /// <param name="frameBufferFlags">Flags for the frame buffer, e.g., to sync with tev</param>
    /// <param name="computeErrorMetrics">Compute error metrics when reference is available</param>
    public Benchmark(Experiment experiment, List<SceneConfig> sceneConfigs,
                     string workingDirectory, int width, int height,
                     FrameBuffer.Flags frameBufferFlags = FrameBuffer.Flags.None,
                     bool computeErrorMetrics = false) {
        this.experiment = experiment;
        this.sceneConfigs = sceneConfigs;
        this.workingDirectory = workingDirectory;
        this.width = width;
        this.height = height;
        this.frameBufferFlags = frameBufferFlags;
        this.computeErrorMetrics = computeErrorMetrics;
    }

    /// <summary>
    /// Renders all scenes with all methods, generating one result directory per scene.
    /// If the reference images do not exist yet, they are also rendered. Each method's
    /// images are placed in a separate folder, using the method's name as the folder's name.
    /// </summary>
    public void Run(string format = ".exr", bool skipReference = false) {
        experiment.OnStart(workingDirectory);
        foreach (SceneConfig scene in sceneConfigs)
            RunScene(scene, format, skipReference);
        experiment.OnDone(workingDirectory);
    }

    void RunScene(SceneConfig sceneConfig, string format, bool skipReference) {
        string dir = Path.Join(workingDirectory, sceneConfig.Name);
        Logger.Log($"Running scene '{sceneConfig.Name}'", Verbosity.Info);

        RgbImage refImg = null;
        if (!skipReference) {
            string refFilename = Path.Join(dir, "Reference" + format);
            refImg = sceneConfig.GetReferenceImage(width, height);
            refImg.WriteToFile(refFilename);
        }

        // Prepare a scene for rendering. We do it once to reduce overhead.
        using Scene scene = sceneConfig.MakeScene();
        scene.FrameBuffer = MakeFrameBuffer("dummy");
        scene.Prepare();

        experiment.OnStartScene(scene, dir);
        var methods = experiment.MakeMethods();
        foreach (var method in methods) {
            string path = Path.Join(dir, method.Name);

            Logger.Log($"Rendering {sceneConfig.Name} with {method.Name}");
            scene.FrameBuffer = MakeFrameBuffer(Path.Join(path, "Render" + format));
            method.Integrator.MaxDepth = sceneConfig.MaxDepth;
            method.Integrator.MinDepth = sceneConfig.MinDepth;

            if (computeErrorMetrics && refImg != null)
                scene.FrameBuffer.ReferenceImage = refImg;

            method.Integrator.Render(scene);
            scene.FrameBuffer.WriteToFile();
        }
        experiment.OnDoneScene(scene, dir);
    }

    /// <summary>
    /// Creates a new frame buffer with the correct resolution
    /// </summary>
    /// <param name="filename">Desired file name</param>
    protected virtual FrameBuffer MakeFrameBuffer(string filename)
    => new(width, height, filename, frameBufferFlags);

    /// <summary>
    /// Resolution of all images
    /// </summary>
    protected int width, height;

    /// <summary>
    /// The experiment to run on all scenes
    /// </summary>
    protected Experiment experiment;

    readonly string workingDirectory;
    readonly List<SceneConfig> sceneConfigs;
    readonly FrameBuffer.Flags frameBufferFlags;
    readonly bool computeErrorMetrics;
}
