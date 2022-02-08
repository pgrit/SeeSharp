using System.Text.Json;

namespace SeeSharp.IO;

/// <summary>
/// Methods to deserialize common types in the scene description
/// </summary>
public static class JsonUtils {
    public static TextureRgb ReadColorOrTexture(JsonElement json, string path) {
        string type = json.GetProperty("type").GetString();
        if (type == "rgb") {
            var rgb = ReadRgbColor(json);
            return new TextureRgb(rgb);
        } else if (type == "image") {
            var texturePath = json.GetProperty("filename").GetString();
            texturePath = Path.Join(Path.GetDirectoryName(path), texturePath);
            return new TextureRgb(texturePath);
        } else {
            Logger.Log($"Invalid texture specification: {json}", Verbosity.Error);
            return new TextureRgb(new RgbColor(1, 0, 1));
        }
    }

    public static RgbColor ReadRgbColor(JsonElement json) {
        var vec = ReadVector(json.GetProperty("value"));
        return new RgbColor(vec.X, vec.Y, vec.Z);
    }

    public static Matrix4x4 ReadMatrix(JsonElement json) {
        if (json.GetArrayLength() != 9 && json.GetArrayLength() != 12 && json.GetArrayLength() != 16) {
            Logger.Log($"Invalid matrix: Number of entries {json.GetArrayLength()} is not allowed", Verbosity.Error);
            return Matrix4x4.Identity;
        }

        Matrix4x4 m = Matrix4x4.Identity;

        // 3x3
        m.M11 = json[0].GetSingle();
        m.M12 = json[1].GetSingle();
        m.M13 = json[2].GetSingle();

        m.M21 = json[4].GetSingle();
        m.M22 = json[5].GetSingle();
        m.M23 = json[6].GetSingle();

        m.M31 = json[8].GetSingle();
        m.M32 = json[9].GetSingle();
        m.M33 = json[10].GetSingle();

        // 3x4
        if (json.GetArrayLength() >= 12) {
            m.M14 = json[3].GetSingle();
            m.M24 = json[7].GetSingle();
            m.M34 = json[11].GetSingle();
        }

        // 4x4
        if (json.GetArrayLength() == 16) {
            m.M41 = json[12].GetSingle();
            m.M42 = json[13].GetSingle();
            m.M43 = json[14].GetSingle();
            m.M44 = json[15].GetSingle();
        }

        return m;
    }

    public static Vector3 ReadVector(JsonElement json) {
        return new Vector3(
            json[0].GetSingle(),
            json[1].GetSingle(),
            json[2].GetSingle());
    }
}
