using SeeSharp.SceneManagement;

namespace SeeSharp.Blazor;

public partial class SceneSelector : ComponentBase {
    [Parameter]
    public EventCallback<SceneDirectory> OnSceneLoaded { get; set; }


    IEnumerable<string> AvailableSceneNames {
        get {
            _availableSceneNames ??= SceneRegistry.FindAvailableScenes().Order();
            return _availableSceneNames;
        }
    }
    IEnumerable<string> _availableSceneNames;

    AutocompleteInput sceneNameInput;

    SceneDirectory scene;
    bool loading = false;

    bool IsSceneNameValid => sceneNameInput?.Valid == true;

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender) {
            try {
                var result = await ProtectedSessionStore.GetAsync<string>("lastScene");
                if (result.Success)
                    sceneNameInput.Text = result.Value;
            } catch { } // If we cannot access session storage, just ignore
        }
    }

    async Task OnSceneNameUpdate(string newName) {
        await ProtectedSessionStore.SetAsync("lastScene", newName);
    }

    async Task LoadScene() {
        if (!IsSceneNameValid || loading) return;
        loading = true;
        await Task.Run(() => scene = SceneRegistry.Find(sceneNameInput.Text));
        loading = false;
        await OnSceneLoaded.InvokeAsync(scene);
    }
}