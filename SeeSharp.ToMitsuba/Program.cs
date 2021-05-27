using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using SeeSharp;
using SeeSharp.Common;
using SeeSharp.Geometry;
using SeeSharp.Image;
using SeeSharp.Shading.Background;
using SeeSharp.Shading.Emitters;
using SeeSharp.Shading.Materials;
using SimpleImageIO;

namespace SeeSharpToMitsuba {
    class Program {
        static void WriteMeshPly(Mesh mesh, string filename) {
            using var writer = File.OpenWrite(filename);

            writer.Write(UTF8Encoding.ASCII.GetBytes("ply\n"));
            writer.Write(UTF8Encoding.ASCII.GetBytes("format binary_little_endian 1.0\n"));

            writer.Write(UTF8Encoding.ASCII.GetBytes($"element vertex {mesh.NumVertices}\n"));
            writer.Write(UTF8Encoding.ASCII.GetBytes("property float x\n"));
            writer.Write(UTF8Encoding.ASCII.GetBytes("property float y\n"));
            writer.Write(UTF8Encoding.ASCII.GetBytes("property float z\n"));
            writer.Write(UTF8Encoding.ASCII.GetBytes("property float nx\n"));
            writer.Write(UTF8Encoding.ASCII.GetBytes("property float ny\n"));
            writer.Write(UTF8Encoding.ASCII.GetBytes("property float nz\n"));
            if (mesh.TextureCoordinates != null) {
                writer.Write(UTF8Encoding.ASCII.GetBytes("property float u\n"));
                writer.Write(UTF8Encoding.ASCII.GetBytes("property float v\n"));
            }

            writer.Write(UTF8Encoding.ASCII.GetBytes($"element face {mesh.NumFaces}\n"));
            writer.Write(UTF8Encoding.ASCII.GetBytes("property list uchar int vertex_index\n"));

            writer.Write(UTF8Encoding.ASCII.GetBytes("end_header\n"));

            for (int i = 0; i < mesh.NumVertices; ++i) {
                writer.Write(BitConverter.GetBytes(mesh.Vertices[i].X));
                writer.Write(BitConverter.GetBytes(mesh.Vertices[i].Y));
                writer.Write(BitConverter.GetBytes(mesh.Vertices[i].Z));

                writer.Write(BitConverter.GetBytes(mesh.ShadingNormals[i].X));
                writer.Write(BitConverter.GetBytes(mesh.ShadingNormals[i].Y));
                writer.Write(BitConverter.GetBytes(mesh.ShadingNormals[i].Z));

                if (mesh.TextureCoordinates != null) {
                    writer.Write(BitConverter.GetBytes(mesh.TextureCoordinates[i].X));
                    writer.Write(BitConverter.GetBytes(mesh.TextureCoordinates[i].Y));
                }
            }

            for (int i = 0; i < mesh.NumFaces; ++i) {
                writer.Write(new[] {(byte)3});
                writer.Write(BitConverter.GetBytes(mesh.Indices[i * 3 + 0]));
                writer.Write(BitConverter.GetBytes(mesh.Indices[i * 3 + 1]));
                writer.Write(BitConverter.GetBytes(mesh.Indices[i * 3 + 2]));
            }
        }

        static int meshCounter = 0;
        static int texCounter = 0;
        static string parentDir;

        static XElement MapTextureOrColor(TextureRgb texture, string name) {
            if (texture.IsConstant) {
                var clr = texture.Lookup(Vector2.Zero);
                return new XElement("rgb", MakeNameValue(name, $"{clr.R}, {clr.G}, {clr.B}"));
            } else {
                texCounter++;
                string filename = $"Textures/texture-{texCounter:0000}.exr";
                texture.Image.WriteToFile(filename);
                return new("texture", new XAttribute("type", "bitmap"), new XAttribute("name", name),
                    new XElement("string", MakeNameValue("filename", filename))
                );
            }
        }

