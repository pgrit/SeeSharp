using System.Linq;
using System.Text;

namespace SeeSharp.IO;

/// <summary>
/// Represents an entire parsed .ply file
/// Note, we ignore vertex color or other auxilary information
/// </summary>
public class PlyFile {

    /// <summary>
    /// A face with arbitrarily many vertices, given by a list of indices
    /// </summary>
    public class Face {
        /// <summary> Indices of the face </summary>
        public List<int> Indices = new();
    }

    /// <summary>
    /// All faces in the file
    /// </summary>
    public List<Face> Faces = new();

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
    /// Loads .ply file and returns list of errors
    /// </summary>
    /// <param name="filename"></param>
    /// <returns>True if successful</returns>
    public bool ParseFile(string filename) {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Logger.Log($"Parsing {filename}...", Verbosity.Debug);

        bool success = false;
        // Parse the .ply itself
        using (var file = new MixReader(filename, Encoding.ASCII))
            success = ParsePlyFile(file);
        watch.Stop();
        Logger.Log($"Done parsing .ply after {watch.ElapsedMilliseconds}ms.", Verbosity.Debug);

        return success;
    }

    /// <summary>
    /// Constructs mesh from previously loaded file content
    /// </summary>
    /// <returns>A mesh</returns>
    public Mesh ToMesh() {
        return new(Vertices.ToArray(), ToTriangleList().ToArray(),
               Normals.Count == 0 ? null : Normals.ToArray(),
               Texcoords.Count == 0 ? null : Texcoords.ToArray());
    }

    /// <summary>
    /// Construct list of triangle indices from convex polygons
    /// </summary>
    private List<int> ToTriangleList() {
        List<int> list = new();
        bool warn_face = false;
        foreach (Face f in Faces) {
            if (f.Indices.Count == 3) {
                list.AddRange(f.Indices);
            } else if (f.Indices.Count < 3) {
                warn_face = true;
            } else { // Fan triangulation, only works for convex polygons
                int pin = f.Indices[0];
                for (int i = 2; i < f.Indices.Count; ++i) {
                    list.AddRange(new int[3] { pin, f.Indices[i - 1], f.Indices[i] });
                }
            }
        }

        if (warn_face) Logger.Log("Mesh contains invalid faces. Skipping them.", Verbosity.Warning);

        return list;
    }

    /// <summary>
    /// Interface to read binary and ascii based data region
    /// </summary>
    private interface IDataReader {
        public (Vector3, Vector3, Vector2) ReadPerVertexLine();
        public Face ReadFaceLine();
    }

    /// <summary>
    /// Data reader for ascii based files
    /// </summary>
    private class AsciiDataReader : IDataReader {
        public AsciiDataReader(PlyHeader header, MixReader stream) {
            Header = header;
            Stream = stream;
        }

        public Face ReadFaceLine() {
            bool eos = false;// Ignore
            string[] parts = Stream.ReadLineAsString(ref eos).Split();
            int count = int.Parse(parts[0]);
            return new() { Indices = parts.Skip(1).Take(count).Select(p => int.Parse(p)).ToList() };
        }

        public (Vector3, Vector3, Vector2) ReadPerVertexLine() {
            bool eos = false;// Ignore
            string[] parts = Stream.ReadLineAsString(ref eos).Split();

            float GetPart(int elem) {
                if (elem != -1 && elem < parts.Length)
                    return float.Parse(parts[elem]);
                else
                    return 0;
            }

            float x = GetPart(Header.XElem);
            float y = GetPart(Header.YElem);
            float z = GetPart(Header.ZElem);
            float nx = GetPart(Header.NXElem);
            float ny = GetPart(Header.NYElem);
            float nz = GetPart(Header.NZElem);
            float u = GetPart(Header.UElem);
            float v = GetPart(Header.VElem);

            return (new(x, y, z), new(nx, ny, nz), new(u, v));
        }

        private readonly PlyHeader Header;
        private readonly MixReader Stream;
    }

    /// <summary>
    /// Data reader for binary based files
    /// </summary>
    private class BinaryDataReader : IDataReader {
        public BinaryDataReader(PlyHeader header, MixReader stream) {
            Header = header;
            Stream = stream;
            IsBigEndian = header.IsBigEndian;
        }

