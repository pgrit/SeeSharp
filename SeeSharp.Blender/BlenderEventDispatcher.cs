using System.Text.Json;

namespace SeeSharp.Blender
{
    public interface IBlenderEventHandler
{
    string EventType { get; }
    void Handle(JsonElement root);
}

public class BlenderEventDispatcher
{
    private readonly Dictionary<string, IBlenderEventHandler> _handlers;

    public BlenderEventDispatcher(IEnumerable<IBlenderEventHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.EventType);
    }

    public void Register(IBlenderEventHandler handler)
    {
        _handlers.Add(handler.EventType, handler);
    }

    public void Unregister(IBlenderEventHandler handler)
    {
        _handlers.Remove(handler.EventType);
    }

    public void Dispatch(JsonElement root)
    {
        if (!root.TryGetProperty("event", out var evtProp))
            return;

        var evt = evtProp.GetString();
        if (evt == null)
            return;

        if (_handlers.TryGetValue(evt, out var handler))
            handler.Handle(root);
        else
            Console.WriteLine($"⚠️ Unknown Blender event: {evt}");
    }
}
    
}