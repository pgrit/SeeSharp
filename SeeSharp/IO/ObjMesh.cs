using System.Text.RegularExpressions;

using MaterialLib = System.Collections.Generic.Dictionary<string, SeeSharp.IO.ObjMesh.Material>;

namespace SeeSharp.IO;

/// <summary>
/// Represents a wavefront .obj mesh parsed from a file
/// </summary>
public class ObjMesh {
    /// <summary>A reference to a vertex/normal/texture coord. of the model.</summary>
    public class Index {
        /// <summary> Vertex, normal and texture indices(0 means not present) </summary>
        public int VertexIndex, NormalIndex, TextureIndex;

        /// <summary>
        /// Computes a hash for the index by combining hashes of the three integer values
        /// </summary>
        public override int GetHashCode() {
            uint h = 0, g;

            h = (h << 4) + (uint)this.VertexIndex;
            g = h & 0xF0000000;
            h = g != 0 ? (h ^ (g >> 24)) : h;
            h &= ~g;

            h = (h << 4) + (uint)this.TextureIndex;
            g = h & 0xF0000000;
            h = g != 0 ? (h ^ (g >> 24)) : h;
            h &= ~g;

            h = (h << 4) + (uint)this.NormalIndex;
            g = h & 0xF0000000;
            h = g != 0 ? (h ^ (g >> 24)) : h;
            h &= ~g;

            return (int)h;
        }

        /// <returns>True if the other object is an Index with all values equal</returns>
        public override bool Equals(object other) {
            if (other is not Index b) return false;

            return VertexIndex == b.VertexIndex
                && NormalIndex == b.NormalIndex
                && TextureIndex == b.TextureIndex;
        }
    }

    /// <summary>
    /// A face with arbitrarily many vertices, given by a list of indices, and a material index
    /// </summary>
    public class Face {
        /// <summary> Indices of the face </summary>
        public List<Index> Indices = new();

        /// <summary> Index into the material names of the model </summary>
        public int Material;
    }

    /// <summary>A group of faces in the model.</summary>
    public class Group {
        /// <summary>
        /// Name of the group as it is defined in the obj
        /// </summary>
        public string Name;

        /// <summary>
        /// All faces in the group
        /// </summary>
        public List<Face> Faces = new();

        /// <summary>
        /// Creates a new group with the given name
        /// </summary>
        public Group(string name) => Name = name;
    }

    /// <summary>A object in the model, made of several groups.</summary>
    public class Object {
        /// <summary>
        /// Name of the object as it is defined in the .obj
        /// </summary>
        public string Name;

        /// <summary>
        /// List of all groups in the object
        /// </summary>
        public List<Group> Groups = new();

        /// <summary>
        /// Creates a new object with the given name
        /// </summary>
        public Object(string name) => Name = name;
    }

    /// <summary>
    /// Material data parsed from an associated .mtl file
    /// </summary>
    public class Material {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public RgbColor ambient;
        public RgbColor diffuse;
        public RgbColor specular;
        public RgbColor emission;
        public float specularIndex;
        public float indexOfRefraction;
        public RgbColor transmittance;
        public float transparency;
        public float dissolveFactor;
        public int illuminationModel;
        public string ambientTexture;
        public string diffuseTexture;
        public string specularTexture;
        public string emittingTexture;
        public string bumpMap;
        public string dissolveTexture;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }

    /// <summary>
    /// Represents an entire parsed .obj file
    /// </summary>
    public class File {
        /// <summary>
        /// All objects in the file
        /// </summary>
        public List<Object> Objects = new();

        /// <summary>
        /// All vertices in the entire file
        /// </summary>
        public List<Vector3> Vertices = new();

        /// <summary>
        /// Shading normals of all vertices
        /// </summary>
        public List<Vector3> Normals = new();

        /// <summary>
        /// Texture coordinates of all vertices
        /// </summary>
        public List<Vector2> Texcoords = new();

        /// <summary>
        /// Names of all materials in the file
        /// </summary>
        public List<string> Materials = new();

        /// <summary>
        /// Names of all .mtl files associated to this .obj
        /// </summary>
        public List<string> MtlFiles = new();

