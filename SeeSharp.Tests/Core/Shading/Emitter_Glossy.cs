using SeeSharp.Geometry;
using SeeSharp.Shading.Emitters;
using SimpleImageIO;
using System.Numerics;
using Xunit;

namespace Tests.Shading {
    public class Emitter_Glossy {
        [Fact]
        public void EmittedRays_SampleInverse() {
            var mesh = new Mesh(
                new Vector3[] {
                    new Vector3(-1, 10, -1),
                    new Vector3( 1, 10, -1),
                    new Vector3( 1, 10,  1),
                    new Vector3(-1, 10,  1)
                }, new int[] {
                    0, 1, 2,
                    0, 2, 3
                },
                shadingNormals: new Vector3[] {
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0)
                }
            );
            var emitter = new GlossyEmitter(mesh, RgbColor.White, 50);

            var sample = emitter.SampleRay(new Vector2(0.3f, 0.8f), new Vector2(0.56f, 0.03f));

            var (posP, dirP) = emitter.SampleRayInverse(sample.Point, sample.Direction);

            Assert.Equal(0.3f, posP.X, 3);
            Assert.Equal(0.8f, posP.Y, 3);
            Assert.Equal(0.56f, dirP.X, 3);
            Assert.Equal(0.03f, dirP.Y, 3);
        }
    }
}