        private int GetIndex() {
            bool swap = IsBigEndian == BitConverter.IsLittleEndian;
            int val = Stream.ReadInt32();
            if (swap) {
                byte[] bytes = BitConverter.GetBytes(val);
                Array.Reverse(bytes);
                return BitConverter.ToInt32(bytes);
            } else {
                return val;
            }
        }

        private float GetSingle() {
            bool swap = IsBigEndian == BitConverter.IsLittleEndian;
            float val = Stream.ReadSingle();
            if (swap) {
                byte[] bytes = BitConverter.GetBytes(val);
                Array.Reverse(bytes);
                return BitConverter.ToSingle(bytes);
            } else {
                return val;
            }
        }

        public Face ReadFaceLine() {
            byte elemcount = Stream.ReadByte();

            Face face = new();
            for (byte i = 0; i < elemcount; ++i) {
                face.Indices.Add(GetIndex());
            }

            return face;
        }

        public (Vector3, Vector3, Vector2) ReadPerVertexLine() {
            float x = 0, y = 0, z = 0;
            float nx = 0, ny = 0, nz = 0;
            float u = 0, v = 0;

            for (int i = 0; i < Header.VertexPropCount; ++i) {
                float val = GetSingle();
                if (i == Header.XElem) x = val;
                if (i == Header.YElem) y = val;
                if (i == Header.ZElem) z = val;
                if (i == Header.NXElem) nx = val;
                if (i == Header.NYElem) ny = val;
                if (i == Header.NZElem) nz = val;
                if (i == Header.UElem) u = val;
                if (i == Header.VElem) v = val;
            }

            return (new(x, y, z), new(nx, ny, nz), new(u, v));
        }

        private readonly PlyHeader Header;
        private readonly MixReader Stream;
        private readonly bool IsBigEndian;
    }

    /// <summary>
    /// Essential informations from the .ply header which is always given in ascii format
    /// </summary>
    private class PlyHeader {
        public int VertexCount = 0;
        public int FaceCount = 0;
        public int XElem = -1;
        public int YElem = -1;
        public int ZElem = -1;
        public int NXElem = -1;
        public int NYElem = -1;
        public int NZElem = -1;
        public int UElem = -1;
        public int VElem = -1;
        public int VertexPropCount = 0;
        public int IndElem = -1;

        public bool HasVertices => XElem >= 0 && YElem >= 0 && ZElem >= 0;
        public bool HasNormals => NXElem >= 0 && NYElem >= 0 && NZElem >= 0;
        public bool HasUVs => UElem >= 0 && VElem >= 0;
        public bool HasIndices => IndElem >= 0;

        public string Method = "ascii";
        public bool IsAscii => Method == "ascii";
        public bool IsBinary => !IsAscii;
        public bool IsBigEndian => IsBinary && Method == "binary_big_endian";
    }

    /// <summary>
    /// Returns true if the method of the data region is feasible
    /// </summary>
    private static bool IsAllowedMethod(string method) {
        return method == "ascii" || method == "binary_little_endian" || method == "binary_big_endian";
    }

    /// <summary>
    /// Returns true if the counter type for lists is supported
    /// </summary>
    private static bool IsAllowedVertCountType(string type) {
        return type == "char" || type == "uchar" || type == "int8" || type == "uint8";
    }

    /// <summary>
    /// Returns true if the index type for lists is supported
    /// </summary>
    private static bool IsAllowedVertIndType(string type) {
        return type == "int" || type == "uint";
    }

