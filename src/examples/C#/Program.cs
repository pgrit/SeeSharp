using System;

namespace Experiments {

class Program {
    static void Main(string[] args) {
        var scene = new Scene();
        scene.SetupFrameBuffer(512, 512);
        scene.LoadCornellBox();

        var algorithm = new PathTracer();
        algorithm.Render();
    }
}

}
