using System.Reflection;
using System.Text.Json.Nodes;
using static SeeSharp.SceneManagement.ReferenceCache;

namespace SeeSharp.ReferenceManager.Pages;

public partial class ReferenceRendering
{
    private SceneSelector sceneSelector;
    private IntegratorSelector integratorSelector;
    private SceneDirectory currentSceneFile;
    private FlipBook flip;

    private IEnumerable<ReferenceInfo> referenceFiles =>
        currentSceneFile?.References.AvailableReferences;
    private ReferenceInfo? selectedFile;

    private int renderWidth = 512;
    private int renderHeight = 512;
    private int renderMaxDepth = 5;
    private int renderMinDepth = 1;
    private int quickPreviewSpp = 1;
    private bool isRendering = false;
    private int additionalSpp = 32;
    private bool isStructureMismatch = false;
    private bool isVersionMismatch = false;
    private bool isVersionWarning = false;
    private HashSet<string> extraKeys = new();
    private HashSet<string> missingKeys = new();

    string CurrentVersion = typeof(Scene)
        .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        .InformationalVersion;

    private void OnSceneLoaded(SceneDirectory sceneDir)
    {
        currentSceneFile = sceneDir;
    }

    private void ResetConfig()
    {
        if (currentSceneFile == null)
            return;

        integratorSelector.CurrentIntegrator = currentSceneFile.References.ReferenceIntegrator;
    }

    private void SaveSceneConfig()
    {
        if (integratorSelector.CurrentIntegrator == null || currentSceneFile == null)
            return;

        currentSceneFile.References.ReferenceIntegrator = integratorSelector.CurrentIntegrator;
    }

    private void ApplyReferenceSettings()
    {
        if (selectedFile?.Integrator == null)
            return;

        integratorSelector.CurrentIntegrator = selectedFile.Value.Integrator;
    }

    private void SelectReference(ReferenceInfo file)
    {
        selectedFile = file;
        UpdatePreviewFromMemory(file.Layers.Select(i => (i.Key, i.Value)));
        CheckParamsMatch();
    }

    private async void UpdatePreviewFromMemory(IEnumerable<(string, Image)> layers)
    {
        // TODO is this necessary?
        // flip = null;
        // StateHasChanged();
        // await Task.Delay(1);

        flip = new FlipBook()
            .AddAll(layers)
            .SetToolVisibility(false)
            .SetCustomSizeCSS("width: 100%; height: 580px; resize: vertical; overflow: auto;");
        StateHasChanged();
    }

    async Task RenderQuickPreview()
    {
        if (currentSceneFile == null || integratorSelector.CurrentIntegrator == null)
            return;

        isRendering = true;
        selectedFile = null;
        flip = null;

        await InvokeAsync(StateHasChanged);

        // Copy the integrator so changing settings in the UI does not
        // interfere with the running rendering
        var curIntegrator = Integrator.Deserialize(
            integratorSelector.CurrentIntegrator.Serialize()
        );

        await Task.Run(async () =>
        {
            var renderScene = currentSceneFile.SceneLoader.Scene;

            int targetSpp = quickPreviewSpp < 1 ? 1 : quickPreviewSpp;
            curIntegrator.NumIterations = (uint)targetSpp;
            curIntegrator.MinDepth = renderMinDepth;
            curIntegrator.MaxDepth = renderMaxDepth;

            renderScene.FrameBuffer = new FrameBuffer(renderWidth, renderHeight, "");
            renderScene.Prepare();
            curIntegrator.Render(renderScene);

            UpdatePreviewFromMemory(renderScene.FrameBuffer.LayerImages);
        });

        isRendering = false;
        await InvokeAsync(StateHasChanged);
    }

    async Task RenderReference()
    {
        if (currentSceneFile == null || integratorSelector.CurrentIntegrator == null)
            return;

        isRendering = true;
        flip = null;
        await InvokeAsync(StateHasChanged);

        currentSceneFile.References.ReferenceIntegrator = integratorSelector.CurrentIntegrator;
        await Task.Run(async () =>
        {
            currentSceneFile.References.Get(
                renderWidth,
                renderHeight,
                allowRender: true,
                maxDepth: renderMaxDepth,
                minDepth: renderMinDepth
            );
        });

        isRendering = false;
        await InvokeAsync(StateHasChanged);
    }

