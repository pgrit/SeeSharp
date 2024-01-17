using System.Text.Json;
using System.Text.Json.Nodes;

namespace SeeSharp.Tests.Core;

public class FrameBufferTests {
    [Fact]
    public void NaNShouldBeLogged() {
        FrameBuffer frameBuffer = new(512, 512, "nantest.exr");
        frameBuffer.StartIteration();
        frameBuffer.EndIteration();
        frameBuffer.StartIteration();
        frameBuffer.Splat(42, 69, RgbColor.White);

        frameBuffer.Splat(69, 42, new RgbColor(float.NaN, 1, 2));
        frameBuffer.Splat(13, 200, new RgbColor(0, 1, float.NegativeInfinity));

        frameBuffer.EndIteration();

        var warnings = frameBuffer.NaNWarnings;

        Assert.Equal(2, warnings[0].Iteration);
        Assert.Equal(69, warnings[0].Pixel.Col);
        Assert.Equal(42, warnings[0].Pixel.Row);

        Assert.Equal(2, warnings[1].Iteration);
        Assert.Equal(13, warnings[1].Pixel.Col);
        Assert.Equal(200, warnings[1].Pixel.Row);
    }

    [Fact]
    public void NaNShouldBeLoggedInJson() {
        FrameBuffer frameBuffer = new(512, 512, "nantest.exr");
        frameBuffer.StartIteration();
        frameBuffer.EndIteration();
        frameBuffer.StartIteration();
        frameBuffer.Splat(42, 69, RgbColor.White);

        frameBuffer.Splat(69, 42, new RgbColor(float.NaN, 1, 2));
        frameBuffer.Splat(13, 200, new RgbColor(0, 1, float.NegativeInfinity));

        frameBuffer.EndIteration();

        frameBuffer.WriteToFile();

        var json = JsonNode.Parse(File.ReadAllText("nantest.json"));
        var warnings = json["NaNWarnings"].Deserialize<FrameBuffer.NaNWarning[]>();

        Assert.Equal(2, warnings[0].Iteration);
        Assert.Equal(69, warnings[0].Pixel.Col);
        Assert.Equal(42, warnings[0].Pixel.Row);

        Assert.Equal(2, warnings[1].Iteration);
        Assert.Equal(13, warnings[1].Pixel.Col);
        Assert.Equal(200, warnings[1].Pixel.Row);
    }
}