using System.Diagnostics;
using System.Threading.Tasks;

namespace Experiments {
    class SimpleQuad {
        public void Run() {
            Ground.InitScene();

            float[] vertices = {
                0.0f, 0.0f, 0.0f,
                1.0f, 0.0f, 0.0f,
                1.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
            };

            int[] indices = {
                0, 1, 2,
                0, 2, 3
            };

            Ground.AddTriangleMesh(vertices, vertices.Length,
                indices, indices.Length);

            Ground.FinalizeScene();

            int imageWidth = 512;
            int imageHeight = 512;
            var image = Ground.CreateImage(imageWidth, imageHeight, 1);

            float[] topLeft = { -1.0f, -1.0f, 5.0f };
            float[] diag = { 3.0f, 3.0f, 0.0f };

            var stopWatch = Stopwatch.StartNew();

            Parallel.For(0, imageHeight, (int y) => {
                for (int x = 0; x < imageWidth; ++x) {
                    float[] org = {
                        topLeft[0] + (float)x / (float)imageWidth * diag[0],
                        topLeft[1] + (float)y / (float)imageHeight * diag[1],
                        5.0f
                    };
                    float[] dir = { 0.0f, 0.0f, -1.0f };

                    var hit = Ground.TraceSingle(org, dir);

                    float[] value = { hit.meshId };
                    Ground.AddSplat(image, x, y, value);
                }
            });

            stopWatch.Stop();
            System.Console.WriteLine(
                string.Format("{0}ms", stopWatch.Elapsed.TotalMilliseconds));

            Ground.WriteImage(image, "renderCS.exr");
        }
    }
}