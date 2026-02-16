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
        flip = new FlipBook()
            .AddAll(layers.OrderBy(kv => kv.Item1))
            .SetToolVisibility(false)
            .SetCustomSizeCSS("width: 100%; height: 580px; resize: vertical; overflow: auto;");
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
    }

    async Task RenderMoreSamples()
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
                additionalSpp,
                renderWidth,
                renderHeight,
                maxDepth: renderMaxDepth,
                minDepth: renderMinDepth
            );
        });

        isRendering = false;
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

        if (selectedFile?.Integrator?.GetType() == null)
        {
            isStructureMismatch = true;
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
    }
}