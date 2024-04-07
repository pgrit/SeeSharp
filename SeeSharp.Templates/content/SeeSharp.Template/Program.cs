using SeeSharp.Template;

// Additional scene directories can be added here, or globally via the environment variable "SEESHARP_SCENE_DIRS"
SceneRegistry.AddSourceRelativeToScript("./Scenes");

new Benchmark(
    new MyExperiment(), // the experiment to run
    [ // list of all scenes to render
        SceneRegistry.LoadScene("ExampleScene", maxDepth: 100),
    ],
    "Results", // name of the output directory
    1280, 768 // image resolution
).Run(skipReference: false);

