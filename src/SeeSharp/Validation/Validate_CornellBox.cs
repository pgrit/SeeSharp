using SeeSharp.Core;

namespace SeeSharp.Validation {
    class Validate_CornellBox : ValidationSceneFactory {
        public override int SamplesPerPixel => 10;

        public override int MaxDepth => 5;

        public override string Name => "CornellBox";

        public override Scene MakeScene() {
            var scene = Scene.LoadFromFile("../data/scenes/cbox.json");
            scene.FrameBuffer = new FrameBuffer(512, 512, "");
            scene.Prepare();
            return scene;
        }
    }
}