        static XElement MapRoughnessScalarOrTexture(TextureMono texture) {
            // Alpha is the squared roughness
            if (texture.IsConstant) {
                var roughness = texture.Lookup(Vector2.Zero);
                roughness *= roughness;
                return new XElement("float", MakeNameValue("alpha", roughness.ToString()));
            } else {
                texCounter++;
                string filename = $"Textures/texture-{texCounter:0000}.exr";
                MonochromeImage img = new(texture.Image.Width, texture.Image.Height);
                for (int row = 0; row < img.Height; ++row) {
                    for (int col = 0; col < img.Width; ++col) {
                        float r = texture.Image.GetPixelChannel(col, row, 0);
                        img.SetPixel(col, row, r * r);
                    }
                }
                img.WriteToFile(filename);
                return new("texture", new XAttribute("type", "bitmap"), new XAttribute("name", "alpha"),
                    new XElement("string", MakeNameValue("filename", filename))
                );
            }
        }

        static XAttribute[] MakeNameValue(string name, string value)
        => new[] { new XAttribute("name", name), new XAttribute("value", value) };

        static XElement ExportMesh(Scene scene, Mesh mesh) {
            meshCounter++;
            string filename = $"Meshes/mesh-{meshCounter:0000}.ply";
            WriteMeshPly(mesh, Path.Join(parentDir, filename));

            // TODO materials, textures
            XElement bsdf = null;
            if (mesh.Material is DiffuseMaterial) {
                var mat = mesh.Material as DiffuseMaterial;
                bsdf = new("bsdf", new XAttribute("type", "twosided"),
                    new XElement("bsdf", new XAttribute("type", "diffuse"),
                        MapTextureOrColor(mat.MaterialParameters.BaseColor, "reflectance")
                    )
                );
            } else if (mesh.Material is GenericMaterial) {
                var mat = mesh.Material as GenericMaterial;
                if (mat.MaterialParameters.DiffuseTransmittance > 0) {
                    float difftrans = mat.MaterialParameters.DiffuseTransmittance;
                    bsdf = new("bsdf", new XAttribute("type", "mixturebsdf"),
                        new XElement("string",
                            new XAttribute("name", "weights"),
                            new XAttribute("value", $"{difftrans}, {1 - difftrans}")
                        ),
                        new XElement("bsdf", new XAttribute("type", "difftrans"),
                            MapTextureOrColor(mat.MaterialParameters.BaseColor, "transmittance")
                        ),
                        new XElement("bsdf", new XAttribute("type", "diffuse"),
                            MapTextureOrColor(mat.MaterialParameters.BaseColor, "reflectance")
                        )
                    );
                } else if (mat.MaterialParameters.SpecularTransmittance > 0) {
                    // Rough dielectric BSDF
                    bsdf = new("bsdf", new XAttribute("type", "roughdielectric"),
                        new XElement("string", MakeNameValue("distribution", "ggx")),
                        MapRoughnessScalarOrTexture(mat.MaterialParameters.Roughness),
                        new XElement("float", MakeNameValue("intIOR",
                            mat.MaterialParameters.IndexOfRefraction.ToString()))
                    );
                } else if (mat.MaterialParameters.Metallic > 0.8f) {
                    // Rough conductor BSDF (silver-like)
                    bsdf = new("bsdf", new XAttribute("type", "roughconductor"),
                        new XElement("string", MakeNameValue("distribution", "ggx")),
                        MapRoughnessScalarOrTexture(mat.MaterialParameters.Roughness),
                        new XElement("string", MakeNameValue("material", "Ag"))
                    );
                } else {
                    // Plastic BSDF
                    bsdf = new("bsdf", new XAttribute("type", "roughplastic"),
                        new XElement("string", MakeNameValue("distribution", "ggx")),
                        MapRoughnessScalarOrTexture(mat.MaterialParameters.Roughness),
                        new XElement("float", MakeNameValue("intIOR",
                            mat.MaterialParameters.IndexOfRefraction.ToString())),
                        MapTextureOrColor(mat.MaterialParameters.BaseColor, "diffuseReflectance")
                    );
                }
            } else {
                Logger.Log("Unsupported material found!", Verbosity.Warning);
                return null;
            }

            XElement emitter = null;
            foreach (var em in scene.Emitters) {
                if (em.Mesh == mesh) {
                    var clr = (em as DiffuseEmitter).Radiance;
                    emitter = new("emitter", new XAttribute("type", "area"),
                        new XElement("rgb",
                            new XAttribute("name", "radiance"),
                            new XAttribute("value", $"{clr.R}, {clr.G}, {clr.B}")
                        )
                    );
                }
            }

            return new("shape",
                new XAttribute("type", "ply"),
                new XElement("string", new XAttribute("name", "filename"), new XAttribute("value", filename)),
                bsdf, emitter
            );
        }

