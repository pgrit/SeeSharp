using SimpleImageIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SeeSharp.Experiments {
    /// <summary>
    /// Represents a scene loaded from a directory in the <see cref="SceneRegistry" />.
    /// </summary>
    public class SceneFromFile : SceneConfig {
        FileInfo file;
        int maxDepth;
        Scene scene;
        string name;

        public override int MaxDepth => maxDepth;

        public override string Name => name;

        /// <summary>
        /// Creates a shallow copy of this scene configuration under a new name.
        /// </summary>
        /// <param name="newName">The new name to use</param>
        /// <returns>Shallow copy with new name</returns>
        public SceneFromFile WithName(string newName) {
            var copy = (SceneFromFile)this.MemberwiseClone();
            copy.name = newName;
            return copy;
        }

        static Dictionary<string, Type> IntegratorNames = new() {
            { "PathTracer", typeof(Integrators.PathTracer) },
            { "VCM", typeof(Integrators.Bidir.VertexConnectionAndMerging) },
            { "ClassicBidir", typeof(Integrators.Bidir.ClassicBidir) },
        };

        public override RgbImage GetReferenceImage(int width, int height) {
            string refDir = Path.Join(file.DirectoryName, "References");
            Directory.CreateDirectory(refDir);

            string filename = Path.Join(refDir, $"MaxDepth{MaxDepth}-Width{width}-Height{height}.exr");

            if (File.Exists(filename)) {
                return new RgbImage(filename);
            }

            // TODO we could make this even more fancy by triggering a re-render if the hash of the config
            //      file has changed since the last render (which can be embedded in the filename)
            //      Need to be careful about line-endings in git, though.

            string referenceSpecs = Path.Join(refDir, "Config.json");
            Integrators.Integrator refIntegrator = null;
            if (File.Exists(referenceSpecs)) {
                string json = File.ReadAllText(referenceSpecs);

                using (JsonDocument doc = JsonDocument.Parse(json)) {
                    string name = doc.RootElement.GetProperty("Name").GetString();
                    string settings = doc.RootElement.GetProperty("Settings").GetRawText();

                    Type integratorType = IntegratorNames[name];
                    refIntegrator = JsonSerializer.Deserialize(settings, integratorType, new() {
                        IncludeFields = true,
                    }) as Integrators.Integrator;
                }

                Common.Logger.Log("Rendering reference based on Config.json");
            } else {
                refIntegrator = DefaultReferenceIntegrator;

                string settingsJson = JsonSerializer.Serialize(refIntegrator, refIntegrator.GetType(), new() {
                    IncludeFields = true,
                    WriteIndented = true
                });
                string name = IntegratorNames.FirstOrDefault(kv => kv.Value == refIntegrator.GetType()).Key;
                string json = "{ \"Name\": \"" + name + "\", \"Settings\": " + settingsJson + "}";
                File.WriteAllText(referenceSpecs, json);

                Common.Logger.Log("Rendering reference with default integrator");
            }

            refIntegrator.MaxDepth = MaxDepth;

            Scene scn = MakeScene();
            scn.FrameBuffer = new(width, height, filename);
            scn.Prepare();
            refIntegrator.Render(scn);
            scn.FrameBuffer.WriteToFile();

            return scn.FrameBuffer.Image;
        }

        public SceneFromFile(string filename, int maxDepth, string name = null) {
            file = new(filename);
            scene = Scene.LoadFromFile(filename);
            this.maxDepth = maxDepth;
            this.name = name ?? Path.GetFileNameWithoutExtension(filename);
        }

        public virtual SeeSharp.Integrators.Integrator DefaultReferenceIntegrator
        => new SeeSharp.Integrators.PathTracer() {
            BaseSeed = 571298512u,
            TotalSpp = 512,
            MaxDepth = MaxDepth
        };

        public override Scene MakeScene() => scene.Copy();
    }
}