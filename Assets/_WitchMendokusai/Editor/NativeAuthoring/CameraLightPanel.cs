using System;
using System.Collections.Generic;
using SceneContent;
using UnityEditor;
using UnityEngine;
using WitchMendokusai.ContentRuntime;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed class CameraLightPanel
    {
        private ContentWorkspace workspace;
        private Action<Action<SceneDocument>> change;
        private Func<ContentStageView> stage;
        private bool expanded;
        private int cameraIndex;
        private int lightIndex;
        private readonly Action<string[]> record;

        public CameraLightPanel(Action<string[]> recordValues)
        {
            record = recordValues;
        }

        public void Bind(ContentWorkspace value, Action<Action<SceneDocument>> changeDocument, Func<ContentStageView> getStage)
        {
            workspace = value;
            change = changeDocument;
            stage = getStage;
            cameraIndex = 0;
            lightIndex = 0;
        }

        public void Draw()
        {
            expanded = EditorGUILayout.Foldout(expanded, AuthoringText.Get("Cameras and lights"), true);
            if (expanded == false)
            {
                return;
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(AuthoringText.Get("Camera from view")))
            {
                AddCamera();
            }
            if (GUILayout.Button(AuthoringText.Get("Add key light")))
            {
                AddLight();
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button(AuthoringText.Get("Use lit surfaces")))
            {
                change(document =>
                {
                    foreach (SurfaceMaterial material in document.Materials)
                    {
                        material.LightingModel = "lit";
                    }
                });
                stage().sceneLighting = true;
            }
            DrawCamera();
            DrawLight();
        }

        private void AddCamera()
        {
            Camera view = stage().camera;
            SceneNode node = Node("Camera " + (workspace.Document.Cameras.Length + 1), view.transform.position, view.transform.rotation);
            change(document =>
            {
                List<SceneNode> nodes = new List<SceneNode>(document.Nodes);
                int index = nodes.Count;
                nodes.Add(node);
                document.Nodes = nodes.ToArray();
                List<SceneCamera> cameras = new List<SceneCamera>(document.Cameras);
                foreach (SceneCamera camera in cameras)
                {
                    camera.Enabled = false;
                }
                cameras.Add(new SceneCamera { Node = index, FieldOfView = view.fieldOfView, Projection = view.orthographic ? "orthographic" : "perspective", OrthographicSize = Mathf.Max(.001f, view.orthographicSize) });
                document.Cameras = cameras.ToArray();
            });
            cameraIndex = workspace.Document.Cameras.Length - 1;
        }

        private void AddLight()
        {
            SceneNode node = Node("Key light " + (workspace.Document.Lights.Length + 1), Vector3.zero, Quaternion.Euler(35, 145, 0));
            change(document =>
            {
                List<SceneNode> nodes = new List<SceneNode>(document.Nodes);
                int index = nodes.Count;
                nodes.Add(node);
                document.Nodes = nodes.ToArray();
                List<SceneLight> lights = new List<SceneLight>(document.Lights);
                lights.Add(new SceneLight { Node = index, Intensity = 1.5f, Shadows = true });
                document.Lights = lights.ToArray();
            });
            lightIndex = workspace.Document.Lights.Length - 1;
            stage().sceneLighting = true;
        }

        private void DrawCamera()
        {
            SceneCamera[] cameras = workspace.Document.Cameras;
            if (cameras.Length == 0)
            {
                return;
            }
            cameraIndex = Mathf.Clamp(cameraIndex, 0, cameras.Length - 1);
            cameraIndex = EditorGUILayout.Popup(AuthoringText.Get("Camera"), cameraIndex, Names(cameras));
            SceneCamera binding = cameras[cameraIndex];
            Transform node = workspace.Instance.FindNode(workspace.Document.Nodes[binding.Node].Id);
            Camera camera = node.GetComponent<Camera>();
            EditorGUI.BeginChangeCheck();
            bool orthographic = EditorGUILayout.Toggle(AuthoringText.Get("Orthographic"), camera.orthographic);
            float field = EditorGUILayout.Slider(AuthoringText.Get("Field of view"), camera.fieldOfView, 1, 179);
            float size = Mathf.Clamp(EditorGUILayout.FloatField(AuthoringText.Get("View size"), camera.orthographicSize), .001f, 1000000);
            float near = Mathf.Clamp(EditorGUILayout.FloatField(AuthoringText.Get("Near clip"), camera.nearClipPlane), .00001f, 999999);
            float far = Mathf.Clamp(EditorGUILayout.FloatField(AuthoringText.Get("Far clip"), camera.farClipPlane), near + .00001f, 10000000);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(camera, "Change camera lens");
                camera.orthographic = orthographic;
                camera.fieldOfView = field;
                camera.orthographicSize = size;
                camera.nearClipPlane = near;
                camera.farClipPlane = far;
            }
            if (GUILayout.Button(AuthoringText.Get("Select camera node")))
            {
                Selection.activeTransform = node;
            }
            if (GUILayout.Button(AuthoringText.Get("Use this camera in game")))
            {
                foreach (SceneCamera candidate in cameras)
                {
                    candidate.Enabled = candidate == binding;
                }
            }
            if (GUILayout.Button(AuthoringText.Get("Record lens key")))
            {
                string id = workspace.Document.Nodes[binding.Node].Id;
                record(new[] { SceneMotionBindings.CameraField(id, "fov"), SceneMotionBindings.CameraField(id, "size") });
            }
        }

        private void DrawLight()
        {
            SceneLight[] lights = workspace.Document.Lights;
            if (lights.Length == 0)
            {
                return;
            }
            lightIndex = Mathf.Clamp(lightIndex, 0, lights.Length - 1);
            string[] names = new string[lights.Length];
            for (int index = 0; index < names.Length; index++)
            {
                names[index] = workspace.Document.Nodes[lights[index].Node].Label;
            }
            lightIndex = EditorGUILayout.Popup(AuthoringText.Get("Light"), lightIndex, names);
            Light light = workspace.Instance.FindNode(workspace.Document.Nodes[lights[lightIndex].Node].Id).GetComponent<Light>();
            EditorGUI.BeginChangeCheck();
            Color color = EditorGUILayout.ColorField(AuthoringText.Get("Color"), light.color);
            float intensity = Mathf.Clamp(EditorGUILayout.FloatField(AuthoringText.Get("Intensity (relative)"), light.intensity), 0, 1000000);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(light, "Change scene light");
                light.color = color;
                light.intensity = intensity;
                SceneView.RepaintAll();
            }
            if (GUILayout.Button(AuthoringText.Get("Record light key")))
            {
                string id = workspace.Document.Nodes[lights[lightIndex].Node].Id;
                record(new[] { SceneMotionBindings.LightField(id, "intensity"), SceneMotionBindings.LightField(id, "color-0"), SceneMotionBindings.LightField(id, "color-1"), SceneMotionBindings.LightField(id, "color-2") });
            }
        }

        private string[] Names(SceneCamera[] cameras)
        {
            string[] names = new string[cameras.Length];
            for (int index = 0; index < names.Length; index++)
            {
                SceneNode node = workspace.Document.Nodes[cameras[index].Node];
                names[index] = string.IsNullOrEmpty(node.Label) ? node.Id : node.Label;
            }
            return names;
        }

        private static SceneNode Node(string label, Vector3 position, Quaternion rotation)
        {
            return new SceneNode
            {
                Id = Guid.NewGuid().ToString("N"), Label = label, Parent = -1,
                Position = new[] { position.x, position.y, -position.z }, Rotation = new[] { -rotation.x, -rotation.y, rotation.z, rotation.w }, Scale = new[] { 1f, 1f, 1f }
            };
        }
    }
}
