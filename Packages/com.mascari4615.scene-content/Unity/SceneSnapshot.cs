using System;
using UnityEngine;

namespace SceneContent.Unity
{
    public static class SceneSnapshot
    {
        public static SceneDocument Capture(SceneDocument source, SceneInstance instance)
        {
            if (source == null || instance == null)
            {
                throw new ArgumentNullException("Scene source and instance are required");
            }
            SceneNode[] nodes = new SceneNode[source.Nodes.Length];
            for (int index = 0; index < nodes.Length; index++)
            {
                Transform transform = instance.FindNode(source.Nodes[index].Id);
                Vector3 position = transform.localPosition;
                Quaternion rotation = transform.localRotation;
                Vector3 scale = transform.localScale;
                nodes[index] = new SceneNode
                {
                    Id = source.Nodes[index].Id,
                    Label = source.Nodes[index].Label,
                    Parent = source.Nodes[index].Parent,
                    Position = new[] { position.x, position.y, -position.z },
                    Rotation = new[] { -rotation.x, -rotation.y, rotation.z, rotation.w },
                    Scale = new[] { scale.x, scale.y, scale.z }
                };
            }
            MeshInstance[] meshes = new MeshInstance[source.Meshes.Length];
            for (int index = 0; index < meshes.Length; index++)
            {
                MeshInstance original = source.Meshes[index];
                Transform node = instance.FindNode(source.Nodes[original.Node].Id);
                Transform meshTransform = node.Find("mesh-" + index);
                if (meshTransform == null)
                {
                    throw new InvalidOperationException("Missing mesh instance: " + index);
                }
                SkinnedMeshRenderer skin = meshTransform.GetComponent<SkinnedMeshRenderer>();
                float[] weights = new float[original.MorphWeights.Length];
                for (int morph = 0; morph < weights.Length; morph++)
                {
                    weights[morph] = skin.GetBlendShapeWeight(morph) / 100f;
                }
                meshes[index] = new MeshInstance
                {
                    Node = original.Node, Geometry = original.Geometry, Bones = original.Bones,
                    Materials = original.Materials, ShadowOnly = original.ShadowOnly,
                    RootBone = original.RootBone, MorphWeights = weights
                };
            }
            SceneDocument snapshot = new SceneDocument
            {
                Id = source.Id, Nodes = nodes, Meshes = meshes,
                Materials = source.Materials, Resources = source.Resources
            };
            SceneValidation.Ensure(snapshot);
            return snapshot;
        }
    }
}
