using SeeSharp.Common;
using SimpleImageIO;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;

using MaterialLib = System.Collections.Generic.Dictionary<string, SeeSharp.Geometry.ObjMesh.Material>;

namespace SeeSharp.Geometry {
    public class ObjMesh {
        /// <summary>A reference to a vertex/normal/texture coord. of the model.</summary>
        public class Index {
            // Vertex, normal and texture indices(0 means not present)
            public int v, n, t;

            public override int GetHashCode() {
                uint h = 0, g;

                h = (h << 4) + (uint)this.v;
                g = h & 0xF0000000;
                h = g != 0 ? (h ^ (g >> 24)) : h;
                h &= ~g;

                h = (h << 4) + (uint)this.t;
                g = h & 0xF0000000;
                h = g != 0 ? (h ^ (g >> 24)) : h;
                h &= ~g;

                h = (h << 4) + (uint)this.n;
                g = h & 0xF0000000;
                h = g != 0 ? (h ^ (g >> 24)) : h;
                h &= ~g;

                return (int)h;
            }

            public override bool Equals(object other) {
                var b = other as Index;
                if (b == null) return false;

                return v == b.v && n == b.n && t == b.t;
            }
        }

        public class Face {
            // Indices of the face
            public List<Index> indices = new List<Index>();

            // Index into the material names of the model
            public int material;
        }

        /// <summary>A group of faces in the model.</summary>
        public class Group {
            public string name;
            public List<Face> faces = new List<Face>();

            public Group(string name) => this.name = name;
        }

        /// <summary>A object in the model, made of several groups.</summary>
        public class Object {
            public string name;
            public List<Group> groups = new List<Group>();

            public Object(string name) => this.name = name;
        }

        public class Material {
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
        }

        public class File {
            public List<Object>  objects     = new List<Object>();
            public List<Vector3> vertices    = new List<Vector3>();
            public List<Vector3> normals     = new List<Vector3>();
            public List<Vector2> texcoords   = new List<Vector2>();
            public List<string>  materials   = new List<string>();
            public List<string>  mtlFiles    = new List<string>();
            public MaterialLib   materialLib = new MaterialLib();
        }

        public File file = new File();
        public string basePath;
        public List<string> errors;

        public static ObjMesh FromFile(string filename) {
            var mesh = new ObjMesh();
            mesh.errors = new List<string>();

            // Textures and materials will be relative to the .obj
            string full = System.IO.Path.GetFullPath(filename);
            mesh.basePath = System.IO.Path.GetDirectoryName(full);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            Logger.Log($"Parsing {filename}...", Verbosity.Debug);
            // Parse the .obj itself
            using (var file = new System.IO.StreamReader(filename))
                mesh.errors.AddRange(mesh.ParseObjFile(file));
            watch.Stop();
            Logger.Log($"Done parsing .obj after {watch.ElapsedMilliseconds}ms.", Verbosity.Info);

            // Parse all linked .mtl files
            foreach (string mtlFilename in mesh.file.mtlFiles) {
                // the mtl files are expected to be relative to the .obj itself
                string mtlPath = System.IO.Path.Join(mesh.basePath, mtlFilename);

                try {
                    using (var file = new System.IO.StreamReader(mtlPath))
                        mesh.errors.AddRange(mesh.ParseMtlFile(file));
                } catch (System.IO.FileNotFoundException) {
                    Logger.Log($".mtl not found: {mtlPath}", Verbosity.Debug);
                }
            }

            return mesh;
        }

