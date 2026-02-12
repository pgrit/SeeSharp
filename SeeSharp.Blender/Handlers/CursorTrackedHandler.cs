using SeeSharp.Blender;
using System.Text.Json;
public class BlenderCursorData
    {
        // [JsonPropertyName("object")]
        public string? Object { get; set; }
        // [JsonPropertyName("cursor_position")]
        public float[] Cursor_Position { get; set; } = Array.Empty<float>();
        // [JsonPropertyName("hit_position")]
        public float[]? Hit_Position { get; set; }
        // [JsonPropertyName("face_index")]
        public int? Face_Index { get; set; }
        // [JsonPropertyName("normal")]
        public float[]? Normal { get; set; }
    }

public class CursorTrackedHandler : IBlenderEventHandler
{
    public string EventType => "cursor_tracked";
    public event Action<BlenderCursorData>? OnCursorTracked;

    public void Handle(JsonElement root)
    {
        OnCursorTracked?.Invoke(new BlenderCursorData
                        {
                            Object = root.TryGetProperty("object", out var obj) ?
                                obj.GetString() : null,
                            Cursor_Position = 
                                JsonSerializer.Deserialize<float[]>(root.GetProperty("cursor_position")),
                            Hit_Position = root.TryGetProperty("hit_position", out var pos) ?
                                JsonSerializer.Deserialize<float[]>(pos.GetRawText()) : null,
                            Face_Index = root.TryGetProperty("face_index", out var face) ?
                                face.GetInt32() : null,
                            Normal = root.TryGetProperty("normal", out var norm) ?
                                JsonSerializer.Deserialize<float[]>(norm.GetRawText()) : null,
                        });
    }
}