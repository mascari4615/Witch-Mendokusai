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
            SceneDocument snapshot = SceneCloning.Copy(source);
            snapshot.Nodes = nodes;
            snapshot.Meshes = meshes;
            foreach (SceneCamera binding in snapshot.Cameras)
            {
                Camera camera = instance.FindNode(snapshot.Nodes[binding.Node].Id).GetComponent<Camera>();
                binding.Projection = camera.orthographic ? "orthographic" : "perspective";
                binding.FieldOfView = camera.fieldOfView;
                binding.OrthographicSize = camera.orthographicSize;
                binding.Near = camera.nearClipPlane;
                binding.Far = camera.farClipPlane;
                Color color = camera.backgroundColor;
                binding.ClearColor = new[] { color.r, color.g, color.b, color.a };
            }
            foreach (SceneLight binding in snapshot.Lights)
            {
                Light light = instance.FindNode(snapshot.Nodes[binding.Node].Id).GetComponent<Light>();
                if (light.type != LightType.Directional && light.type != LightType.Point && light.type != LightType.Spot)
                {
                    throw new NotSupportedException("Scene snapshots support directional, point and spot lights");
                }
                binding.Kind = light.type == LightType.Directional ? "directional" : (light.type == LightType.Point ? "point" : "spot");
                Color color = light.color;
                binding.Color = new[] { color.r, color.g, color.b };
                binding.Intensity = light.intensity;
                binding.Range = light.range;
                binding.SpotAngle = light.spotAngle;
                binding.Shadows = light.shadows != LightShadows.None;
                binding.Enabled = light.enabled;
            }
            SceneValidation.Ensure(snapshot);
            return snapshot;
        }
    }
}
