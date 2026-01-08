using SeeSharp.Blender;
using System.Text.Json;
public class CreatedHandler : IBlenderEventHandler
{
    public string EventType => "created";

    public event Action<string>? OnCreated;

    public void Handle(JsonElement root)
    {
        OnCreated?.Invoke(root.GetProperty("id").GetString());
    }
}
