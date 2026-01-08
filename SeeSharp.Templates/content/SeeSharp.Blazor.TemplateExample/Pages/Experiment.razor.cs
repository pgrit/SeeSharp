namespace SeeSharp.Blazor.Template.Pages;
using Microsoft.JSInterop;

// Only here for the example to show some possibilities
struct ExampleImageGenerator {
    public Image rndImage(float strength, int width, int height, bool colored) {
        Image image = new Image(width, height, 3);
        RNG rng = new RNG();

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                if (colored) {
                    image.SetPixelChannel(x, y, 0, rng.NextFloat(0.5f - strength, 0.5f + strength));
                    image.SetPixelChannel(x, y, 1, rng.NextFloat(0.5f - strength, 0.5f + strength));
                    image.SetPixelChannel(x, y, 2, rng.NextFloat(0.5f - strength, 0.5f + strength));
                } else {
                    float value = rng.NextFloat(0.5f - strength, 0.5f + strength);

                    image.SetPixelChannel(x, y, 0, value);
                    image.SetPixelChannel(x, y, 1, value);
                    image.SetPixelChannel(x, y, 2, value);
                }

            }
        }

        return image;
    }
}

public partial class Experiment : BaseExperiment {
    // Define all flip books here
    public SimpleImageIO.FlipBook flip;
    public (SimpleImageIO.FlipBook, SimpleImageIO.FlipBook) compare;

    long renderTimePT, renderTimeVCM;

    //Methods
    PathTracer pathTracer;
    VertexConnectionAndMerging vcm;
    ExampleImageGenerator imgGen;

    RgbImage ptImage;
    RgbImage vcmImage;

    /// <summary>
    /// Initializes all flipbooks
    /// </summary>
    void InitFlipbooks() {
        // create new Flipbook w/o key
        flip = new FlipBook(FlipWidth, FlipHeight)
            .SetZoom(FlipBook.InitialZoom.FillWidth)
            .SetToneMapper(FlipBook.InitialTMO.Exposure(scene.RecommendedExposure))
            .SetToolVisibility(false);

        // create Flipbooks with keys
        compare = (
            new FlipBook(FlipWidth, FlipHeight)
                .SetZoom(FlipBook.InitialZoom.FillWidth)
                .SetToneMapper(FlipBook.InitialTMO.Exposure(scene.RecommendedExposure))
                .SetGroupName("compare")
                .SetToolVisibility(false)
                .SetKey("1,0"),
            new FlipBook(FlipWidth, FlipHeight)
                .SetZoom(FlipBook.InitialZoom.FillWidth)
                .SetToneMapper(FlipBook.InitialTMO.Exposure(scene.RecommendedExposure))
                .SetGroupName("compare")
                .SetToolVisibility(false)
                .SetKey("0,1")
        );
        registry.Add(compare.Item1.GetKey(), compare.Item1);
        registry.Add(compare.Item2.GetKey(), compare.Item2);
    }

    /// <summary>
    /// Sets all intial images of flipbooks with extra functions (ex: compare)
    /// </summary>
    void FlipBookSetBaseImages() {
        compare.Item1.Add($"PathTracer", ptImage);
        compare.Item2.Add($"VCM", vcmImage);
    }

    public override void RunExperiment() {
        InitFlipbooks();

        scene.FrameBuffer = new(Width, Height, "");
        scene.Prepare();

        pathTracer = new() {
            TotalSpp = NumSamples,
            MaxDepth = MaxDepth,
        };
        pathTracer.Render(scene);
        flip.Add($"PT", scene.FrameBuffer.Image);
        renderTimePT = scene.FrameBuffer.RenderTimeMs;
        ptImage = scene.FrameBuffer.Image;

        scene.FrameBuffer = new(Width, Height, "");
        vcm = new() {
            NumIterations = NumSamples,
            MaxDepth = MaxDepth,
        };
        vcm.Render(scene);
        flip.Add($"VCM", scene.FrameBuffer.Image);
        renderTimeVCM = scene.FrameBuffer.RenderTimeMs;
        vcmImage = scene.FrameBuffer.Image;

        FlipBookSetBaseImages();
    }

    /// <summary>
    /// Catches fired events and forward events to selected flipbooks
    /// </summary>
    /// <param name="fired">Fired type</param>
    public override void updateFlipbook(FiredType fired) {
        if (String.IsNullOrEmpty(state.currFlipKey))
            return;

        switch (state.currFlipKey) {
            case "1,0":
            case "0,1": {
                    updateCompare(fired);
                    break;
                }
            default:
                break;
        }
    }

    /// <summary>
    /// Example method that updates the flipbook pair.
    /// When Alt key is pressed, the image change to random noise images
    /// When Ctrl key is pressed, the colored and black/white images swap
    /// </summary>
    /// <param name="fired">Fired type</param>
    /// <returns></returns>
    async Task updateCompare(FiredType fired) {
        // Disable event types that you want to ignore
        if (fired == FiredType.Click || fired == FiredType.Move)
            return;

        FlipBook flipBook = registry[state.currFlipKey];
        // TODO: some iteration over all pairs with the same number ex: (1,0,0,...) -> (0,1,0,0,..)
        // this is a fast solution (and maybe good enough) for now -> flip the string to get the other flipbook
        FlipBook flipBookOther = registry[Reverse(state.currFlipKey)];

        Image updateImage = flipBook.GetImage(state.selectedIndex);
        Image updateImageOther = flipBookOther.GetImage(state.selectedIndex);

        if (state.actionKey1Pressed) {
            bool colored = true;

            if (state.actionKey2Pressed)
                colored = !colored;

            if (state.currIteration == 0) {
                updateImage = imgGen.rndImage(0.2f, Width, Height, !colored);
                updateImageOther = imgGen.rndImage(0.2f, Width, Height, colored);
            } else if (state.currIteration == 1) {
                updateImage = imgGen.rndImage(0.4f, Width, Height, !colored);
                updateImageOther = imgGen.rndImage(0.4f, Width, Height, colored);
            }

            FlipBook.GeneratedCode code = flipBook.UpdateImage(updateImage, state.selectedIndex);
            JS.InvokeVoidAsync("updateImage", code.Data);
            code = flipBookOther.UpdateImage(updateImageOther, state.selectedIndex);
            JS.InvokeVoidAsync("updateImage", code.Data);

            Console.WriteLine("Compare updated");
        } else {
            updateImage = ptImage;
            updateImageOther = vcmImage;

            FlipBook.GeneratedCode code = flipBook.UpdateImage(updateImage, state.selectedIndex);
            JS.InvokeVoidAsync("updateImage", code.Data);
            code = flipBookOther.UpdateImage(updateImageOther, state.selectedIndex);
            JS.InvokeVoidAsync("updateImage", code.Data);

            Console.WriteLine("Compare reset");
        }
    }
}