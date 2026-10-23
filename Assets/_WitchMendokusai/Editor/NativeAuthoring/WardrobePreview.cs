using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    // Draws the character close to the head into a small texture, so the wardrobe window shows the result of a click without the stage window.
    public sealed class WardrobePreview
    {
        private const float FIELD_OF_VIEW = 25f;
        private const float HEAD_DROP = 0.12f;
        private const float MIN_DISTANCE = 0.6f;
        private const float DISTANCE_PER_SIZE = 2.6f;
        private const float PART_MIN_SIZE = 0.3f;
        private static readonly Vector3 VIEW_DIRECTION = new Vector3(.35f, .12f, 1f);
        private static readonly Color BACKGROUND = new Color(.15f, .17f, .2f);
        private RenderTexture texture;

        // Frames the worn part when there is one, otherwise the top of the character where the head is.
        public Texture Render(ContentWorkspace workspace, bool wearing, int size)
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
            camera.backgroundColor = BACKGROUND;
            camera.transform.position = center + VIEW_DIRECTION.normalized * Mathf.Max(MIN_DISTANCE, extent * DISTANCE_PER_SIZE);
            camera.transform.LookAt(center);
            camera.Render();
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
    }
}