        /// <summary>
        /// Look-up table for parsed .mtl data for each material name
        /// </summary>
        public MaterialLib MaterialLib = new();
    }

    /// <summary>
    /// Parsed file contents
    /// </summary>
    public File Contents = new();

    /// <summary>
    /// Directory containing the .obj. Required to find textures with relative paths
    /// </summary>
    public string BasePath;

    /// <summary>
    /// List of potential errors encountered during parsing
    /// </summary>
    public List<string> Errors;

    /// <summary>
    /// Loads a wavefront .obj file
    /// </summary>
    /// <param name="filename">Path to an existing .obj file</param>
    public static ObjMesh FromFile(string filename) {
        ObjMesh mesh = new() {
            Errors = new List<string>()
        };

        // Textures and materials will be relative to the .obj
        string full = Path.GetFullPath(filename);
        mesh.BasePath = Path.GetDirectoryName(full);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        Logger.Log($"Parsing {filename}...", Verbosity.Debug);
        // Parse the .obj itself
        using (var file = new StreamReader(filename))
            mesh.Errors.AddRange(mesh.ParseObjFile(file));
        watch.Stop();
        Logger.Log($"Done parsing .obj after {watch.ElapsedMilliseconds}ms.", Verbosity.Debug);

        // Parse all linked .mtl files
        foreach (string mtlFilename in mesh.Contents.MtlFiles) {
            // the mtl files are expected to be relative to the .obj itself
            string mtlPath = Path.Join(mesh.BasePath, mtlFilename);

            try {
                using var file = new StreamReader(mtlPath);
                mesh.Errors.AddRange(mesh.ParseMtlFile(file));
            } catch (FileNotFoundException) {
                Logger.Log($".mtl not found: {mtlPath}", Verbosity.Debug);
            }
        }

        return mesh;
    }

