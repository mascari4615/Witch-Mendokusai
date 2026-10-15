using System;
using System.Collections.Generic;

namespace SceneContent
{
    public static class ScenePruning
    {
        public static SceneDocument Snapshot(SceneDocument source, IEnumerable<string> pinnedNodes)
        {
            SceneValidation.Ensure(source);
            if (pinnedNodes == null)
            {
                throw new ArgumentNullException("pinnedNodes");
            }
            bool[] retained = new bool[source.Nodes.Length];
            Dictionary<string, int> indices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < source.Nodes.Length; index++)
            {
                indices.Add(source.Nodes[index].Id, index);
            }
            foreach (string node in pinnedNodes)
            {
                int index;
                if (node == null || indices.TryGetValue(node, out index) == false)
                {
                    throw new ArgumentException("Pinned node does not exist: " + node);
                }
                Keep(source, retained, index);
            }
            foreach (MeshInstance mesh in source.Meshes)
            {
                Keep(source, retained, mesh.Node);
                Keep(source, retained, mesh.RootBone);
                foreach (int bone in mesh.Bones)
                {
                    Keep(source, retained, bone);
                }
            }
            foreach (SceneCamera camera in source.Cameras)
            {
                Keep(source, retained, camera.Node);
            }
            foreach (SceneLight light in source.Lights)
            {
                Keep(source, retained, light.Node);
            }
            int[] remap = new int[retained.Length];
            List<SceneNode> nodes = new List<SceneNode>();
            for (int index = 0; index < retained.Length; index++)
            {
                remap[index] = retained[index] ? nodes.Count : -1;
                if (retained[index])
                {
                    SceneNode original = source.Nodes[index];
                    nodes.Add(new SceneNode
                    {
                        Id = original.Id, Label = original.Label, Parent = original.Parent,
                        Position = (float[])original.Position.Clone(), Rotation = (float[])original.Rotation.Clone(), Scale = (float[])original.Scale.Clone()
                    });
                }
            }
            if (nodes.Count == 0)
            {
                throw new ArgumentException("Snapshot would contain no nodes; pin authored roots explicitly");
            }
            foreach (SceneNode node in nodes)
            {
                node.Parent = node.Parent < 0 ? -1 : remap[node.Parent];
            }
            MeshInstance[] meshes = new MeshInstance[source.Meshes.Length];
            for (int index = 0; index < meshes.Length; index++)
            {
                MeshInstance original = source.Meshes[index];
                int[] bones = new int[original.Bones.Length];
                for (int bone = 0; bone < bones.Length; bone++)
                {
                    bones[bone] = remap[original.Bones[bone]];
                }
                meshes[index] = new MeshInstance
                {
                    Node = remap[original.Node], RootBone = original.RootBone < 0 ? -1 : remap[original.RootBone],
                    Geometry = original.Geometry, Bones = bones, Materials = (int[])original.Materials.Clone(),
                    ShadowOnly = original.ShadowOnly, MorphWeights = (float[])original.MorphWeights.Clone()
                };
            }
            SceneDocument result = SceneCloning.Copy(source);
            result.Nodes = nodes.ToArray();
            result.Meshes = meshes;
            foreach (SceneCamera camera in result.Cameras)
            {
                camera.Node = remap[camera.Node];
            }
            foreach (SceneLight light in result.Lights)
            {
                light.Node = remap[light.Node];
            }
            SceneValidation.Ensure(result);
            return result;
        }

        private static void Keep(SceneDocument source, bool[] retained, int node)
        {
            while (node >= 0 && retained[node] == false)
            {
                retained[node] = true;
                node = source.Nodes[node].Parent;
            }
        }
    }
}
