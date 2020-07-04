﻿using SeeSharp.Core;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Sampling;
using SeeSharp.Core.Shading;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace SeeSharp.Integrators.Bidir {
    public class PhotonMapper : Integrator {
        public int NumIterations = 2;
        public int NumLightPaths = 0;
        public int MaxDepth = 10;
        public uint BaseSeedLight = 0xC030114u;
        public uint BaseSeedCamera = 0x13C0FEFEu;

        protected Scene scene;
        protected LightPathCache lightPaths;

        PhotonHashGrid photonMap = new PhotonHashGrid();

        public override void Render(Scene scene) {
            this.scene = scene;

            if (NumLightPaths <= 0) {
                NumLightPaths = scene.FrameBuffer.Width * scene.FrameBuffer.Height;
            }

            lightPaths = new LightPathCache { MaxDepth = MaxDepth, NumPaths = NumLightPaths, Scene = scene };

            for (uint iter = 0; iter < NumIterations; ++iter) {
                scene.FrameBuffer.StartIteration();
                lightPaths.TraceAllPaths(iter, null);
                ProcessPathCache();
                TraceAllCameraPaths(iter);
                scene.FrameBuffer.EndIteration();
            }
        }

        public virtual void ProcessPathCache() {
            photonMap.Build(lightPaths, scene.SceneRadius / 100);
        }

        public virtual ColorRGB EstimatePixelValue(SurfacePoint cameraPoint, Vector2 filmPosition, Ray primaryRay,
                                                   float pdfFromCamera, ColorRGB initialWeight, RNG rng) {
            // Trace the primary ray into the scene
            var hit = scene.Raytracer.Trace(primaryRay);
            if (!hit)
                return scene.Background != null ? scene.Background.EmittedRadiance(primaryRay.Direction) : ColorRGB.Black;

            // Gather nearby photons
            float radius = scene.SceneRadius / 100.0f;
            ColorRGB estimate = ColorRGB.Black;
            photonMap.Query(hit.Position, (vertexIdx, mergeDistanceSquared) => {
                // Compute the contribution of the photon
                var photon = lightPaths.PathCache[vertexIdx];
                var ancestor = lightPaths.PathCache[photon.AncestorId];
                var dirToAncestor = ancestor.Point.Position - photon.Point.Position;
                var bsdfValue = photon.Point.Bsdf.EvaluateBsdfOnly(-primaryRay.Direction, dirToAncestor, false);
                var photonContrib = photon.Weight * bsdfValue / NumLightPaths;

                // Epanechnikov kernel
                float radiusSquared = radius * radius;
                photonContrib *= (radiusSquared - mergeDistanceSquared) * 2.0f / (radiusSquared * radiusSquared * MathF.PI);

                estimate += photonContrib;
            }, radius);

            // Add contribution from directly visible light sources
            var light = scene.QueryEmitter(hit);
            if (light != null) {
                estimate += light.EmittedRadiance(hit, -primaryRay.Direction);
            }

            return estimate;
        }

        private void RenderPixel(uint row, uint col, RNG rng) {
            // Sample a ray from the camera
            var offset = rng.NextFloat2D();
            var filmSample = new Vector2(col, row) + offset;
            Ray primaryRay = scene.Camera.GenerateRay(filmSample);

            // Compute the corresponding solid angle pdf (required for MIS)
            float pdfFromCamera = scene.Camera.SolidAngleToPixelJacobian(primaryRay.Direction); // TODO this should be returned by Camera.Sample() which should replace GenerateRay() to follow conventions similar to the BSDF system
            var initialWeight = ColorRGB.White; // TODO this should be computed by the camera and returned by SampleCamera()
            var cameraPoint = new SurfacePoint {
                Position = scene.Camera.Position,
                Normal = scene.Camera.Direction
            };

            var value = EstimatePixelValue(cameraPoint, filmSample, primaryRay, pdfFromCamera, initialWeight, rng);

            // TODO we do nearest neighbor splatting manually here, to avoid numerical
            //      issues if the primary samples are almost 1 (400 + 0.99999999f = 401)
            scene.FrameBuffer.Splat((float)col, (float)row, value);
        }

        private void TraceAllCameraPaths(uint iter) {
            Parallel.For(0, scene.FrameBuffer.Height,
                row => {
                    for (uint col = 0; col < scene.FrameBuffer.Width; ++col) {
                        uint pixelIndex = (uint)(row * scene.FrameBuffer.Width + col);
                        var seed = RNG.HashSeed(BaseSeedCamera, pixelIndex, (uint)iter);
                        var rng = new RNG(seed);
                        RenderPixel((uint)row, col, rng);
                    }
                }
            );
        }
    }
}