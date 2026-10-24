using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace WitchMendokusai.ContentAuthoring
{
    // Draws the character close to the head into a small texture, so the wardrobe window shows the result of a click without the stage window.
    // The toon look swaps every surface for the toon shader and adds one key light for the duration of the draw, then puts everything back, so the scene is not changed.
    public sealed class WardrobePreview
    {
        private const float FIELD_OF_VIEW = 25f;
        private const float HEAD_DROP = 0.18f;
        private const float MIN_DISTANCE = 0.8f;
        private const float DISTANCE_PER_SIZE = 2.6f;
        private const float PART_MIN_SIZE = 0.3f;
        private const float KEY_LIGHT_INTENSITY = 0.95f;
        private const float SHADE_EXTENT = 0.5f;
        private const float RIM_STRENGTH = 0.12f;
        private const string TOON_SHADER = "WM/Toon Surface";
        private static readonly Vector3 VIEW_DIRECTION = new Vector3(.35f, .12f, 1f);
        private static readonly Color BACKGROUND = new Color(.15f, .17f, .2f);
        private static readonly Color TOON_BACKGROUND = new Color(.72f, .78f, .86f);
        private static readonly Color SHADE_TINT = new Color(.80f, .86f, .98f);
        private static readonly Color AMBIENT = new Color(.55f, .55f, .6f);
        private static readonly string[] COPIED_FLOATS = { "_Cull", "_Surface", "_AlphaClip", "_Cutoff", "_SrcBlend", "_DstBlend", "_ZWrite" };
        private RenderTexture texture;

        // Frames the worn part when there is one, otherwise the top of the character where the head is.
        public Texture Render(ContentWorkspace workspace, bool wearing, int size, bool toon)
        {
            if (texture == null || texture.width != size)
            {
                Release();
                texture = new RenderTexture(size, size, 24);
                texture.hideFlags = HideFlags.HideAndDontSave;
            }
            Vector3 center;
            float extent;
            Renderer part = wearing ? workspace.Instance.FindMesh(workspace.Document.Meshes.Length - 1) : null;
            if (part != null)
            {
                center = part.bounds.center;
                extent = Mathf.Max(PART_MIN_SIZE, part.bounds.size.magnitude);
            }
            else
            {
                Bounds bounds = workspace.GetBounds();
                center = new Vector3(bounds.center.x, bounds.max.y - HEAD_DROP, bounds.center.z);
                extent = PART_MIN_SIZE;
            }
            GameObject holder = new GameObject("wardrobe-preview-camera");
            holder.hideFlags = HideFlags.HideAndDontSave;
            holder.transform.SetParent(workspace.Instance.transform, false);
            Camera camera = holder.AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = workspace.PreviewScene;
            camera.cullingMask = -1;
            camera.targetTexture = texture;
            camera.fieldOfView = FIELD_OF_VIEW;
            camera.nearClipPlane = .01f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = toon ? TOON_BACKGROUND : BACKGROUND;
            camera.transform.position = center + VIEW_DIRECTION.normalized * Mathf.Max(MIN_DISTANCE, extent * DISTANCE_PER_SIZE);
            camera.transform.LookAt(center);
            Toon look = toon ? Toon.Apply(workspace) : null;
            camera.Render();
            if (look != null) look.Undo();
            camera.targetTexture = null;
            Object.DestroyImmediate(holder);
            return texture;
        }

        public void Release()
        {
            if (texture == null) return;
            texture.Release();
            Object.DestroyImmediate(texture);
            texture = null;
        }

        // The temporary changes of one toon draw. Undo restores every material array, the ambient light and the async compile setting.
        private sealed class Toon
        {
            private readonly List<Renderer> renderers = new List<Renderer>();
            private readonly List<Material[]> originals = new List<Material[]>();
            private readonly List<Material> temporary = new List<Material>();
            private GameObject lightObject;
            private AmbientMode ambientMode;
            private Color ambientLight;
            private bool asyncCompilation;

            public static Toon Apply(ContentWorkspace workspace)
            {
                Shader shader = Shader.Find(TOON_SHADER);
                if (shader == null) return null;
                Toon toon = new Toon();
                // The first frame of a new shader must not be skipped while it compiles in the background.
                toon.asyncCompilation = ShaderUtil.allowAsyncCompilation;
                ShaderUtil.allowAsyncCompilation = false;
                toon.ambientMode = RenderSettings.ambientMode;
                toon.ambientLight = RenderSettings.ambientLight;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = AMBIENT;
                toon.lightObject = new GameObject("wardrobe-preview-light");
                toon.lightObject.hideFlags = HideFlags.HideAndDontSave;
                Light light = toon.lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = KEY_LIGHT_INTENSITY;
                light.shadows = LightShadows.None;
                toon.lightObject.transform.rotation = Quaternion.Euler(35f, 145f, 0f);
                SceneManager.MoveGameObjectToScene(toon.lightObject, workspace.PreviewScene);
                foreach (Renderer renderer in workspace.Instance.GetComponentsInChildren<Renderer>())
                {
                    Material[] materials = renderer.sharedMaterials;
                    Material[] replaced = new Material[materials.Length];
                    for (int index = 0; index < materials.Length; index++) replaced[index] = Replace(materials[index], shader, toon.temporary);
                    toon.renderers.Add(renderer);
                    toon.originals.Add(materials);
                    renderer.sharedMaterials = replaced;
                }
                return toon;
            }

            private static Material Replace(Material source, Shader shader, List<Material> temporary)
            {
                if (source == null || source.HasProperty("_BaseColor") == false) return source;
                Material next = new Material(shader);
                next.hideFlags = HideFlags.HideAndDontSave;
                next.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                next.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                next.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                next.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
                foreach (string name in COPIED_FLOATS)
                {
                    if (source.HasProperty(name)) next.SetFloat(name, source.GetFloat(name));
                }
                if (source.IsKeywordEnabled("_ALPHATEST_ON")) next.EnableKeyword("_ALPHATEST_ON");
                next.renderQueue = source.renderQueue;
                next.SetColor("_ShadowColor", SHADE_TINT);
                next.SetFloat("_ShadowExtend", SHADE_EXTENT);
                next.SetFloat("_RimStrength", RIM_STRENGTH);
                temporary.Add(next);
                return next;
            }

            public void Undo()
            {
                for (int index = 0; index < renderers.Count; index++)
                {
                    if (renderers[index] != null) renderers[index].sharedMaterials = originals[index];
                }
                foreach (Material material in temporary) Object.DestroyImmediate(material);
                if (lightObject != null) Object.DestroyImmediate(lightObject);
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambientLight;
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
            }
        }
    }
}
