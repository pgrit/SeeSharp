using SeeSharp.Geometry;
using SimpleImageIO;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SeeSharp.Integrators.Util {
    public class PathVisualizer : DebugVisualizer {
        public Dictionary<int, RgbColor> TypeToColor;
        public List<LoggedPath> Paths;

        public float Radius = 0.01f;
        public float HeadHeight = 0.02f;
        public int NumSegments = 16;

        public override void Render(Scene scene) {
            curScene = scene;

            if (Paths != null) {
                // Generate and add geometry for the selected paths
                MakePathArrows();
                scene.Prepare();
            }

            base.Render(scene);

            // Remove the marker meshes from the scene and trigger acceleration structure regeneration
            foreach (var mesh in markerTypes.Keys) {
                scene.Meshes.Remove(mesh);
            }
            scene.Prepare();
        }

        public override RgbColor ComputeColor(SurfacePoint hit, Vector3 from) {
            int type;
            if (!markerTypes.TryGetValue(hit.Mesh, out type))
                return base.ComputeColor(hit, from);

            RgbColor color = new RgbColor(1, 0, 0);
            TypeToColor.TryGetValue(type, out color);

            float cosine = Math.Abs(Vector3.Dot(hit.Normal, from));
            cosine /= hit.Normal.Length();
            cosine /= from.Length();
            return color * cosine;
        }

        void MakeArrow(Vector3 start, Vector3 end, int type) {
            float headHeight = curScene.Radius * HeadHeight;
            float radius = curScene.Radius * Radius;

            var headStart = end + headHeight * (start - end);
            var line = MeshFactory.MakeCylinder(start, headStart, radius, NumSegments);
            var head = MeshFactory.MakeCone(headStart, end, radius * 2, NumSegments);
            line.Material = new SeeSharp.Shading.Materials.DiffuseMaterial(new());
            head.Material = new SeeSharp.Shading.Materials.DiffuseMaterial(new());

            curScene.Meshes.Add(line);
            curScene.Meshes.Add(head);

            markerTypes.Add(line, type);
            markerTypes.Add(head, type);
        }

        void MakePathArrows() {
            foreach (var path in Paths) {
                // Iterate over all edges
                for (int i = 0; i < path.Vertices.Count - 1; ++i) {
                    var start = path.Vertices[i];
                    var end = path.Vertices[i + 1];
                    int type = path.UserTypes[i];
                    MakeArrow(start, end, type);
                }
            }
        }

        Dictionary<Mesh, int> markerTypes = new();
        Scene curScene;
    }
}