    private List<string> ParseObjFile(StreamReader stream) {
        // Add an empty object to the scene
        int cur_object = 0;
        Contents.Objects.Add(new Object(""));

        // Add an empty group to this object
        int cur_group = 0;
        Contents.Objects[0].Groups.Add(new Group(""));

        // Add an empty material to the scene
        int cur_mtl = 0;
        Contents.Materials.Add("");

        // Add dummy vertex, normal, and texcoord
        Contents.Vertices.Add(Vector3.Zero);
        Contents.Normals.Add(Vector3.Zero);
        Contents.Texcoords.Add(Vector2.Zero);

        List<string> errors = new();
        int cur_line = 0;
        string line;

        Vector3 ParseVector3() {
            Vector3 v = new();
            var matches = regexFloat.Matches(line);
            if (matches.Count != 3)
                errors.Add($"Invalid vector (line {cur_line}).");
            v.X = float.Parse(matches[0].Value);
            v.Y = float.Parse(matches[1].Value);
            v.Z = float.Parse(matches[2].Value);
            return v;
        }

        Vector2 ParseVector2() {
            Vector2 v = new();
            var matches = regexFloat.Matches(line);
            if (matches.Count != 2) {
                errors.Add($"Invalid vector (line {cur_line}) '{line}'. Replaced by (0,0).");
                return Vector2.Zero;
            }
            v.X = float.Parse(matches[0].Value);
            v.Y = float.Parse(matches[1].Value);
            return v;
        }

        List<Index> ReadFace() {
            var matches = regexIndex.Matches(line);

            var indices = new List<Index>(matches.Count);
            for (int i = 0; i < matches.Count; ++i) {
                if (matches[i].Groups[1].Value == "") // vertex index is mandatory
                    errors.Add($"Invalid face (line {cur_line}).");

                Index idx = new() {
                    VertexIndex = int.Parse(matches[i].Groups[1].Value)
                };
                if (matches[i].Groups[2].Value != "") // texture is optional
                    idx.TextureIndex = int.Parse(matches[i].Groups[2].Value);
                if (matches[i].Groups[3].Value != "") // normal is optional
                    idx.NormalIndex = int.Parse(matches[i].Groups[3].Value);

                indices.Add(idx);
            }

            if (matches.Count == 0)
                errors.Add($"Invalid face (line {cur_line}).");

            return indices;
        }

        while ((line = stream.ReadLine()) != null) {
            cur_line++;

            // Strip white space
            line = line.Trim();

            // Skip comments and empty lines
            if (line.Length == 0 || line[0] == '#')
                continue;

            // Test each command in turn, the most frequent first
            if (line[0] == 'v') {
                switch (line[1]) {
                    case ' ':
                    case '\t': // vertices
                        Contents.Vertices.Add(ParseVector3());
                        break;
                    case 'n': // normals
                        Contents.Normals.Add(ParseVector3());
                        break;
                    case 't': // uvs
                        Contents.Texcoords.Add(ParseVector2());
                        break;
                    default:
                        errors.Add($"Invalid vertex (line {cur_line}).");
                        break;
                }
            } else if (line[0] == 'f' && char.IsWhiteSpace(line[1])) {
                Face f = new() {
                    Material = cur_mtl,
                    Indices = ReadFace()
                };

                if (f.Indices.Count < 3) {
                    errors.Add($"Invalid face (line {cur_line}).");
                } else {
                    // Convert relative indices to absolute
                    for (int i = 0; i < f.Indices.Count; i++) {
                        f.Indices[i].VertexIndex = (f.Indices[i].VertexIndex < 0) ? Contents.Vertices.Count + f.Indices[i].VertexIndex : f.Indices[i].VertexIndex;
                        f.Indices[i].TextureIndex = (f.Indices[i].TextureIndex < 0) ? Contents.Texcoords.Count + f.Indices[i].TextureIndex : f.Indices[i].TextureIndex;
                        f.Indices[i].NormalIndex = (f.Indices[i].NormalIndex < 0) ? Contents.Normals.Count + f.Indices[i].NormalIndex : f.Indices[i].NormalIndex;
                    }

                    // Check if the indices are valid or not
                    bool valid = true;
                    for (int i = 0; i < f.Indices.Count; i++) {
                        if (f.Indices[i].VertexIndex <= 0 || f.Indices[i].TextureIndex < 0 || f.Indices[i].NormalIndex < 0) {
                            valid = false;
                            break;
                        }
                    }

                    if (valid) {
                        Contents.Objects[cur_object].Groups[cur_group].Faces.Add(f);
                    } else {
                        errors.Add($"Invalid indices in face definition (line {cur_line} ).");
                    }
                }
            } else if (line[0] == 'g' && char.IsWhiteSpace(line[1])) {
                string groupName = line[2..].Trim();
                Contents.Objects[cur_object].Groups.Add(new Group(groupName));
                cur_group++;
            } else if (line[0] == 'o' && char.IsWhiteSpace(line[1])) {
                string objectName = line[2..].Trim();
                Contents.Objects.Add(new Object(objectName));
                cur_object++;
                Contents.Objects[cur_object].Groups.Add(new Group(""));
                cur_group = 0;
            } else if (line.StartsWith("usemtl") && char.IsWhiteSpace(line[6])) {
                line = line[6..];
                string mtl_name = line.Trim();

                cur_mtl = Contents.Materials.FindIndex(x => x == mtl_name);
                if (cur_mtl < 0) {
                    Contents.Materials.Add(mtl_name);
                    cur_mtl = Contents.Materials.Count - 1;
                }
            } else if (line.StartsWith("mtllib") && char.IsWhiteSpace(line[6])) {
                line = line[6..];
                string lib_name = line.Trim();
                Contents.MtlFiles.Add(lib_name);
            } else if (line[0] == 's' && char.IsWhiteSpace(line[1])) {
                // Ignore smooth commands
            } else {
                errors.Add($"Unknown command '{line}' (line {cur_line}).");
            }
        }

        return errors;
    }

    // Common regular expressions for extracting values
    readonly Regex regexFloat = new(@"([+-]?([0-9]+([.][0-9]*)?|[.][0-9]+))", RegexOptions.Compiled);
    readonly Regex regexIndex = new(@"([+-]?[0-9]+)/?([+-]?[0-9]+)?/?([+-]?[0-9]+)?", RegexOptions.Compiled);

