using SeeSharp.Images;

namespace SeeSharp.Validation;

class Validate_Environment : ValidationSceneFactory {
    public override int SamplesPerPixel => 10;

    public override int MaxDepth => 5;

    public override string Name => "Environment";

    public override Scene MakeScene() {
        var scene = Scene.LoadFromFile("Data/Scenes/simplebackground.json");
        scene.FrameBuffer = new FrameBuffer(512, 512, "");
        scene.Prepare();
        return scene;
    }
}
