using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WitchMendokusai.ContentRuntime;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed class ExpressionPanel
    {
        private SkinnedMeshRenderer[] skins = new SkinnedMeshRenderer[0];
        private string[] labels = new string[0];
        private int selected;
        private string filter = "";
        private Vector2 scroll;
        private bool expanded;
        private readonly Action<string[]> record;
        private ContentWorkspace workspace;
        private int[] meshIndices;

        public ExpressionPanel(Action<string[]> recordValues)
        {
            record = recordValues;
        }

        public void Bind(ContentWorkspace workspace)
        {
            this.workspace = workspace;
            List<SkinnedMeshRenderer> available = new List<SkinnedMeshRenderer>();
            List<string> names = new List<string>();
            List<int> indices = new List<int>();
            selected = 0;
            int largest = 0;
            for (int mesh = 0; mesh < workspace.Document.Meshes.Length; mesh++)
            {
                SkinnedMeshRenderer skin = workspace.Instance.FindMesh(mesh) as SkinnedMeshRenderer;
                if (skin == null) continue;
                int count = skin.sharedMesh.blendShapeCount;
                if (count == 0)
                {
                    continue;
                }
                if (count > largest)
                {
                    largest = count;
                    selected = available.Count;
                }
                available.Add(skin);
                indices.Add(mesh);
                names.Add(skin.name + ", " + count + " controls");
            }
            skins = available.ToArray();
            labels = names.ToArray();
            meshIndices = indices.ToArray();
            filter = "";
        }

        public void Draw()
        {
            expanded = EditorGUILayout.Foldout(expanded, "Expressions", true);
            if (expanded == false)
            {
                return;
            }
            if (skins.Length == 0)
            {
                EditorGUILayout.LabelField("This scene has no expression controls.");
                return;
            }
            selected = EditorGUILayout.Popup("Surface", selected, labels);
            filter = EditorGUILayout.TextField("Find expression", filter);
            SkinnedMeshRenderer skin = skins[selected];
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(180));
            for (int index = 0; index < skin.sharedMesh.blendShapeCount; index++)
            {
                string label = skin.sharedMesh.GetBlendShapeName(index);
                if (label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                float weight = EditorGUILayout.Slider(label, skin.GetBlendShapeWeight(index), 0, 100);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(skin, "Change expression");
                    skin.SetBlendShapeWeight(index, weight);
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("Key", GUILayout.Width(38)))
                {
                    int mesh = meshIndices[selected];
                    string node = workspace.Document.Nodes[workspace.Document.Meshes[mesh].Node].Id;
                    record(new[] { SceneMotionBindings.Expression(node, mesh, label) });
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
