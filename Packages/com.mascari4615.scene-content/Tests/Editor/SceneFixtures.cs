using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Geometry;
using Geometry.Serialization;

namespace SceneContent.Tests
{
    internal sealed class MemorySource : IContentSource
    {
        public byte[] Bytes;
        public byte[] Read(ContentResource resource) { return Bytes; }
    }

    internal static class SceneFixtures
    {
        public static MeshGeometry Geometry()
        {
            MeshGeometry geometry = new MeshGeometry
            {
                Positions = new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f },
                Normals = new[] { 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f },
                Uv = new[] { 0f, 0f, 1f, 0f, 0f, 1f },
                BoneIndices = new[] { 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3 },
                BoneWeights = new[] { .25f, .25f, .25f, .25f, .25f, .25f, .25f, .25f, .25f, .25f, .25f, .25f },
                BindPoses = new float[64], Submeshes = new[] { new[] { 0, 1, 2 } },
                Morphs = new[] { new MorphTarget { Id = "expand", Frames = new[] { new MorphFrame { Weight = 1, Indices = new[] { 0 }, Positions = new[] { .2f, 0f, 0f }, Normals = new float[0], Tangents = new float[0] } } } }
            };
            for (int bone = 0; bone < 4; bone++)
                for (int diagonal = 0; diagonal < 4; diagonal++) geometry.BindPoses[bone * 16 + diagonal * 5] = 1;
            return geometry;
        }

        public static SceneDocument Document(byte[] bytes)
        {
            List<SceneNode> nodes = new List<SceneNode>();
            for (int index = 0; index < 5; index++)
                nodes.Add(new SceneNode { Id = "node-" + index, Parent = index == 0 ? -1 : 0, Position = new[] { 0f, 0f, 0f }, Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } });
            string hash;
            using (SHA256 algorithm = SHA256.Create()) hash = BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            return new SceneDocument
            {
                Id = "scene-fixture", Nodes = nodes.ToArray(),
                Meshes = new[] { new MeshInstance { Node = 0, Geometry = "geometry", RootBone = 1, Bones = new[] { 1, 2, 3, 4 }, Materials = new[] { 0 }, MorphWeights = new[] { .5f } } },
                Materials = new[] { new SurfaceMaterial { BaseColor = new[] { 1f, 1f, 1f, 1f } } },
                Resources = new[] { new ContentResource { Id = "geometry", Path = "mesh.bin", MediaType = "application/vnd.native.geometry", Sha256 = hash } }
            };
        }

        public static byte[] Bytes() { return GeometryBinary.Write(Geometry()); }
    }
}
