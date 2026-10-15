using System;
using System.Collections.Generic;
using SceneContent;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed class SurfacePanel
    {
        private ContentWorkspace workspace;
        private Action<Action<SceneDocument>> apply;
        private SurfaceMaterial draft;
        private int selected;
        private bool expanded;
        private string[] labels = new string[0];
        private static readonly string[] LIGHTING = { "unlit", "lit" };
        private static readonly string[] ALPHA = { "opaque", "mask", "blend" };

        public void Bind(ContentWorkspace value, Action<Action<SceneDocument>> applyChange)
        {
            workspace = value;
            apply = applyChange;
            labels = new string[workspace.Document.Materials.Length];
            for (int index = 0; index < labels.Length; index++)
            {
                int uses = 0;
                foreach (MeshInstance mesh in workspace.Document.Meshes)
                {
                    foreach (int material in mesh.Materials)
                    {
                        if (material == index)
                        {
                            uses++;
                        }
                    }
                }
                labels[index] = "Surface " + index + " (" + uses + " slots)";
            }
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, labels.Length - 1));
            ResetDraft();
        }

        public void Apply(int index, SurfaceMaterial surface)
        {
            if (index < 0 || index >= workspace.Document.Materials.Length)
            {
                throw new ArgumentOutOfRangeException("index");
            }
            SurfaceMaterial copy = SceneCloning.CopySurface(surface);
            apply(document => document.Materials[index] = copy);
        }

        public void SelectUsers(int index)
        {
            if (index < 0 || index >= workspace.Document.Materials.Length)
            {
                throw new ArgumentOutOfRangeException("index");
            }
            List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
            for (int mesh = 0; mesh < workspace.Document.Meshes.Length; mesh++)
            {
                if (Array.IndexOf(workspace.Document.Meshes[mesh].Materials, index) >= 0)
                {
                    objects.Add(workspace.Instance.FindMesh(mesh).gameObject);
                }
            }
            Selection.objects = objects.ToArray();
        }

        private void ResetDraft()
        {
            draft = labels.Length == 0 ? null : SceneCloning.CopySurface(workspace.Document.Materials[selected]);
        }

        public void Draw()
        {
            expanded = EditorGUILayout.Foldout(expanded, "Surfaces", true);
            if (expanded == false)
            {
                return;
            }
            if (draft == null)
            {
                EditorGUILayout.LabelField("This scene has no surfaces.");
                return;
            }
            EditorGUILayout.LabelField("Adjust values, then Apply. Reset discards unapplied values.", EditorStyles.wordWrappedLabel);
            int next = EditorGUILayout.Popup("Surface", selected, labels);
            if (next != selected)
            {
                selected = next;
                ResetDraft();
            }
            if (GUILayout.Button("Select surface users"))
            {
                SelectUsers(selected);
            }
            Color color = EditorGUILayout.ColorField("Base color", new Color(draft.BaseColor[0], draft.BaseColor[1], draft.BaseColor[2], draft.BaseColor[3]));
            draft.BaseColor = new[] { color.r, color.g, color.b, color.a };
            draft.LightingModel = LIGHTING[EditorGUILayout.Popup("Lighting", Array.IndexOf(LIGHTING, draft.LightingModel), LIGHTING)];
            draft.AlphaMode = ALPHA[EditorGUILayout.Popup("Alpha", Array.IndexOf(ALPHA, draft.AlphaMode), ALPHA)];
            draft.DoubleSided = EditorGUILayout.Toggle("Double sided", draft.DoubleSided);
            using (new EditorGUI.DisabledScope(draft.LightingModel != "lit"))
            {
                draft.Roughness = EditorGUILayout.Slider("Roughness", draft.Roughness, 0, 1);
                draft.Metallic = EditorGUILayout.Slider("Metallic", draft.Metallic, 0, 1);
                Color emission = EditorGUILayout.ColorField(new GUIContent("Emission"), new Color(draft.Emission[0], draft.Emission[1], draft.Emission[2]), true, false, true);
                draft.Emission = new[] { emission.r, emission.g, emission.b };
            }
            Vector2 scale = EditorGUILayout.Vector2Field("Texture scale", new Vector2(draft.TextureScale[0], draft.TextureScale[1]));
            Vector2 offset = EditorGUILayout.Vector2Field("Texture offset", new Vector2(draft.TextureOffset[0], draft.TextureOffset[1]));
            draft.TextureScale = new[] { scale.x, scale.y };
            draft.TextureOffset = new[] { offset.x, offset.y };
            EditorGUILayout.LabelField("Texture", string.IsNullOrEmpty(draft.BaseColorTexture) ? "None" : draft.BaseColorTexture);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply surface"))
            {
                Apply(selected, draft);
            }
            if (GUILayout.Button("Reset surface"))
            {
                ResetDraft();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
