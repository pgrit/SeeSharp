using System.Text.Json;
namespace SeeSharp.Blender;
public class CreatedHandler : IBlenderEventHandler {
    public string EventType => "created";

    public event Action<string>? OnCreated;

    public void Handle(JsonElement root) {
        OnCreated?.Invoke(root.GetProperty("id").GetString());
    }
}