    private List<string> ParseMtlFile(StreamReader stream) {
        var errors = new List<string>();
        int cur_line = 0;
        string line;

        string mtl_name = "";
        Material current_material() {
            return Contents.MaterialLib[mtl_name];
        };

        RgbColor ParseRGB() {
            RgbColor v = new();
            var matches = regexFloat.Matches(line);
            if (matches.Count != 3)
                errors.Add($"Invalid vector (line {cur_line}).");
            v.R = float.Parse(matches[0].Value);
            v.G = float.Parse(matches[1].Value);
            v.B = float.Parse(matches[2].Value);
            return v;
        }

        while ((line = stream.ReadLine()) != null) {
            cur_line++;

            // Strip spaces
            line = line.Trim();

            // Skip comments and empty lines
            if (line == "" || line[0] == '#')
                continue;

            if (line.StartsWith("newmtl") && char.IsWhiteSpace(line[6])) {
                mtl_name = line[6..].Trim();
                if (!Contents.MaterialLib.TryAdd(mtl_name, new Material()))
                    errors.Add($"Material redefinition for '{mtl_name}' (line {cur_line}).");
            } else if (line[0] == 'K') {
                if (line[1] == 'a' && char.IsWhiteSpace(line[2])) {
                    var mat = current_material();
                    mat.ambient = ParseRGB();
                } else if (line[1] == 'd' && char.IsWhiteSpace(line[2])) {
                    var mat = current_material();
                    mat.diffuse = ParseRGB();
                } else if (line[1] == 's' && char.IsWhiteSpace(line[2])) {
                    var mat = current_material();
                    mat.specular = ParseRGB();
                } else if (line[1] == 'e' && char.IsWhiteSpace(line[2])) {
                    var mat = current_material();
                    mat.emission = ParseRGB();
                } else
                    errors.Add($"Invalid command '{line}' (line {cur_line}).");
            } else if (line[0] == 'N') {
                if (line[1] == 's' && char.IsWhiteSpace(line[2])) {
                    var mat = current_material();
                    mat.specularIndex = float.Parse(line[3..]);
                } else if (line[1] == 'i' && char.IsWhiteSpace(line[2])) {
                    var mat = current_material();
                    mat.indexOfRefraction = float.Parse(line[3..]);
                } else
                    errors.Add($"Invalid command '{line}' (line {cur_line}).");
            } else if (line[0] == 'T') {
                if (line[1] == 'f' && char.IsWhiteSpace(line[2])) {
                    var mat = current_material();
                    mat.transmittance = ParseRGB();
                } else if (line[1] == 'r' && char.IsWhiteSpace(line[2])) {
                    var mat = current_material();
                    mat.transparency = float.Parse(line[3..]);
                } else
                    errors.Add($"Invalid command '{line}' (line {cur_line}).");
            } else if (line[0] == 'd' && char.IsWhiteSpace(line[1])) {
                var mat = current_material();
                mat.dissolveFactor = float.Parse(line[2..]);
            } else if (line.StartsWith("illum") && char.IsWhiteSpace(line[5])) {
                var mat = current_material();
                mat.illuminationModel = int.Parse(line[6..]);
            } else if (line.StartsWith("map_Ka") && char.IsWhiteSpace(line[6])) {
                var mat = current_material();
                mat.ambientTexture = line[7..].Trim();
            } else if (line.StartsWith("map_Kd") && char.IsWhiteSpace(line[6])) {
                var mat = current_material();
                mat.diffuseTexture = line[7..].Trim();
            } else if (line.StartsWith("map_Ks") && char.IsWhiteSpace(line[6])) {
                var mat = current_material();
                mat.specularTexture = line[7..].Trim();
            } else if (line.StartsWith("map_Ke") && char.IsWhiteSpace(line[6])) {
                var mat = current_material();
                mat.emittingTexture = line[7..].Trim();
            } else if (line.StartsWith("map_bump") && char.IsWhiteSpace(line[8])) {
                var mat = current_material();
                mat.bumpMap = line[9..].Trim();
            } else if (line.StartsWith("bump") && char.IsWhiteSpace(line[4])) {
                var mat = current_material();
                mat.bumpMap = line[5..].Trim();
            } else if (line.StartsWith("map_d") && char.IsWhiteSpace(line[5])) {
                var mat = current_material();
                mat.dissolveTexture = line[6..].Trim();
            } else
                errors.Add("Unknown command '{line}' (line {cur_line}).");
        }

        return errors;
    }
}
