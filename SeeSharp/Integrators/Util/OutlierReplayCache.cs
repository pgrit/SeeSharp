namespace SeeSharp.Integrators.Util;

public class OutlierReplayCache<TGlobal, TLocal> {
    public struct PathReplayInfo {
        public RgbColor Weight;
        public TLocal LocalReplayInfo;
    }

    public void Notify(in Pixel pixel, in PathReplayInfo info) {
        float w = info.Weight.Average;

        if (!float.IsFinite(w)) {
            // TODO log into a separate NaN / Inf cache instead
            return;
        }

        var q = pixelHeaps[pixel.Row * width + pixel.Col];
        lock (q) {
            if (q.Count < nMax) q.Enqueue(info, w);
            else q.EnqueueDequeue(info, w);
        }
    }

    public OutlierReplayCache(TGlobal globalReplayInfo, int width, int height, int n) {
        this.width = width;
        nMax = n;
        GlobalReplayInfo = globalReplayInfo;

        pixelHeaps = new PriorityQueue<PathReplayInfo, float>[width * height];
        for (int i = 0; i < width * height; ++i)
            pixelHeaps[i] = new(n + 1);
    }

    public PriorityQueue<PathReplayInfo, float> GetPixelOutlier(in Pixel pixel)
    => pixelHeaps[pixel.Row * width + pixel.Col];

    PriorityQueue<PathReplayInfo, float>[] pixelHeaps;
    public readonly TGlobal GlobalReplayInfo;
    int width, nMax;
}