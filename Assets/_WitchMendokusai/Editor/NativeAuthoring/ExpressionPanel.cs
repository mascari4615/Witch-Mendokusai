using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

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

        public void Bind(ContentWorkspace workspace)
        {
            List<SkinnedMeshRenderer> available = new List<SkinnedMeshRenderer>();
            List<string> names = new List<string>();
            selected = 0;
            int largest = 0;
            foreach (SkinnedMeshRenderer skin in workspace.Instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
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
                names.Add(skin.name + ", " + count + " controls");
            }
            skins = available.ToArray();
            labels = names.ToArray();
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
                float weight = EditorGUILayout.Slider(label, skin.GetBlendShapeWeight(index), 0, 100);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(skin, "Change expression");
                    skin.SetBlendShapeWeight(index, weight);
                    SceneView.RepaintAll();
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
