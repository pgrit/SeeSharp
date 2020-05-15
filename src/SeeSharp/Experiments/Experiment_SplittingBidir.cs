using GroundWrapper;
using GroundWrapper.Cameras;
using GroundWrapper.Geometry;
using GroundWrapper.Shading;
using GroundWrapper.Shading.Emitters;
using GroundWrapper.Shading.Materials;
using Integrators;
using Integrators.Bidir;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Experiments {
    /// <summary>
    /// This experiment tries to trigger the balance heuristic issue observed in VCM in a simpler setting.
    /// We combine next event estimation with many shadow rays, and bidirectional connections using MIS.
    /// 
    /// Design:
    ///     The scene is set up such that, for a single sample, next event has a higher variance.
    /// 
    /// Assumption:
    ///     
    /// </summary>
    public class Experiment_SplittingBidir : ExperimentFactory {
        public float lightArea;
        public float lightPower;

        public float lightDistance;
        public float planeDistance;
        public float cameraDistance;

        public float cameraFov;

        public int numLightPaths;

        public int numConnections;

        public override Scene MakeScene() {
            Scene scene = new Scene();

            // Create the camera
            var worldToCamera = Matrix4x4.CreateLookAt(-cameraDistance * Vector3.UnitZ,
                                                        Vector3.Zero,
                                                        Vector3.UnitY);
            scene.Camera = new PerspectiveCamera(worldToCamera, cameraFov, null);

            // Create the transmissive plane in-between
            var mesh = new Mesh(new Vector3[] {
                new Vector3(-1, -1, 0),
                new Vector3( 1, -1, 0),
                new Vector3( 1,  1, 0),
                new Vector3(-1,  1, 0),
            }, new int[] {
                0, 1, 2,
                0, 2, 3,
            });
            mesh.Material = new GenericMaterial(new GenericMaterial.Parameters {
                baseColor = Image.Constant(new ColorRGB(1, 1, 1)),
                roughness = 1,
                thin = true,
                diffuseTransmittance = 1,
            });
            scene.Meshes.Add(mesh);

            // Create the light source
            var pos = MathF.Sqrt(lightArea) / 2;
            var lightMesh = new Mesh(new Vector3[] {
                new Vector3(-pos, -pos, lightDistance),
                new Vector3( pos, -pos, lightDistance),
                new Vector3( pos,  pos, lightDistance),
                new Vector3(-pos,  pos, lightDistance),
            }, new int[] {
                0, 1, 2,
                0, 2, 3,
            }, new Vector3[] {
                -Vector3.UnitZ,
                -Vector3.UnitZ,
                -Vector3.UnitZ,
                -Vector3.UnitZ,
            });
            lightMesh.Material = new DiffuseMaterial(new DiffuseMaterial.Parameters {
                baseColor = Image.Constant(ColorRGB.Black)
            });
            scene.Meshes.Add(lightMesh);
            var radiance = lightPower / (MathF.PI * lightArea);
            var emitter = new DiffuseEmitter(lightMesh, ColorRGB.White * radiance);
            scene.Emitters.Add(emitter);

            return scene;
        }

        public override Integrator MakeReferenceIntegrator() {
            return new PathTracer {
                MaxDepth = 2,
                TotalSpp = 32
            };
        }

        public override Dictionary<string, Integrator> MakeMethods() {
            var methods = new Dictionary<string, Integrator>() {
                ["balance"] = new VertexCacheBidir() {
                    NumIterations = 1,

                    MaxDepth = 2,
                    EnableHitting = false,
                    EnableLightTracer = false,

                    NumLightPaths = numLightPaths,
                    NumConnections = numConnections,
                    NumShadowRays = numConnections
                }
            };

            return methods;
        }
    }
}
