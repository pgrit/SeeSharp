using Microsoft.AspNetCore.Components;

namespace ExperimentConfigTest.Pages;

public partial class Experiment : ComponentBase
{
    const int Width = 1280;
    const int Height = 720;

    SurfacePoint? selected;

    void OnFlipClick(FlipViewer.OnEventArgs args)
    {
        if (args.Control)
        {
            RNG rng = new(1241512);
            var ray = scene.Camera.GenerateRay(new Vector2(args.MouseX + 0.5f, args.MouseY + 0.5f), ref rng).Ray;
            selected = (SurfacePoint)scene.Raytracer.Trace(ray);

            SurfaceShader shader = new(selected.Value, -ray.Direction, false);
            var s = shader.Sample(rng.NextFloat(), rng.NextFloat2D());
            Console.WriteLine(s);
        }
    }

    async Task OnDownloadClick()
    {
        HtmlReport report = new();
        report.AddMarkdown("""
        # Example experiment
        $$ L_\mathrm{o} = \int_\Omega L_\mathrm{i} f_\mathrm{r} |\cos\theta_\mathrm{i}| \, d\omega_\mathrm{i} $$
        """);
        report.AddFlipBook(flip);
        await SeeSharp.Blazor.Scripts.DownloadAsFile(JS, "report.html", report.ToString());
    }

    void RunSingleIntegrator(Integrator integrator, string? name = null)
    {
        if (flip == null) {
            flip = new FlipBook(660, 580)
                .SetZoom(FlipBook.InitialZoom.FillWidth)
                .SetToneMapper(FlipBook.InitialTMO.Exposure(scene.RecommendedExposure))
                .SetToolVisibility(false);
        }

        scene.FrameBuffer = new(Width, Height, "");
        scene.Prepare();
        integrator.Render(scene);
        
        string displayName = name ?? IntegratorUtils.FormatClassName(integrator.GetType());
        flip.Remove(displayName);
        flip.Add(displayName, scene.FrameBuffer.Image);
    }
}