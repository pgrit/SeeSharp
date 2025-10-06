using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SeeSharp.Blazor;

namespace SeeSharp.Blazor.Template.Pages;

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

struct ListenerState {
    public ListenerState() { }

    /// <summary>
    /// The index of the selected image of the current selected flipbook (selected by clicking on it)
    /// </summary>
    public int selectedIndex = 0;

    /// <summary>
    /// Number between 0 and NumSamples. Can be used if data is stored from different iterations
    /// </summary>
    public int currIteration = 0;

    public bool altKeyPressed = false;
    public bool ctrlKeyPressed = false;
    public int currX = 0;
    public int currY = 0;

    /// <summary>
    /// The key of the current flipbook in string form and concatenated with ','
    /// </summary>
    public string currFlipKey = "";
}

/// <summary>
/// Differences between event type so update methods for flipbooks can ignore events
/// </summary>
enum FiredType {
    Click = 0,
    Move = 1,
    Wheel = 2,
    KeyDown = 4,
    KeyUp = 8,
}

public partial class Experiment : ComponentBase {
    const int Width = 1280;
    const int Height = 720;
    const int FlipWidth = 660;
    const int FlipHeight = 580;
    const int MaxDepth = 10;

    int NumSamples = 2;

    long renderTimePT, renderTimeVCM;

    //Methods
    PathTracer pathTracer;
    VertexConnectionAndMerging vcm;
    ExampleImageGenerator imgGen;

    RgbImage ptImage;
    RgbImage vcmImage;