        private List<string> ParseObjFile(System.IO.StreamReader stream) {
            // Add an empty object to the scene
            int cur_object = 0;
            file.objects.Add(new Object(""));

            // Add an empty group to this object
            int cur_group = 0;
            file.objects[0].groups.Add(new Group(""));

            // Add an empty material to the scene
            int cur_mtl = 0;
            file.materials.Add("");

            // Add dummy vertex, normal, and texcoord
            file.vertices.Add(Vector3.Zero);
            file.normals.Add(Vector3.Zero);
            file.texcoords.Add(Vector2.Zero);

            List<string> errors = new List<string>();
            int cur_line = 0;
            string line;

            Vector3 ParseVector3() {
                Vector3 v = new Vector3();
                var matches = regexFloat.Matches(line);
                if (matches.Count != 3)
                    errors.Add($"Invalid vector (line {cur_line}).");
                v.X = float.Parse(matches[0].Value);
                v.Y = float.Parse(matches[1].Value);
                v.Z = float.Parse(matches[2].Value);
                return v;
            }

            Vector2 ParseVector2() {
                Vector2 v = new Vector2();
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

                    var idx = new Index();
                    idx.v = int.Parse(matches[i].Groups[1].Value);
                    if (matches[i].Groups[2].Value != "") // texture is optional
                        idx.t = int.Parse(matches[i].Groups[2].Value);
                    if (matches[i].Groups[3].Value != "") // normal is optional
                        idx.n = int.Parse(matches[i].Groups[3].Value);

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
                        file.vertices.Add(ParseVector3());
                    break;
                    case 'n': // normals
                        file.normals.Add(ParseVector3());
                    break;
                    case 't': // uvs
                        file.texcoords.Add(ParseVector2());
                    break;
                    default:
                        errors.Add($"Invalid vertex (line {cur_line}).");
                        break;
                    }
                } else if (line[0] == 'f' && char.IsWhiteSpace(line[1])) {
                    Face f = new Face();
                    f.material = cur_mtl;

                    f.indices = ReadFace();

                    if (f.indices.Count < 3) {
                        errors.Add($"Invalid face (line {cur_line}).");
                    } else {
                        // Convert relative indices to absolute
                        for (int i = 0; i < f.indices.Count; i++) {
                            f.indices[i].v = (f.indices[i].v < 0) ? file.vertices.Count  + f.indices[i].v : f.indices[i].v;
                            f.indices[i].t = (f.indices[i].t < 0) ? file.texcoords.Count + f.indices[i].t : f.indices[i].t;
                            f.indices[i].n = (f.indices[i].n < 0) ? file.normals.Count   + f.indices[i].n : f.indices[i].n;
                        }

                        // Check if the indices are valid or not
                        bool valid = true;
                        for (int i = 0; i < f.indices.Count; i++) {
                            if (f.indices[i].v <= 0 || f.indices[i].t < 0 || f.indices[i].n < 0) {
                                valid = false;
                                break;
                            }
                        }

                        if (valid) {
                            file.objects[cur_object].groups[cur_group].faces.Add(f);
                        } else {
                            errors.Add($"Invalid indices in face definition (line {cur_line} ).");
                        }
                    }
                } else if (line[0] == 'g' && char.IsWhiteSpace(line[1])) {
                    string groupName = line.Substring(2).Trim();
                    file.objects[cur_object].groups.Add(new Group(groupName));
                    cur_group++;
                } else if (line[0] == 'o' && char.IsWhiteSpace(line[1])) {
                    string objectName = line.Substring(2).Trim();
                    file.objects.Add(new Object(objectName));
                    cur_object++;
                    file.objects[cur_object].groups.Add(new Group(""));
                    cur_group = 0;
                } else if (line.StartsWith("usemtl") && char.IsWhiteSpace(line[6])) {
                    line = line.Substring(6);
                    string mtl_name = line.Trim();

                    cur_mtl = file.materials.FindIndex(x => x == mtl_name);
                    if (cur_mtl < 0) {
                        file.materials.Add(mtl_name);
                        cur_mtl = file.materials.Count - 1;
                    }
                } else if (line.StartsWith("mtllib") && char.IsWhiteSpace(line[6])) {
                    line = line.Substring(6);
                    string lib_name = line.Trim();
                    file.mtlFiles.Add(lib_name);
                } else if (line[0] == 's' && char.IsWhiteSpace(line[1])) {
                    // Ignore smooth commands
                } else {
                    errors.Add($"Unknown command '{line}' (line {cur_line}).");
                }
            }

            return errors;
        }

        // Common regular expressions for extracting values
        Regex regexFloat = new Regex(@"([+-]?([0-9]+([.][0-9]*)?|[.][0-9]+))", RegexOptions.Compiled);
        Regex regexIndex = new Regex(@"([+-]?[0-9]+)/?([+-]?[0-9]+)?/?([+-]?[0-9]+)?", RegexOptions.Compiled);

        private List<string> ParseMtlFile(System.IO.StreamReader stream) {
            var errors = new List<string>();
            int cur_line = 0;
            string line;

            string mtl_name = "";
            Material current_material() {
                return file.materialLib[mtl_name];
            };

            RgbColor ParseRGB() {
                RgbColor v = new RgbColor();
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
                    mtl_name = line.Substring(6).Trim();
                    if (!file.materialLib.TryAdd(mtl_name, new Material()))
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
                        mat.specularIndex = float.Parse(line.Substring(3));
                    } else if (line[1] == 'i' && char.IsWhiteSpace(line[2])) {
                        var mat = current_material();
                        mat.indexOfRefraction = float.Parse(line.Substring(3));
                    } else
                        errors.Add($"Invalid command '{line}' (line {cur_line}).");
                } else if (line[0] == 'T') {
                    if (line[1] == 'f' && char.IsWhiteSpace(line[2])) {
                        var mat = current_material();
                        mat.transmittance = ParseRGB();
                    } else if (line[1] == 'r' && char.IsWhiteSpace(line[2])) {
                        var mat = current_material();
                        mat.transparency = float.Parse(line.Substring(3));
                    } else
                        errors.Add($"Invalid command '{line}' (line {cur_line}).");
                } else if (line[0] == 'd' && char.IsWhiteSpace(line[1])) {
                    var mat = current_material();
                    mat.dissolveFactor = float.Parse(line.Substring(2));
                } else if (line.StartsWith("illum") && char.IsWhiteSpace(line[5])) {
                    var mat = current_material();
                    mat.illuminationModel = int.Parse(line.Substring(6));
                } else if (line.StartsWith("map_Ka") && char.IsWhiteSpace(line[6])) {
                    var mat = current_material();
                    mat.ambientTexture = line.Substring(7).Trim();
                } else if (line.StartsWith("map_Kd") && char.IsWhiteSpace(line[6])) {
                    var mat = current_material();
                    mat.diffuseTexture = line.Substring(7).Trim();
                } else if (line.StartsWith("map_Ks") && char.IsWhiteSpace(line[6])) {
                    var mat = current_material();
                    mat.specularTexture = line.Substring(7).Trim();
                } else if (line.StartsWith("map_Ke") && char.IsWhiteSpace(line[6])) {
                    var mat = current_material();
                    mat.emittingTexture = line.Substring(7).Trim();
                } else if (line.StartsWith("map_bump") && char.IsWhiteSpace(line[8])) {
                    var mat = current_material();
                    mat.bumpMap = line.Substring(9).Trim();
                } else if (line.StartsWith("bump") && char.IsWhiteSpace(line[4])) {
                    var mat = current_material();
                    mat.bumpMap = line.Substring(5).Trim();
                } else if (line.StartsWith("map_d") && char.IsWhiteSpace(line[5])) {
                    var mat = current_material();
                    mat.dissolveTexture = line.Substring(6).Trim();
                } else
                    errors.Add("Unknown command '{line}' (line {cur_line}).");
            }

            return errors;
        }
    }
}
