using SeeSharp.Core.Shading;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SeeSharp.Integrators.Util {
    public class LoggedPath : ICloneable {
        public List<Vector3> Vertices = new();
        public ColorRGB Contribution = ColorRGB.Black;
        public List<int> UserTypes = new();

        public object Clone() => new LoggedPath {
            Vertices = new(Vertices),
            Contribution = Contribution,
            UserTypes = new(UserTypes)
        };
    }

    public class PathLogger {
        public PathLogger(int imageWidth, int imageHeight) {
            pixelPaths = new List<LoggedPath>[imageWidth * imageHeight];
            for (int i = 0; i < pixelPaths.Length; ++i)
                pixelPaths[i] = new List<LoggedPath>();
            width = imageWidth;
            height = imageHeight;
        }

        public struct PathIndex {
            public int Pixel { get; init; }
            public int Local { get; init; }
        }

        public PathIndex StartNew(Vector2 pixel) {
            var paths = pixelPaths[PixelToIndex(pixel)];
            int id;
            lock (paths) {
                id = paths.Count;
                paths.Add(new LoggedPath());
            }
            return new PathIndex { Pixel = PixelToIndex(pixel), Local = id };
        }

        public PathIndex Split(PathIndex original) {
            var paths = pixelPaths[original.Pixel];
            int id;
            lock (paths) {
                id = paths.Count;
                paths.Add(paths[original.Local].Clone() as LoggedPath);
            }
            return new PathIndex { Pixel = original.Pixel, Local = id };
        }

        public void Continue(PathIndex id, Vector3 nextVertex, int type, ColorRGB partialContrib) {
            pixelPaths[id.Pixel][id.Local].Vertices.Add(nextVertex);
            pixelPaths[id.Pixel][id.Local].UserTypes.Add(type);
            pixelPaths[id.Pixel][id.Local].Contribution += partialContrib;
        }

        int PixelToIndex(Vector2 pixel) {
            int col = Math.Clamp((int)(pixel.X / width), 0, width - 1);
            int row = Math.Clamp((int)(pixel.Y / height), 0, width - 1);
            return row * width + col;
        }

        public List<LoggedPath> GetAllInPixel(int row, int col, ColorRGB minContrib) {
            List<LoggedPath> result = new();
            var candidates = pixelPaths[row * width + col];
            foreach (var c in candidates) {
                if (minContrib != null && c.Contribution.R < minContrib.R
                    && c.Contribution.G < minContrib.G && c.Contribution.B < minContrib.B)
                    continue;
                result.Add(c);
            }
            return result;
        }

        public void WriteToFile(string filename) { // TODO JSON serialize the class
            throw new NotImplementedException();
        }

        public static PathLogger ReadFromFile(string filename) { // TODO JSON deserialize the class
            throw new NotImplementedException();
        }

        List<LoggedPath>[] pixelPaths;
        int width, height;
    }
}