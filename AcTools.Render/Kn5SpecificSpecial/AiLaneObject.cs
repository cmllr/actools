using System;
using System.Collections.Generic;
using AcTools.AiFile;
using AcTools.ExtraKn5Utils.Kn5Utils;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class AiLaneObject : TrianglesRenderableObject<InputLayouts.VerticeP> {
        private AiLaneObject([CanBeNull] string name, InputLayouts.VerticeP[] vertices, ushort[] indices) : base(name, vertices, indices) { }

        private static Vector3 Prev(Vector3 s0, Vector3 s1) {
            return new Vector3(s0.X - 0.0001f * (s1.X - s0.X), s0.Y - 0.0001f * (s1.Y - s0.Y), s0.Z - 0.0001f * (s1.Z - s0.Z));
        }

        private static IRenderableObject Create(AiPoint[] aiPoints, AiPointExtra[] aiPointsExtra, float? fixedWidth, int from, int to) {
            var vertices = new List<InputLayouts.VerticeP>();
            var indices = new List<ushort>();

            var points = new Vector3[aiPoints.Length];
            for (var i = 0; i < points.Length; i++) {
                points[i] = aiPoints[i].Position.ToVector3();
            }

            for (var i = from; i < to; i++) {
                var v0 = Get(points, i - 2);
                var v1 = Get(points, i - 1);
                var v2 = points[i];
                var v3 = Get(points, i + 1);

                if ((v1 - v2).LengthSquared() > 100f) continue;
                if ((v0 - v1).LengthSquared() > 100f) v0 = Prev(v1, v2);
                if ((v3 - v2).LengthSquared() > 100f) v3 = Prev(v2, v1);

                var d = Vector3.Normalize(v2 - v1);

                var d0 = Vector3.Normalize(v1 - v0);
                var d3 = Vector3.Normalize(v3 - v2);

                var s0 = Vector3.Normalize(Vector3.Cross(Vector3.Normalize((d + d0) / 2), Vector3.UnitY));
                var s3 = Vector3.Normalize(Vector3.Cross(Vector3.Normalize((d + d3) / 2), Vector3.UnitY));

                var j = (ushort)vertices.Count;

                if (fixedWidth.HasValue) {
                    s0 *= fixedWidth.Value;
                    s3 *= fixedWidth.Value;
                    vertices.Add(new InputLayouts.VerticeP(v1 + s0));
                    vertices.Add(new InputLayouts.VerticeP(v1 - s0));
                    vertices.Add(new InputLayouts.VerticeP(v2 + s3));
                    vertices.Add(new InputLayouts.VerticeP(v2 - s3));
                } else {
                    var p1 = Get(aiPointsExtra, i - 1);
                    var p2 = Get(aiPointsExtra, i);
                    vertices.Add(new InputLayouts.VerticeP(v1 + s0 * Math.Max(p1.SideRight, 0.5f)));
                    vertices.Add(new InputLayouts.VerticeP(v1 - s0 * Math.Max(p1.SideLeft, 0.5f)));
                    vertices.Add(new InputLayouts.VerticeP(v2 + s3 * Math.Max(p2.SideRight, 0.5f)));
                    vertices.Add(new InputLayouts.VerticeP(v2 - s3 * Math.Max(p2.SideLeft, 0.5f)));
                }

                indices.Add(j);
                indices.Add((ushort)(j + 1));
                indices.Add((ushort)(j + 2));
                indices.Add((ushort)(j + 3));
                indices.Add((ushort)(j + 2));
                indices.Add((ushort)(j + 1));
            }

            return new AiLaneObject("_aiLine", vertices.ToArray(), indices.ToArray()) {
                OptimizedBoundingBoxUpdate = false
            };

            T Get<T>(T[] array, int index) {
                return index < 0 ? array[array.Length + index]
                        : index >= array.Length ? array[index - array.Length] : array[index];
            }
        }

        public static IRenderableObject Create(AiSpline aiSpline, float? fixedWidth) {
            // Four vertices per sector
            const int verticesPerSector = 4;

            // Actually, I think it’s 65535, but let’s not go there
            const int maxVertices = 30000;

            var aiPoints = aiSpline.Points;
            var aiPointsExtra = aiSpline.PointsExtra;
            var sectorsNumber = aiPoints.Length - 1;
            if (sectorsNumber < 1) return new InvisibleObject();

            if (sectorsNumber * verticesPerSector <= maxVertices) {
                return Create(aiPoints, aiPointsExtra, fixedWidth, 0, aiPoints.Length);
            }

            var pointsPerObject = maxVertices / verticesPerSector + 1;
            var result = new RenderableList();
            for (var i = 0; i < aiPoints.Length; i += pointsPerObject) {
                result.Add(Create(aiPoints, aiPointsExtra, fixedWidth, i, Math.Min(i + pointsPerObject, aiPoints.Length)));
            }

            AcToolsLogging.Write("Objects: " + result.Count);
            return result;
        }

        private IRenderableMaterial _material;

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);

            _material = contextHolder.Get<SharedMaterials>().GetMaterial(BasicMaterials.DepthOnlyKey);
            _material.EnsureInitialized(contextHolder);
        }

        protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Simple) return;
            if (!_material.Prepare(contextHolder, mode)) return;

            base.DrawOverride(contextHolder, camera, mode);
            _material.SetMatrices(ParentMatrix, camera);
            _material.Draw(contextHolder, Indices.Length, mode);
        }

        public override BaseRenderableObject Clone() {
            throw new NotSupportedException();
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _material);
            base.Dispose();
        }
    }
}