    /// <summary>
    /// Will parse the header and populate the PlyHeader structure
    /// </summary>
    /// <param name="stream"></param>
    /// <returns>PlyHeader or null if error</returns>
    private static PlyHeader ParsePlyHeader(MixReader stream) {
        PlyHeader header = new();

        bool eos = false;
        string magic = stream.ReadLineAsString(ref eos);
        if (magic != "ply") {
            throw new MeshLoadException("Trying to load invalid .ply file.", stream.Path);
        }

        if (eos) {
            throw new MeshLoadException("Trying to load empty header .ply file.", stream.Path);
        }

        int facePropCounter = 0;
        while (!eos) {
            string line = stream.ReadLineAsString(ref eos);
            string[] parts = line.Split();
            if (parts.Length == 0)
                continue;

            string action = parts[0];
            if (action == "comment") {
                continue;
            } else if (action == "format") {
                if (!IsAllowedMethod(parts[1])) {
                    Logger.Log($"In '{stream.Path}' unknown format '{parts[1]}' given. Ignoring it.", Verbosity.Warning);
                    continue;
                }

                header.Method = parts[1];
            } else if (action == "element") {
                string type = parts[1];
                if (type == "vertex")
                    header.VertexCount = int.Parse(parts[2]);
                else if (type == "face")
                    header.FaceCount = int.Parse(parts[2]);
                else
                    Logger.Log($"In '{stream.Path}' unknown element type '{type}' given. Ignoring it.", Verbosity.Warning);
            } else if (action == "property") {
                string type = parts[1];
                if (type == "float") {
                    string name = parts[2];
                    if (name == "x")
                        header.XElem = header.VertexPropCount;
                    else if (name == "y")
                        header.YElem = header.VertexPropCount;
                    else if (name == "z")
                        header.ZElem = header.VertexPropCount;
                    else if (name == "nx")
                        header.NXElem = header.VertexPropCount;
                    else if (name == "ny")
                        header.NYElem = header.VertexPropCount;
                    else if (name == "nz")
                        header.NZElem = header.VertexPropCount;
                    else if (name == "u" || name == "s")
                        header.UElem = header.VertexPropCount;
                    else if (name == "v" || name == "t")
                        header.VElem = header.VertexPropCount;
                    ++header.VertexPropCount;
                } else if (type == "list") {
                    ++facePropCounter;

                    string countType = parts[2];
                    string indType = parts[3];
                    string name = parts[4];

                    if (!IsAllowedVertCountType(countType)) {
                        Logger.Log($"In '{stream.Path}' only 'property list uchar int' is supported. Ignoring '{countType}'.", Verbosity.Warning);
                        continue;
                    }

                    if (!IsAllowedVertIndType(indType)) {
                        Logger.Log($"In '{stream.Path}' only 'property list uchar int' is supported. Ignoring '{indType}'.", Verbosity.Warning);
                        continue;
                    }

                    if (name == "vertex_indices" || name == "vertex_index")
                        header.IndElem = facePropCounter - 1;
                } else {
                    Logger.Log($"In '{stream.Path}' only float or list properties allowed. Ignoring '{type}'.", Verbosity.Warning);
                    ++header.VertexPropCount;
                }
            } else if (action == "end_header") {
                break;
            } else {
                Logger.Log($"In '{stream.Path}' unknown header entry '{action}'.", Verbosity.Warning);
            }
        }

        return header;
    }

    /// <summary>
    /// Will parse the whole file, starting with the header and following up with the data region
    /// </summary>
    /// <param name="stream"></param>
    /// <returns>True if successful</returns>
    private bool ParsePlyFile(MixReader stream) {

        PlyHeader header = ParsePlyHeader(stream);

        // Error in header, return false
        if (header == null)
            return false;

        if (!header.HasVertices || !header.HasIndices) {
            throw new MeshLoadException("Ply file has no data.", stream.Path);
        }

        // Setup reader
        IDataReader reader;
        if (header.IsBinary) {
            reader = new BinaryDataReader(header, stream);
        } else {
            reader = new AsciiDataReader(header, stream);
        }

        // Reserve memory
        Vertices.Capacity = header.VertexCount;
        if (header.HasNormals) Normals.Capacity = header.VertexCount;
        if (header.HasUVs) Texcoords.Capacity = header.VertexCount;

        // Read per vertex stuff
        for (int i = 0; i < header.VertexCount; ++i) {
            (Vector3 vertex, Vector3 normal, Vector2 tex) = reader.ReadPerVertexLine();

            Vertices.Add(vertex);
            if (header.HasNormals) Normals.Add(normal);
            if (header.HasUVs) Texcoords.Add(tex);
        }

        // Read per face indices
        if (header.IndElem != 0) {
            Logger.Log($"In '{stream.Path}' no support for multiple face properties. Assuming first entry to be the list of indices.", Verbosity.Warning);
        }

        // Load faces. Will be triangulated later
        for (int i = 0; i < header.FaceCount; ++i) {
            Faces.Add(reader.ReadFaceLine());
        }

        return true;
    }
}