    /// <summary>
    /// Registers all Flipbooks
    /// Key will be the stringified key of a Flipbook which is set by Flipbook.SetKey
    /// A Flipbook key is an array of ints
    /// </summary>
    Dictionary<string, FlipBook> registry = new Dictionary<string, FlipBook>();
    ListenerState state = new ListenerState();
    public static string Reverse(string s) {
        char[] charArray = s.ToCharArray();
        Array.Reverse(charArray);
        return new string(charArray);
    }

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
                .SetKey([1, 0]),
            new FlipBook(FlipWidth, FlipHeight)
                .SetZoom(FlipBook.InitialZoom.FillWidth)
                .SetToneMapper(FlipBook.InitialTMO.Exposure(scene.RecommendedExposure))
                .SetGroupName("compare")
                .SetToolVisibility(false)
                .SetKey([0, 1])
        );
        registry.Add(compare.Item1.GetKeyAsString(), compare.Item1);
        registry.Add(compare.Item2.GetKeyAsString(), compare.Item2);
    }

    /// <summary>
    /// Sets all intial images of flipbooks with extra functions (ex: compare)
    /// </summary>
    void FlipBookSetBaseImages() {
        compare.Item1.Add($"PathTracer", ptImage);
        compare.Item2.Add($"VCM", vcmImage);
    }

    void RunExperiment() {
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

    // SurfacePoint? selected;
    // void OnFlipClick(FlipViewer.OnClickEventArgs args) {
    //     if (args.CtrlKey) {
    //         selected = scene.RayCast(new(args.X, args.Y));
    //     }
    // }

    /// <summary>
    /// Is fired when clicked on an image in the flipbook
    /// </summary>
    /// <param name="args">ListenerState from HMTL side</param>
    void OnFlipClick(FlipViewer.OnClickEventArgs args) {
        updateFlipbook(FiredType.Click);
    }

    /// <summary>
    /// Is fired when the mouse wheel state changes over an image. 
    /// This gets only called when the alt key is pressed (from HTML side)
    /// </summary>
    /// <param name="args">ListenerState from HMTL side.</param>
    void OnFlipWheel(FlipViewer.OnClickEventArgs args) {
        if (state.altKeyPressed) {
            // scrolled up
            if (args.deltaY < 0) {
                if (state.currIteration < NumSamples - 1) {
                    state.currIteration++;
                    updateFlipbook(FiredType.Wheel);
                }
            }
            // scrolled down
            if (args.deltaY >= 0) {
                if (state.currIteration > 0) {
                    state.currIteration--;
                    updateFlipbook(FiredType.Wheel);
                }
            }
        }
    }

    /// <summary>
    /// Is fired when mouse position changes over the selected flipbook
    /// </summary>
    /// <param name="args">ListenerState from HMTL side.</param>
    void OnFlipMouseOver(FlipViewer.OnClickEventArgs args) {
        if (state.currX == args.X && state.currY == args.Y)
            return;

        if (args.X >= 0 && args.X <= Width - 1)
                state.currX = args.X;
        if (args.Y >= 0 && args.Y <= Height - 1)
            state.currY = args.Y;

        updateFlipbook(FiredType.Move);
    }

    /// <summary>
    /// Is fired when key is pressed or released
    /// </summary>
    /// <param name="args">ListenerState from HMTL side.</param>
    void OnFlipKey(FlipViewer.OnClickEventArgs args) {
        if (args.key == "Alt")
            state.altKeyPressed = args.isPressedDown;

        if (args.key == "Control")
            state.ctrlKeyPressed = args.isPressedDown;

        state.currFlipKey = args.registryKey;
        state.selectedIndex = args.selectedIndex;

        if(args.key == "Alt" && !args.isPressedDown)
            state.currIteration = 0;

        if (args.isPressedDown)
            updateFlipbook(FiredType.KeyDown);
        else
            updateFlipbook(FiredType.KeyUp);
    }

    /// <summary>
    /// Catches fired events and forward events to selected flipbooks
    /// </summary>
    /// <param name="fired">Fired type</param>
    void updateFlipbook(FiredType fired) {
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

        (string Name, Image Image, FlipBook.DataType TargetType, FlipBook.InitialTMO TMOOverride) updateImage = flipBook.GetImage(state.selectedIndex);
        (string Name, Image Image, FlipBook.DataType TargetType, FlipBook.InitialTMO TMOOverride) updateImageOther = flipBookOther.GetImage(state.selectedIndex);

        if (state.altKeyPressed) {
            bool colored = true;

            if (state.ctrlKeyPressed)
                colored = !colored;

            if (state.currIteration == 0) {
                updateImage.Image = imgGen.rndImage(0.2f, Width, Height, !colored);
                updateImageOther.Image = imgGen.rndImage(0.2f, Width, Height, colored);
            } else if (state.currIteration == 1) {
                updateImage.Image = imgGen.rndImage(0.4f, Width, Height, !colored);
                updateImageOther.Image = imgGen.rndImage(0.4f, Width, Height, colored);
            }

            FlipBook.GeneratedCode code = flipBook.UpdateImage(updateImage, state.selectedIndex);
            JS.InvokeVoidAsync("updateImage", code.Data);
            code = flipBookOther.UpdateImage(updateImageOther, state.selectedIndex);
            JS.InvokeVoidAsync("updateImage", code.Data);

            Console.WriteLine("Cell updated");
        } else {
            updateImage.Image = ptImage;
            updateImageOther.Image = vcmImage;

            FlipBook.GeneratedCode code = flipBook.UpdateImage(updateImage, state.selectedIndex);
            JS.InvokeVoidAsync("updateImage", code.Data);
            code = flipBookOther.UpdateImage(updateImageOther, state.selectedIndex);
            JS.InvokeVoidAsync("updateImage", code.Data);

            Console.WriteLine("Compare reset");
        }
    }

    async Task OnDownloadClick() {
        HtmlReport report = new();
        report.AddMarkdown("""
        # Example experiment
        $$ L_\mathrm{o} = \int_\Omega L_\mathrm{i} f_\mathrm{r} |\cos\theta_\mathrm{i}| \, d\omega_\mathrm{i} $$
        """);
        report.AddFlipBook(flip);
        await SeeSharp.Blazor.Scripts.DownloadAsFile(JS, "report.html", report.ToString());
    }
}