using SeeSharp.Image;

namespace SeeSharp.Validation {
    class Validate_Textures : ValidationSceneFactory {
        public override int SamplesPerPixel => 8;

        public override int MaxDepth => 5;

        public override string Name => "TextureTest";

        public override Scene MakeScene() {
            var scene = Scene.LoadFromFile("Data/Scenes/TextureTest/TextureTest.json");
            scene.FrameBuffer = new FrameBuffer(700, 500, "");
            scene.Prepare();
            return scene;
        }
    }
}