    async Task RenderMoreSamples(bool isResume)
    {
        if (currentSceneFile == null || integratorSelector.CurrentIntegrator == null)
            return;

        isRendering = true;
        flip = null;
        await InvokeAsync(StateHasChanged);

        currentSceneFile.References.ReferenceIntegrator = integratorSelector.CurrentIntegrator;
        await Task.Run(async () =>
        {
            currentSceneFile.References.AddSamples(
                1000, // TODO determine actual sample count
                renderWidth,
                renderHeight,
                maxDepth: renderMaxDepth,
                minDepth: renderMinDepth
            );
        });

        isRendering = false;
        await InvokeAsync(StateHasChanged);

        // if (currentSceneFile == null || selectedFile == null || !File.Exists(selectedFile.FilePath))
        //     return;
        // if (integratorSelector.CurrentIntegrator == null)
        //     return;

        // isRendering = true;
        // await InvokeAsync(StateHasChanged);

        // await Task.Run(async () =>
        // {
        //     try
        //     {
        //         string filePath = selectedFile.FilePath;
        //         var oldImg = new RgbImage(filePath);
        //         int currentSpp = selectedFile.Spp;

        //         string folder = Path.GetDirectoryName(filePath);
        //         string baseNameNoSuffix = Path.GetFileNameWithoutExtension(filePath)
        //             .Replace("-partial", "");
        //         string jsonPath = Path.Combine(
        //             folder,
        //             baseNameNoSuffix + (filePath.Contains("-partial") ? "-partial.json" : ".json")
        //         );

        //         string thisStepStart = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        //         JsonNode rootNode = null;
        //         Integrator renderIntegrator = null;
        //         long totalPreviousMs = 0;
        //         int settingsTargetSpp = 0;

        //         if (File.Exists(jsonPath))
        //         {
        //             rootNode = JsonNode.Parse(File.ReadAllText(jsonPath));
        //             totalPreviousMs = (long)(rootNode?["RenderTime"]?.GetValue<double>() ?? 0);
        //             var settingsNode = rootNode?["Settings"];
        //             if (settingsNode != null)
        //             {
        //                 settingsTargetSpp =
        //                     settingsNode["TotalSpp"]?.GetValue<int>()
        //                     ?? settingsNode["NumIterations"]?.GetValue<int>()
        //                     ?? 0;
        //                 Type targetType = integratorSelector.GetIntegratorType(
        //                     rootNode["Name"]?.GetValue<string>()
        //                 );
        //                 if (targetType != null)
        //                 {
        //                     var options = new JsonSerializerOptions
        //                     {
        //                         IncludeFields = true,
        //                         PropertyNameCaseInsensitive = true,
        //                     };
        //                     renderIntegrator =
        //                         JsonSerializer.Deserialize(settingsNode, targetType, options)
        //                         as Integrator;
        //                 }
        //             }
        //         }

        //         int batchSpp = isResume ? (settingsTargetSpp - currentSpp) : additionalSpp;
        //         int finalTotalSpp = currentSpp + batchSpp;
        //         if (batchSpp <= 0)
        //             return;

        //         renderIntegrator.NumIterations = (uint)batchSpp;
        //         if (!isResume)
        //         {
        //             uint originalBaseSeed = renderIntegrator.BaseSeed;
        //             renderIntegrator.BaseSeed = originalBaseSeed + (uint)currentSpp;
        //         }

        //         string currentPartialPath = Path.Combine(folder, baseNameNoSuffix + "-partial.exr");
        //         var fbFlags =
        //             FrameBuffer.Flags.WriteContinously
        //             | FrameBuffer.Flags.WriteExponentially
        //             | FrameBuffer.Flags.IgnoreNanAndInf;

        //         scene.FrameBuffer = new FrameBuffer(
        //             oldImg.Width,
        //             oldImg.Height,
        //             currentPartialPath,
        //             fbFlags
        //         );
        //         scene.Prepare();

        //         var stopwatch = Stopwatch.StartNew();
        //         renderIntegrator.Render(scene);
        //         stopwatch.Stop();

        //         long thisStepMs = stopwatch.ElapsedMilliseconds;
        //         string thisStepWrite = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

        //         var newImg = scene.FrameBuffer.Image;
        //         var finalImg = new RgbImage(oldImg.Width, oldImg.Height);
        //         float wOld = (float)currentSpp / finalTotalSpp;
        //         float wNew = (float)batchSpp / finalTotalSpp;

        //         Parallel.For(
        //             0,
        //             finalImg.Height,
        //             row =>
        //             {
        //                 for (int col = 0; col < finalImg.Width; ++col)
        //                 {
        //                     finalImg.SetPixel(
        //                         col,
        //                         row,
        //                         oldImg.GetPixel(col, row) * wOld + newImg.GetPixel(col, row) * wNew
        //                     );
        //                 }
        //             }
        //         );

        //         if (rootNode == null)
        //             rootNode = new JsonObject();
        //         rootNode["RenderTime"] = totalPreviousMs + thisStepMs;
        //         rootNode["RenderWriteTime"] = thisStepWrite;
        //         rootNode["NumIterations"] = finalTotalSpp;

        //         var steps = rootNode["RenderSteps"]?.AsArray() ?? new JsonArray();
        //         steps.Add(
        //             new JsonObject
        //             {
        //                 ["Type"] = isResume ? "Resume" : "More",
        //                 ["DurationMs"] = thisStepMs,
        //                 ["StartTime"] = thisStepStart,
        //                 ["WriteTime"] = thisStepWrite,
        //             }
        //         );
        //         rootNode["RenderSteps"] = steps;

        //         if (!isResume && rootNode["Settings"] != null)
        //         {
        //             rootNode["Settings"]["TotalSpp"] = finalTotalSpp;
        //         }

        //         string finalSavePath = isResume
        //             ? Path.Combine(folder, baseNameNoSuffix + ".exr")
        //             : filePath;
        //         finalImg.WriteToFile(finalSavePath);
        //         File.WriteAllText(
        //             Path.ChangeExtension(finalSavePath, ".json"),
        //             rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
        //         );

        //         if (File.Exists(currentPartialPath))
        //             File.Delete(currentPartialPath);
        //         string partialJsonPath = Path.ChangeExtension(currentPartialPath, ".json");
        //         if (File.Exists(partialJsonPath))
        //             File.Delete(partialJsonPath);

        //         if (isResume && filePath != finalSavePath)
        //         {
        //             if (File.Exists(filePath))
        //                 File.Delete(filePath);
        //             string oldJ = Path.ChangeExtension(filePath, ".json");
        //             if (File.Exists(oldJ))
        //                 File.Delete(oldJ);
        //         }

        //         await InvokeAsync(() =>
        //         {
        //             referenceFiles = ReferenceUtils.ScanReferences(currentSceneFile).ToList();
        //             StateHasChanged();
        //         });
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine(ex.ToString());
        //     }
        // });

        // isRendering = false;
        // await InvokeAsync(StateHasChanged);
    }

