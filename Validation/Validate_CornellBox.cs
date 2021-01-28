using SeeSharp.Core;
using SeeSharp.Core.Image;

namespace SeeSharp.Validation {
    class Validate_CornellBox : ValidationSceneFactory {
        public override int SamplesPerPixel => 8;

        public override int MaxDepth => 5;

        public override string Name => "CornellBox";

        public override Scene MakeScene() {
            var scene = Scene.LoadFromFile("Data/scenes/cbox.json");
            scene.FrameBuffer = new FrameBuffer(512, 512, "");
            scene.Prepare();
            return scene;
        }
    }
}
