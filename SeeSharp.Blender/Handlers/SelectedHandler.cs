using SeeSharp.Blender;
using System.Text.Json;
public class SelectedHandler : IBlenderEventHandler
{
    public string EventType => "selected";
    public event Action<string>? OnSelected;

    public void Handle(JsonElement root)
    {
        OnSelected?.Invoke(root.GetProperty("id").GetString());
    }
}