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
        private int[] usesPerSurface = new int[0];
        private static readonly string[] LIGHTING = { "unlit", "lit", "toon" };
        private static readonly string[] ALPHA = { "opaque", "mask", "blend" };

        public void Bind(ContentWorkspace value, Action<Action<SceneDocument>> applyChange)
        {
            workspace = value;
            apply = applyChange;
            labels = new string[workspace.Document.Materials.Length];
            usesPerSurface = new int[labels.Length];
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
                usesPerSurface[index] = uses;
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
            apply(document =>
            {
                if (copy.ToonSurface != null || copy.MaterialClass != null || copy.TextureWrap != null) document.Version = Math.Max(document.Version, 2);
                document.Materials[index] = copy;
            });
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
            expanded = EditorGUILayout.Foldout(expanded, AuthoringText.Get("Surfaces"), true);
            if (expanded == false)
            {
                return;
            }
            if (draft == null)
            {
                EditorGUILayout.LabelField(AuthoringText.Get("This scene has no surfaces."));
                return;
            }
            EditorGUILayout.LabelField(AuthoringText.Get("Adjust values, then Apply. Reset discards unapplied values."), EditorStyles.wordWrappedLabel);
            for (int index = 0; index < labels.Length; index++)
            {
                labels[index] = AuthoringText.Format("Surface {0} ({1} slots)", index, usesPerSurface[index]);
            }
            int next = EditorGUILayout.Popup(AuthoringText.Get("Surface"), selected, labels);
            if (next != selected)
            {
                selected = next;
                ResetDraft();
            }
            if (GUILayout.Button(AuthoringText.Get("Select surface users")))
            {
                SelectUsers(selected);
            }
            Color color = EditorGUILayout.ColorField(AuthoringText.Get("Base color"), new Color(draft.BaseColor[0], draft.BaseColor[1], draft.BaseColor[2], draft.BaseColor[3]));
            draft.BaseColor = new[] { color.r, color.g, color.b, color.a };
            draft.LightingModel = LIGHTING[EditorGUILayout.Popup(AuthoringText.Get("Lighting"), Array.IndexOf(LIGHTING, draft.LightingModel), AuthoringText.Labels(LIGHTING))];
            if (draft.LightingModel == "toon")
            {
                if (draft.ToonSurface == null) draft.ToonSurface = new ToonSurface();
                DrawToon(draft.ToonSurface);
            }
            else draft.ToonSurface = null;
            draft.AlphaMode = ALPHA[EditorGUILayout.Popup(AuthoringText.Get("Alpha"), Array.IndexOf(ALPHA, draft.AlphaMode), AuthoringText.Labels(ALPHA))];
            draft.DoubleSided = EditorGUILayout.Toggle(AuthoringText.Get("Double sided"), draft.DoubleSided);
            using (new EditorGUI.DisabledScope(draft.LightingModel != "lit"))
            {
                draft.Roughness = EditorGUILayout.Slider(AuthoringText.Get("Roughness"), draft.Roughness, 0, 1);
                draft.Metallic = EditorGUILayout.Slider(AuthoringText.Get("Metallic"), draft.Metallic, 0, 1);
                Color emission = EditorGUILayout.ColorField(new GUIContent(AuthoringText.Get("Emission")), new Color(draft.Emission[0], draft.Emission[1], draft.Emission[2]), true, false, true);
                draft.Emission = new[] { emission.r, emission.g, emission.b };
            }
            Vector2 scale = EditorGUILayout.Vector2Field(AuthoringText.Get("Texture scale"), new Vector2(draft.TextureScale[0], draft.TextureScale[1]));
            Vector2 offset = EditorGUILayout.Vector2Field(AuthoringText.Get("Texture offset"), new Vector2(draft.TextureOffset[0], draft.TextureOffset[1]));
            draft.TextureScale = new[] { scale.x, scale.y };
            draft.TextureOffset = new[] { offset.x, offset.y };
            EditorGUILayout.LabelField(AuthoringText.Get("Texture"), string.IsNullOrEmpty(draft.BaseColorTexture) ? AuthoringText.Get("None") : draft.BaseColorTexture);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(AuthoringText.Get("Apply surface")))
            {
                Apply(selected, draft);
            }
            if (GUILayout.Button(AuthoringText.Get("Reset surface")))
            {
                ResetDraft();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawToon(ToonSurface surface)
        {
            Color shade = EditorGUILayout.ColorField(AuthoringText.Get("Shade tint"), new Color(surface.ShadeTint[0], surface.ShadeTint[1], surface.ShadeTint[2]));
            surface.ShadeTint = new[] { shade.r, shade.g, shade.b };
            surface.ShadeExtent = EditorGUILayout.Slider(AuthoringText.Get("Shade extent"), surface.ShadeExtent, 0, 1);
            surface.ShadeSoftness = EditorGUILayout.Slider(AuthoringText.Get("Shade softness"), surface.ShadeSoftness, .001f, .3f);
            surface.ShadeRichness = EditorGUILayout.Slider(AuthoringText.Get("Shade richness"), surface.ShadeRichness, 0, 1);
            surface.LightStrength = EditorGUILayout.Slider(AuthoringText.Get("Light strength"), surface.LightStrength, 0, 2);
            surface.AmbientStrength = EditorGUILayout.Slider(AuthoringText.Get("Ambient strength"), surface.AmbientStrength, 0, 2);
            surface.RimStrength = EditorGUILayout.Slider(AuthoringText.Get("Rim strength"), surface.RimStrength, 0, 1);
            surface.RimPower = EditorGUILayout.Slider(AuthoringText.Get("Rim power"), surface.RimPower, .5f, 8);
            surface.BandStrength = EditorGUILayout.Slider(AuthoringText.Get("Band strength"), surface.BandStrength, 0, 1);
            surface.BandHeight = EditorGUILayout.Slider(AuthoringText.Get("Band height"), surface.BandHeight, -1, 1);
            surface.BandWidth = EditorGUILayout.Slider(AuthoringText.Get("Band width"), surface.BandWidth, .02f, .5f);
            surface.OutlineWidth = EditorGUILayout.Slider(AuthoringText.Get("Outline width (screen fraction)"), surface.OutlineWidth, 0, .02f);
            Color outline = EditorGUILayout.ColorField(AuthoringText.Get("Outline tint"), new Color(surface.OutlineTint[0], surface.OutlineTint[1], surface.OutlineTint[2]));
            surface.OutlineTint = new[] { outline.r, outline.g, outline.b };
        }
    }
}
