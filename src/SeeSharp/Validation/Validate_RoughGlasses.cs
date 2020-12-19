using SeeSharp.Core;
using SeeSharp.Core.Image;

namespace SeeSharp.Validation {
    class Validate_RoughGlassesSoloHdri : ValidationSceneFactory {
        public override int SamplesPerPixel => 16;

        public override int MaxDepth => 5;

        public override string Name => "RoughGlassesSoloHdri";

        public override Scene MakeScene() {
            var scene = Scene.LoadFromFile("../data/scenes/RoughGlasses/RoughGlasses-SoloHdri.json");
            scene.FrameBuffer = new FrameBuffer(640, 480, "");
            scene.Prepare();
            return scene;
        }
    }

    class Validate_RoughGlassesIndirect : ValidationSceneFactory {
        public override int SamplesPerPixel => 16;

        public override int MaxDepth => 5;

        public override string Name => "RoughGlassesIndirect";

        public override Scene MakeScene() {
            var scene = Scene.LoadFromFile("../data/scenes/RoughGlasses/RoughGlasses-Indirect.json");
            scene.FrameBuffer = new FrameBuffer(640, 480, "");
            scene.Prepare();
            return scene;
        }
    }
}
