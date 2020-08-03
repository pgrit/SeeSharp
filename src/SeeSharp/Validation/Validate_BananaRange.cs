using SeeSharp.Core;
using SeeSharp.Core.Image;

namespace SeeSharp.Validation {
    class Validate_BananaRange : ValidationSceneFactory {
        public override int SamplesPerPixel => 8;

        public override int MaxDepth => 5;

        public override string Name => "BananaRange";

        public override Scene MakeScene() {
            var scene = Scene.LoadFromFile("../data/scenes/BananaRange/banana_range.json");
            scene.FrameBuffer = new FrameBuffer(640, 480, "");
            scene.Prepare();
            return scene;
        }
    }
}
