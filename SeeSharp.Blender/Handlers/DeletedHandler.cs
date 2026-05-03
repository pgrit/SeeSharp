using System.Text.Json;
namespace SeeSharp.Blender;
public class DeletedHandler : IBlenderEventHandler {
    public string EventType => "deleted";
    public event Action<string>? OnDeleted;

    public void Handle(JsonElement root) {
        OnDeleted?.Invoke(root.GetProperty("id").GetString());
    }
}