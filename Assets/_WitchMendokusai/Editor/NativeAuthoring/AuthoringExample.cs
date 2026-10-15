using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Geometry;
using Geometry.Serialization;
using Motion;
using SceneContent;
using SceneContent.Storage;
using SceneSequence;
using SceneSequence.Storage;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    public static class AuthoringExample
    {
        public static string Create()
        {
            string directory = Path.Combine(Application.temporaryCachePath, "AuthoringExamples", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            byte[] geometry = GeometryBinary.Write(new MeshGeometry
            {
                Positions = new[] { -.5f, -.5f, -.5f, .5f, -.5f, -.5f, .5f, .5f, -.5f, -.5f, .5f, -.5f, -.5f, -.5f, .5f, .5f, -.5f, .5f, .5f, .5f, .5f, -.5f, .5f, .5f },
                Normals = new float[0], Uv = new float[0], BoneIndices = new int[0], BoneWeights = new float[0], BindPoses = new float[0],
                Submeshes = new[] { new[] { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2, 0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5 } }
            });
            File.WriteAllBytes(Path.Combine(directory, "block.geometry"), geometry);
            string hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = BitConverter.ToString(algorithm.ComputeHash(geometry)).Replace("-", "").ToLowerInvariant();
            }
            List<SceneNode> nodes = new List<SceneNode>();
            List<MeshInstance> meshes = new List<MeshInstance>();
            List<PoseTrack> tracks = new List<PoseTrack>();
            AddActor(nodes, meshes, tracks, "amber", -.7f, 0);
            AddActor(nodes, meshes, tracks, "teal", .7f, 1);
            SceneDocument scene = new SceneDocument
            {
                Id = "block-figures", Nodes = nodes.ToArray(), Meshes = meshes.ToArray(),
                Materials = new[]
                {
                    new SurfaceMaterial { BaseColor = new[] { .95f, .52f, .18f, 1f }, DoubleSided = true },
                    new SurfaceMaterial { BaseColor = new[] { .12f, .7f, .68f, 1f }, DoubleSided = true },
                    new SurfaceMaterial { BaseColor = new[] { .95f, .85f, .68f, 1f }, DoubleSided = true },
                    new SurfaceMaterial { BaseColor = new[] { .08f, .12f, .18f, 1f }, DoubleSided = true }
                },
                Resources = new[] { new ContentResource { Id = "block", Path = "block.geometry", MediaType = "application/vnd.native.geometry", Sha256 = hash } }
            };
            SequenceDocument sequence = new SequenceDocument
            {
                Id = "greeting-example", Scene = scene,
                Clips = new[] { new PoseClip { Id = "greeting", Duration = 2, Loop = true, Tracks = tracks.ToArray() } }
            };
            string file = Path.Combine(directory, "greeting.sequence.json");
            SequenceFiles.Save(file, sequence, new DirectoryContentSource(directory));
            return file;
        }

        private static void AddActor(List<SceneNode> nodes, List<MeshInstance> meshes, List<PoseTrack> tracks, string id, float offset, int material)
        {
            int root = AddNode(nodes, id, "Actor " + (material + 1), -1, offset, 0, 0, 1, 1, 1);
            AddBlock(nodes, meshes, id + "-body", "Chest", root, 0, 1.1f, 0, .48f, .65f, .28f, material);
            AddBlock(nodes, meshes, id + "-head", "Head", root, 0, 1.65f, 0, .38f, .4f, .34f, 2);
            AddBlock(nodes, meshes, id + "-eye-left", "", root, -.09f, 1.69f, -.18f, .055f, .06f, .025f, 3);
            AddBlock(nodes, meshes, id + "-eye-right", "", root, .09f, 1.69f, -.18f, .055f, .06f, .025f, 3);
            AddBlock(nodes, meshes, id + "-leg-left", "Left shin", root, -.14f, .4f, 0, .19f, .75f, .24f, 3);
            AddBlock(nodes, meshes, id + "-leg-right", "Right shin", root, .14f, .4f, 0, .19f, .75f, .24f, 3);
            AddBlock(nodes, meshes, id + "-arm-left", "Left upper arm", root, -.37f, 1.05f, 0, .18f, .65f, .22f, material);
            int shoulder = AddNode(nodes, id + "-shoulder", "Right shoulder", root, .34f, 1.4f, 0, 1, 1, 1);
            AddBlock(nodes, meshes, id + "-arm-right", "Right upper arm", shoulder, 0, -.3f, 0, .18f, .65f, .22f, material);
            PoseKey[] keys = new PoseKey[5];
            float[] angles = { -110, -145, -110, -145, -110 };
            for (int index = 0; index < keys.Length; index++)
            {
                float radians = angles[index] * Mathf.Deg2Rad * .5f;
                keys[index] = new PoseKey { Time = index * .5f, Position = new[] { .34f, 1.4f, 0f }, Rotation = new[] { 0f, 0f, Mathf.Sin(radians), Mathf.Cos(radians) }, Scale = new[] { 1f, 1f, 1f } };
            }
            tracks.Add(new PoseTrack { Node = nodes[shoulder].Id, Keys = keys });
        }

        private static void AddBlock(List<SceneNode> nodes, List<MeshInstance> meshes, string id, string label, int parent, float x, float y, float z, float width, float height, float depth, int material)
        {
            int node = AddNode(nodes, id, label, parent, x, y, z, width, height, depth);
            meshes.Add(new MeshInstance { Node = node, Geometry = "block", Bones = new int[0], Materials = new[] { material } });
        }

        private static int AddNode(List<SceneNode> nodes, string id, string label, int parent, float x, float y, float z, float width, float height, float depth)
        {
            nodes.Add(new SceneNode { Id = id, Label = label, Parent = parent, Position = new[] { x, y, z }, Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { width, height, depth } });
            return nodes.Count - 1;
        }
    }
}
