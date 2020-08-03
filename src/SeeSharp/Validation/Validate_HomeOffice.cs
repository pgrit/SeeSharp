using SeeSharp.Core;
using SeeSharp.Core.Image;

namespace SeeSharp.Validation {
    class Validate_HomeOffice : ValidationSceneFactory {
        public override int SamplesPerPixel => 8;

        public override int MaxDepth => 5;

        public override string Name => "HomeOffice";

        public override Scene MakeScene() {
            var scene = Scene.LoadFromFile("../data/scenes/HomeOffice/office.json");
            scene.FrameBuffer = new FrameBuffer(640, 480, "");
            scene.Prepare();
            return scene;
        }
    }
}
