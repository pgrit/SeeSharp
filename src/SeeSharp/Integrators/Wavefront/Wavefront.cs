using System.Threading.Tasks;
using SeeSharp.Core;
using SeeSharp.Core.Geometry;

namespace SeeSharp.Integrators.Wavefront {
    public class Wavefront {
        Scene scene;
        Ray[] rays;
        SurfacePoint[] hits;
        bool[] isActive;
        int size;

        public Wavefront(Scene scene, int size) {
            this.scene = scene;
            this.size = size;
        }

        public int Size => size;

        public delegate Ray MakeElement(int idx);
        public void Init(MakeElement callback) {
            rays = new Ray[size];
            hits = new SurfacePoint[size];
            isActive = new bool[size];
            Parallel.For(0, size, (idx) => {
                rays[idx] = callback(idx);
                isActive[idx] = true;
            });
        }

        public void Intersect() {
            // TODO we need to make sure no masked out rays are traced needlessly
            // group the arrays!

            scene.Raytracer.Trace(rays, hits);
        }

        public delegate bool ElementAction(int idx, Ray ray, SurfacePoint hit);
        public void Process(ElementAction action) {
            Parallel.For(0, size, (idx) => {
                if (!isActive[idx])
                    return;
                isActive[idx] = action(idx, rays[idx], hits[idx]);
            });
        }

        public delegate Ray? ContinueCallback(int idx, Ray ray, SurfacePoint hit);
        public void ContinuePaths(ContinueCallback action) {
            Parallel.For(0, size, (idx) => {
                if (!isActive[idx])
                    return;
                var nextRay = action(idx, rays[idx], hits[idx]);
                if (!nextRay.HasValue)
                    isActive[idx] = false;
                else
                    rays[idx] = nextRay.Value;
            });
        }
    }
}