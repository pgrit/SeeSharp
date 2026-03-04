using System.Text.Json;
namespace SeeSharp.Blender;
public class BlenderCursorData {
    public string? Object { get; set; }
    public float[] CursorPosition { get; set; } = Array.Empty<float>();
    public float[]? HitPosition { get; set; }
    public int? FaceIndex { get; set; }
    public float[]? Normal { get; set; }
}

public class CursorTrackedHandler : IBlenderEventHandler {
    public string EventType => "cursor_tracked";
    public event Action<BlenderCursorData>? OnCursorTracked;

    public void Handle(JsonElement root) {
        OnCursorTracked?.Invoke(new BlenderCursorData {
            Object = root.TryGetProperty("object", out var obj) ?
                obj.GetString() : null,
            CursorPosition = 
                JsonSerializer.Deserialize<float[]>(root.GetProperty("cursor_position")),
            HitPosition = root.TryGetProperty("hit_position", out var pos) ?
                JsonSerializer.Deserialize<float[]>(pos.GetRawText()) : null,
            FaceIndex = root.TryGetProperty("face_index", out var face) ?
                face.GetInt32() : null,
            Normal = root.TryGetProperty("normal", out var norm) ?
                JsonSerializer.Deserialize<float[]>(norm.GetRawText()) : null,
        });
    }
}