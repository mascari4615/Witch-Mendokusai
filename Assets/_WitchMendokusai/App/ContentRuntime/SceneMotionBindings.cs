using System;
using Motion;
using Motion.Unity;
using SceneContent;
using SceneContent.Unity;
using UnityEngine;

namespace WitchMendokusai.ContentRuntime
{
    public static class SceneMotionBindings
    {
        public static string CameraField(string node, string field)
        {
            return "camera:" + node + ":" + field;
        }

        public static string LightField(string node, string field)
        {
            return "light:" + node + ":" + field;
        }

        public static string Expression(string node, int mesh, string shape)
        {
            return "morph:" + node + ":" + mesh + ":" + shape;
        }

        public static FloatPropertyTarget Create(SceneDocument document, Func<SceneInstance> instance)
        {
            FloatPropertyTarget values = new FloatPropertyTarget();
            foreach (SceneCamera binding in document.Cameras)
            {
                string node = document.Nodes[binding.Node].Id;
                Func<Camera> camera = () => instance().FindNode(node).GetComponent<Camera>();
                values.Bind(CameraField(node, "fov"), () => camera().fieldOfView, value => camera().fieldOfView = value, .01f, 179);
                values.Bind(CameraField(node, "size"), () => camera().orthographicSize, value => camera().orthographicSize = value, .00001f, 1000000);
            }
            foreach (SceneLight binding in document.Lights)
            {
                string node = document.Nodes[binding.Node].Id;
                Func<Light> light = () => instance().FindNode(node).GetComponent<Light>();
                values.Bind(LightField(node, "intensity"), () => light().intensity, value => light().intensity = value, 0, 1000000);
                for (int component = 0; component < 3; component++)
                {
                    int channel = component;
                    values.Bind(LightField(node, "color-" + channel), () => light().color[channel], value =>
                    {
                        Color color = light().color;
                        color[channel] = value;
                        light().color = color;
                    }, 0, 1);
                }
            }
            for (int mesh = 0; mesh < document.Meshes.Length; mesh++)
            {
                int meshIndex = mesh;
                SkinnedMeshRenderer skin = instance().FindMesh(mesh) as SkinnedMeshRenderer;
                if (skin == null)
                {
                    continue;
                }
                string node = document.Nodes[document.Meshes[mesh].Node].Id;
                for (int shape = 0; shape < skin.sharedMesh.blendShapeCount; shape++)
                {
                    int shapeIndex = shape;
                    string key = Expression(node, meshIndex, skin.sharedMesh.GetBlendShapeName(shape));
                    values.Bind(key, () => ((SkinnedMeshRenderer)instance().FindMesh(meshIndex)).GetBlendShapeWeight(shapeIndex) / 100f,
                        value => ((SkinnedMeshRenderer)instance().FindMesh(meshIndex)).SetBlendShapeWeight(shapeIndex, value * 100f), 0, 1);
                }
            }
            return values;
        }
    }
}
