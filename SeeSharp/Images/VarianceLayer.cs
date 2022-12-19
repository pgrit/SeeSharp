namespace SeeSharp.Images;

/// <summary>
/// Estimates the pixel variance in the rendered image.
/// "Pixel variance" is defined as the squared deviation of the pixel value from each iteration
/// from the mean pixel value across all iterations.
/// Note that this does not necessarily equal the variance of the underlying Monte Carlo estimator,
/// especially not if MIS is used. It is a lower-bound approximation of that.
/// </summary>
public class VarianceLayer : Layer {
    MonochromeImage momentImage;
    MonochromeImage meanImage;
    MonochromeImage bufferImage;

    /// <summary>
    /// Average variance over the entire image
    /// </summary>
    public float Average;

    /// <summary>
    /// Called once before the first rendering iteration
    /// </summary>
    /// <param name="width">The width of the frame buffer</param>
    /// <param name="height">The height of the frame buffer</param>
    public override void Init(int width, int height) {
        Image = new MonochromeImage(width, height);
        momentImage = new MonochromeImage(width, height);
        meanImage = new MonochromeImage(width, height);
        bufferImage = new MonochromeImage(width, height);
    }

    /// <summary>
    /// Splats a new pixel value to the current pixel value buffer
    /// </summary>
    public virtual void Splat(float x, float y, RgbColor value)
    => bufferImage.AtomicAdd((int)x, (int)y, value.Average);

    /// <summary>
    /// Normalizes and clears the internal buffers
    /// </summary>
    public override void OnStartIteration(int curIteration) {
        if (curIteration > 1) {
            momentImage.Scale((curIteration - 1.0f) / curIteration);
            meanImage.Scale((curIteration - 1.0f) / curIteration);
        }
        this.curIteration = curIteration;

        // Each iteration needs to store the final pixel value of that iteration
        bufferImage.Scale(0);
    }

    /// <summary>
    /// Computes the pixel variances and their average
    /// </summary>
    public override void OnEndIteration(int curIteration) {
        // Update the mean and moment based on the buffered image of the current iteration
        Parallel.For(0, momentImage.Height, row => {
            for (int col = 0; col < momentImage.Width; ++col) {
                float val = bufferImage.GetPixel(col, row);
                momentImage.AtomicAdd(col, row, val * val / curIteration);
                meanImage.AtomicAdd(col, row, val / curIteration);
            }
        });

        // Blur both buffers to get a more stable estimate.
        // TODO this could be done in-place by directly splatting in multiple pixels above
        MonochromeImage blurredMean = new(meanImage.Width, meanImage.Height);
        MonochromeImage blurredMoment = new(meanImage.Width, meanImage.Height);
        Filter.Box(meanImage, blurredMean, 1);
        Filter.Box(momentImage, blurredMoment, 1);

        // Compute the final variance and update the main image
        Average = 0;
        Parallel.For(0, momentImage.Height, (Action<int>)(row => {
            for (int col = 0; col < momentImage.Width; ++col) {
                float mean = blurredMean.GetPixel(col, row);
                float variance = blurredMoment.GetPixel(col, row) - mean * mean;
                variance /= (mean * mean + 0.001f);
                Image.SetPixelChannel(col, row, 0, variance);
                Atomic.AddFloat(ref Average, variance);
            }
        }));
        Average /= momentImage.Height * momentImage.Width;
    }
}