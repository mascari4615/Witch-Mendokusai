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
    public static class DynamicsExample
    {
        public static string Create()
        {
            string source = AuthoringExample.Create();
            string directory = Path.GetDirectoryName(source);
            SequenceDocument example = SequenceFiles.Read(source);
            ContentResource block = example.Scene.Resources[0];
            byte[] sphere = Sphere();
            File.WriteAllBytes(Path.Combine(directory, "sphere.geometry"), sphere);
            string hash;
            using (SHA256 algorithm = SHA256.Create()) hash = BitConverter.ToString(algorithm.ComputeHash(sphere)).Replace("-", "").ToLowerInvariant();
            List<SceneNode> nodes = new List<SceneNode>(); List<MeshInstance> meshes = new List<MeshInstance>(); List<PoseTrack> tracks = new List<PoseTrack>();
            for (int actor = 0; actor < 2; actor++)
            {
                string prefix = actor == 0 ? "spring" : "direct";
                float x = actor == 0 ? .65f : -.65f;
                int pivot = Add(nodes, prefix + "-anchor", actor == 0 ? "Spring motion" : "Direct motion", -1, x, 1.6f, 1, 1, 1);
                int joint = Add(nodes, prefix + "-joint", "Chain start", pivot, 0, 0, 1, 1, 1);
                int middle = Add(nodes, prefix + "-middle", "Middle", joint, 0, -.5f, 1, 1, 1);
                int tip = Add(nodes, prefix + "-tip", "Chain end", middle, 0, -.4f, 1, 1, 1);
                int upperMesh = Add(nodes, prefix + "-upper-view", "", joint, 0, -.25f, .045f, .5f, .045f);
                int lowerMesh = Add(nodes, prefix + "-lower-view", "", middle, 0, -.2f, .045f, .4f, .045f);
                int tipMesh = Add(nodes, prefix + "-tip-view", "", tip, 0, 0, .075f, .075f, .075f);
                foreach (int node in new[] { upperMesh, lowerMesh }) meshes.Add(new MeshInstance { Node = node, Geometry = block.Id, Bones = new int[0], Materials = new[] { actor } });
                meshes.Add(new MeshInstance { Node = tipMesh, Geometry = "sphere", Bones = new int[0], Materials = new[] { actor } });
                int obstacle = Add(nodes, prefix + "-guard", "Collider", -1, x + .25f, .75f, 1, 1, 1);
                int obstacleView = Add(nodes, prefix + "-guard-view", "", obstacle, 0, 0, .17f, .17f, .17f);
                meshes.Add(new MeshInstance { Node = obstacleView, Geometry = "sphere", Bones = new int[0], Materials = new[] { 2 } });
                float[] offsets = { 0, .3f, 0, -.3f, 0, 0 };
                float[] times = { 0, .5f, 1, 1.5f, 2, 4 };
                PoseKey[] keys = new PoseKey[times.Length];
                for (int key = 0; key < keys.Length; key++) keys[key] = new PoseKey { Time = times[key], Position = new[] { x + offsets[key], 1.6f, 0f }, Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } };
                tracks.Add(new PoseTrack { Node = nodes[pivot].Id, Keys = keys });
            }
            SceneDocument scene = new SceneDocument
            {
                Id = "secondary-motion-example", Nodes = nodes.ToArray(), Meshes = meshes.ToArray(),
                Materials = new[]
                {
                    new SurfaceMaterial { BaseColor = new[] { 1f, .65f, .15f, 1f }, DoubleSided = true },
                    new SurfaceMaterial { BaseColor = new[] { .15f, .8f, .8f, 1f }, DoubleSided = true },
                    new SurfaceMaterial { BaseColor = new[] { .3f, .36f, .45f, 1f }, DoubleSided = true }
                },
                Resources = new[] { block, new ContentResource { Id = "sphere", Path = "sphere.geometry", MediaType = "application/vnd.native.geometry", Sha256 = hash } },
                Colliders = new[] { new SceneCollider { Id = "guard", Node = "spring-guard", Radius = .17f } }
            };
            SceneSpringAuthoring.SetChain(scene, "spring-joint", "spring-tip", new SpringDistribution { FrequencyStart = 2, FrequencyEnd = 3, RadiusStart = .075f, RadiusEnd = .075f });
            foreach (SceneSpring spring in scene.Springs) spring.Colliders = new[] { "guard" };
            string file = Path.Combine(directory, "secondary-motion.sequence.json");
            SequenceFiles.Save(file, new SequenceDocument { Id = "secondary-motion", Scene = scene, Clips = new[] { new PoseClip { Id = "anchors", Duration = 4, Tracks = tracks.ToArray() } } }, new DirectoryContentSource(directory));
            return file;
        }

        private static int Add(List<SceneNode> nodes, string id, string label, int parent, float x, float y, float sx, float sy, float sz)
        {
            nodes.Add(new SceneNode { Id = id, Label = label, Parent = parent, Position = new[] { x, y, 0f }, Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { sx, sy, sz } });
            return nodes.Count - 1;
        }

        private static byte[] Sphere()
        {
            List<float> vertices = new List<float>(); List<float> uv = new List<float>(); List<int> triangles = new List<int>();
            for (int ring = 0; ring <= 12; ring++)
            {
                float latitude = (float)Math.PI * ring / 12;
                for (int sector = 0; sector <= 24; sector++)
                {
                    float longitude = 2 * (float)Math.PI * sector / 24;
                    vertices.Add((float)(Math.Sin(latitude) * Math.Cos(longitude))); vertices.Add((float)Math.Cos(latitude)); vertices.Add((float)(Math.Sin(latitude) * Math.Sin(longitude)));
                    uv.Add(sector / 24f); uv.Add(ring / 12f);
                    if (ring < 12 && sector < 24)
                    {
                        int index = ring * 25 + sector;
                        triangles.AddRange(new[] { index, index + 25, index + 1, index + 1, index + 25, index + 26 });
                    }
                }
            }
            return GeometryBinary.Write(new MeshGeometry { Positions = vertices.ToArray(), Normals = vertices.ToArray(), Uv = uv.ToArray(), BoneIndices = new int[0], BoneWeights = new float[0], BindPoses = new float[0], Submeshes = new[] { triangles.ToArray() } });
        }
    }
}
