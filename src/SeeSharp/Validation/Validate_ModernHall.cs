using SeeSharp.Core;
using SeeSharp.Core.Image;

namespace SeeSharp.Validation {
    class Validate_ModernHall : ValidationSceneFactory {
        public override int SamplesPerPixel => 8;

        public override int MaxDepth => 5;

        public override string Name => "ModernHall";

        public override Scene MakeScene() {
            var scene = Scene.LoadFromFile("../data/scenes/ModernHall/ModernHall.json");
            scene.FrameBuffer = new FrameBuffer(640, 480, "");
            scene.Prepare();
            return scene;
        }
    }
}