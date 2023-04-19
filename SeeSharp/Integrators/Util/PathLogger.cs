namespace SeeSharp.Integrators.Util;

/// <summary>
/// Stores geometry and contribution of a path sample for later visualization
/// </summary>
public class LoggedPath {
    /// <summary>
    /// The positions of the vertices along the path
    /// </summary>
    public List<Vector3> Vertices = new();

    /// <summary>
    /// Contribution of the path to the image
    /// </summary>
    public RgbColor Contribution = RgbColor.Black;

    /// <summary>
    /// For each vertex, some user defined number that identifies its "type" (e.g., light path or
    /// camera path)
    /// </summary>
    public List<int> UserTypes = new();

    public object UserData;

    /// <returns>A deep copy of this path</returns>
    public LoggedPath Copy() => new LoggedPath {
        Vertices = new(Vertices),
        Contribution = Contribution,
        UserTypes = new(UserTypes),
        UserData = UserData
    };
}

/// <summary>
/// Stores paths sampled by an integrator if they fulfill some user-defined filtering condition
/// </summary>
public class PathLogger {
    /// <summary>
    /// Filter function used to determine which paths to store
    /// </summary>
    /// <returns>True if the path should be stored</returns>
    public delegate bool FilterFn(LoggedPath path);

    /// <summary>
    /// The filter function that determines which paths should be stored
    /// </summary>
    public FilterFn Filter { get; init; }

    /// <summary>
    /// Creates a new logger that can store arbitrarily many paths per pixel
    /// </summary>
    public PathLogger(int imageWidth, int imageHeight) {
        pixelPaths = new List<LoggedPath>[imageWidth * imageHeight];
        for (int i = 0; i < pixelPaths.Length; ++i)
            pixelPaths[i] = new List<LoggedPath>();
        width = imageWidth;
        height = imageHeight;
    }

    /// <summary>
    /// Tracks the index of a path in the per-pixel lists
    /// </summary>
    public struct PathIndex {
        /// <summary>
        /// Index of the pixel
        /// </summary>
        public int Pixel { get; init; }

        /// <summary>
        /// Index of the path within the pixel
        /// </summary>
        public int Local { get; init; }
    }

    /// <summary>
    /// Starts a new path from a pixel
    /// </summary>
    /// <returns>Index of the new path</returns>
    public PathIndex StartNew(Pixel pixel) {
        var paths = pixelPaths[PixelToIndex(pixel)];
        int id;
        lock (paths) {
            id = paths.Count;
            paths.Add(new LoggedPath());
        }
        return new PathIndex { Pixel = PixelToIndex(pixel), Local = id };
    }

    /// <summary>
    /// Logs a splitting event, where one path is continued by multiple suffix paths
    /// </summary>
    /// <param name="original">The index of the path so far</param>
    /// <returns>Index of a new path with identical prefix</returns>
    public PathIndex Split(PathIndex original) {
        var paths = pixelPaths[original.Pixel];
        int id;
        lock (paths) {
            id = paths.Count;
            paths.Add(paths[original.Local].Copy());
        }
        return new PathIndex { Pixel = original.Pixel, Local = id };
    }

    /// <summary>
    /// Removes all paths that do not fulfill the filter conditions
    /// </summary>
    public void OnEndIteration() {
        Parallel.ForEach(pixelPaths, paths => {
            paths.RemoveAll(p => !Filter(p));
        });
    }

    /// <summary>
    /// Continues the path by adding a new vertex
    /// </summary>
    /// <param name="id">ID of the path</param>
    /// <param name="nextVertex">Position of the next vertex</param>
    /// <param name="type">User-defined type</param>
    public void Continue(PathIndex id, Vector3 nextVertex, int type) {
        pixelPaths[id.Pixel][id.Local].Vertices.Add(nextVertex);
        pixelPaths[id.Pixel][id.Local].UserTypes.Add(type);
    }

    /// <summary>
    /// Assigns a contribution to a path
    /// </summary>
    /// <param name="id">ID of an existing path</param>
    /// <param name="contrib">The contribution of the path</param>
    public void SetContrib(PathIndex id, RgbColor contrib) {
        pixelPaths[id.Pixel][id.Local].Contribution = contrib;
    }

    public void SetUserData(PathIndex id, object userData) {
        pixelPaths[id.Pixel][id.Local].UserData = userData;
    }

    int PixelToIndex(Pixel pixel) {
        int col = Math.Clamp(pixel.Col, 0, width - 1);
        int row = Math.Clamp(pixel.Row, 0, height - 1);
        return row * width + col;
    }

    /// <returns>All paths stored for the pixel with contribution higher than the minimum</returns>
    public List<LoggedPath> GetAllInPixel(int col, int row, RgbColor minContrib) {
        List<LoggedPath> result = new();
        var candidates = pixelPaths[row * width + col];
        foreach (var c in candidates) {
            if (c.Contribution.R < minContrib.R
             && c.Contribution.G < minContrib.G
             && c.Contribution.B < minContrib.B)
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