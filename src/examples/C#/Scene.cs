namespace Experiments {

using System.Collections.Generic;

class Scene {
    public void SetupFrameBuffer(int width, int height) {
        this.frameBufferId = Ground.Image.CreateImageRGB(width, height);
    }

    public void LoadCornellBox() {
        Ground.Scene.InitScene();
        Ground.Scene.LoadSceneFromFile("../../data/scenes/cbox.json", 0);
        Ground.Scene.FinalizeScene();
        FindEmitters();
    }

    private void FindEmitters() {
        int numEmitters = Ground.Scene.GetNumberEmitters();
        for (int i = 0; i < numEmitters; i++) {
            emitterMeshes.Add(Ground.Scene.GetEmitterMesh(i));
        }
    }

    private int frameBufferId;
    private List<int> emitterMeshes = new List<int>();
}

}