    private void CheckParamsMatch()
    {
        isStructureMismatch = false;
        isVersionMismatch = false;
        isVersionWarning = false;
        extraKeys.Clear();
        missingKeys.Clear();

        if (selectedFile?.Metadata == null)
            return;

        (int Major, int Minor, int Patch) ParseSemVer(string ver)
        {
            if (string.IsNullOrEmpty(ver))
                return (0, 0, 0);
            int idx = ver.IndexOfAny(new[] { '+', '-' });
            if (idx >= 0)
                ver = ver.Substring(0, idx);

            var parts = ver.Split('.');
            int maj = parts.Length > 0 && int.TryParse(parts[0], out int m) ? m : 0;
            int min = parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : 0;
            int pat = parts.Length > 2 && int.TryParse(parts[2], out int p) ? p : 0;

            return (maj, min, pat);
        }

        string fileVerStr = selectedFile?.SeeSharpVersion;

        var cur = ParseSemVer(CurrentVersion);
        var file = ParseSemVer(fileVerStr);

        if (cur.Major != file.Major || cur.Minor != file.Minor)
            isVersionMismatch = true;
        else if (cur.Patch != file.Patch)
            isVersionWarning = true;

        if (selectedFile?.Integrator.GetType() == null)
        {
            isStructureMismatch = true;
            StateHasChanged();
            return;
        }

        var curJson = integratorSelector.CurrentIntegrator.Serialize();
        var fileJson = selectedFile.Value.Integrator.Serialize();

        var curNode = JsonNode.Parse(curJson)?.AsObject();
        var fileNode = JsonNode.Parse(fileJson)?.AsObject();

        if (curNode != null && fileNode != null)
        {
            var codeKeys = curNode.Select(k => k.Key).ToHashSet();
            var fileKeys = fileNode.Select(k => k.Key).ToHashSet();

            foreach (var key in fileKeys)
            {
                if (!codeKeys.Contains(key))
                    extraKeys.Add(key);

                // TODO check parameter values are equal
            }
            foreach (var key in codeKeys)
            {
                if (!fileKeys.Contains(key))
                    missingKeys.Add(key);
            }
            if (extraKeys.Count > 0 || missingKeys.Count > 0)
                isStructureMismatch = true;
        }

        StateHasChanged();
    }
}