        static XElement ConvertBackground(Scene scene) {
            var map = scene.Background as EnvironmentMap;
            if (map == null) return null;

            string filename = $"Textures/background.exr";
            map.Image.WriteToFile(filename);

            return new("emitter", new XAttribute("type", "envmap"),
                new XElement("string", MakeNameValue("filename", filename)),
                new XElement("transform", new XAttribute("name", "toWorld"),
                    new XElement("rotate", new XAttribute("y", "1"), new XAttribute("angle", -90))
                )
            );
        }

        static XElement ConvertCamera(Scene scene) {
            var cam = scene.Camera as SeeSharp.Cameras.PerspectiveCamera;
            if (cam == null) {
                Logger.Log("No / incompatible camera", Verbosity.Warning);
                return null;
            }

            var target = cam.Position + cam.Direction;
            Matrix4x4.Invert(cam.WorldToCamera, out var camToWorld);
            Vector4 up = Vector4.Transform(Vector4.UnitY, camToWorld);

            XElement lookAt = new("lookat",
                new XAttribute("origin", $"{cam.Position.X}, {cam.Position.Y}, {cam.Position.Z}"),
                new XAttribute("target", $"{target.X}, {target.Y}, {target.Z}"),
                new XAttribute("up", $"{up.X}, {up.Y}, {up.Z}")
            );

            XElement transform = new("transform", lookAt, new XAttribute("name", "toWorld"));

            XElement fov = new("float",
                new XAttribute("name", "fov"),
                new XAttribute("value", cam.VerticalFieldOfView)
            );

            XElement fovAxis = new("string",
                new XAttribute("name", "fovAxis"),
                new XAttribute("value", "y")
            );

            return new("sensor", transform, fov, fovAxis, new XAttribute("type", "perspective"));
        }

        /// <summary>
        /// Converts a SeeSharp .json scene to Mitsuba's xml format (roughly matching materials along the way).
        /// Meshes are exported as .ply into a directory called "Meshes" and textures are exported into a
        /// directory called "Textures" next to the outpuf file name.
        /// </summary>
        /// <param name="scene">Path to a SeeSharp scene in .json format</param>
        /// <param name="output">Filename of the Mitsuba scene after conversion</param>
        static int Main(FileInfo scene, string output = "scene.xml") {
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            if (scene == null || !scene.Exists) {
                Logger.Log("No scene file given, use '--scene [filename.json]'", Verbosity.Error);
                return 1;
            }

            var sceneData = Scene.LoadFromFile(scene.FullName);
            if (sceneData == null) {
                Logger.Log($"Scene file '{scene.FullName}' is an invalid scene", Verbosity.Error);
                return 1;
            }

            parentDir = Path.GetDirectoryName(Path.GetFullPath(output));
            Directory.CreateDirectory(Path.Join(parentDir, "Meshes"));
            Directory.CreateDirectory(Path.Join(parentDir, "Textures"));

            XElement sceneElement = new("scene", new XAttribute("version", "0.5.0"),
                ConvertCamera(sceneData),
                ConvertBackground(sceneData),
                from mesh in sceneData.Meshes
                select ExportMesh(sceneData, mesh)
            );

            XDocument result = new(sceneElement);
            result.Declaration = new("1.0", "utf-8", null);
            File.WriteAllText(output, result.Declaration.ToString() + Environment.NewLine + result.ToString());
            Logger.Log("Done.", Verbosity.Info);

            return 0;
        }
    }
}
