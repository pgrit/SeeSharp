using Microsoft.AspNetCore.Components;
using SeeSharp.SceneManagement;

namespace SeeSharp.Blazor;

public partial class SceneSelector : ComponentBase
{
    [Parameter]
    public EventCallback<SceneDirectory> OnSceneLoaded { get; set; }


    IEnumerable<string> availableSceneNames
    {
        get
        {
            if (_availableSceneNames == null)
                _availableSceneNames = SceneRegistry.FindAvailableScenes().Order();
            return _availableSceneNames;
        }
    }
    IEnumerable<string> _availableSceneNames;

    AutocompleteInput sceneNameInput;

    SceneDirectory scene;
    bool loading = false;

    SceneDirectory Scene => scene;

    bool isSceneNameValid => sceneNameInput?.Valid == true;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var result = await ProtectedSessionStore.GetAsync<string>("lastScene");
            if (result.Success)
                sceneNameInput.Text = result.Value;
        }
    }

    async Task OnSceneNameUpdate(string newName)
    {
        await ProtectedSessionStore.SetAsync("lastScene", newName);
    }

    async Task LoadScene()
    {
        if (!isSceneNameValid || loading) return;
        loading = true;
        await Task.Run(() => scene = SceneRegistry.Find(sceneNameInput.Text));
        loading = false;
        await OnSceneLoaded.InvokeAsync(scene);
    }
}