using SeeSharp.Blender;
using System.Text.Json;
public class DeletedHandler : IBlenderEventHandler
{
    public string EventType => "deleted";
    public event Action<string>? OnDeleted;

    public void Handle(JsonElement root)
    {
        OnDeleted?.Invoke(root.GetProperty("id").GetString());
    }
}