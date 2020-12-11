using SeeSharp.Core;
using SeeSharp.Core.Image;

namespace SeeSharp.Validation {
    class Validate_RoughGlassesSoloHdri : ValidationSceneFactory {
        public override int SamplesPerPixel => 32;

        public override int MaxDepth => 10;

        public override string Name => "RoughGlassesSoloHdri";

        public override Scene MakeScene() {
            var scene = Scene.LoadFromFile("../data/scenes/RoughGlasses/RoughGlasses-SoloHdri.json");
            scene.FrameBuffer = new FrameBuffer(640, 480, "");
            scene.Prepare();
            return scene;
        }
    }
}
