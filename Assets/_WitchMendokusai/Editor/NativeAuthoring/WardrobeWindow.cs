using System;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    // The wardrobe in its own window: picture tiles with search and groups on the left, the character close-up on the right.
    // The quick variant is a small drop-down with only the search and tiles, opened with Ctrl+Space from the scene editor, and closes after one pick.
    public sealed class WardrobeWindow : EditorWindow
    {
        private const float PREVIEW_SIZE = 300f;
        private const float WIDE_WIDTH = 640f;
        private const float PADDING = 24f;
        private const int PREVIEW_PIXELS = 512;
        private static readonly Vector2 QUICK_SIZE = new Vector2(460f, 420f);

        private ContentAuthoringWindow owner;
        private readonly WardrobePreview preview = new WardrobePreview();
        private bool quick;
        private Vector2 scroll;

        public static void Open(ContentAuthoringWindow owner)
        {
            WardrobeWindow window = GetWindow<WardrobeWindow>(AuthoringText.Get("Wardrobe"));
            window.owner = owner;
            window.quick = false;
            window.minSize = new Vector2(420f, 420f);
            window.Show();
        }

        public static void OpenQuick(ContentAuthoringWindow owner)
        {
            WardrobeWindow window = CreateInstance<WardrobeWindow>();
            window.owner = owner;
            window.quick = true;
            window.titleContent = new GUIContent(AuthoringText.Get("Quick search"));
            owner.Wardrobe.FocusSearch = true;
            Rect anchor = new Rect(owner.position.x + owner.position.width * .5f, owner.position.y + 80f, 0f, 0f);
            window.ShowAsDropDown(anchor, QUICK_SIZE);
        }

        private void OnDisable()
        {
            preview.Release();
        }

        private void OnGUI()
        {
            if (owner == null)
            {
                Close();
                return;
            }
            if (quick && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                return;
            }
            WardrobePanel panel = owner.Wardrobe;
            ContentWorkspace workspace = owner.Workspace;
            if (workspace.IsLoaded == false || panel.HasDocument == false)
            {
                EditorGUILayout.LabelField(AuthoringText.Get("Open an outfit file to dress this character. The character needs body role bindings."), EditorStyles.wordWrappedLabel);
                return;
            }
            string[] actors = WorkspaceWardrobe.Actors(workspace);
            Func<string, string, bool> wear = (outfitId, actor) => Pick(outfitId, actor);
            Func<string, bool> takeOff = actor => owner.WearChecked(null, actor);
            Func<Wardrobe.WardrobeDocument, string, string> wearingOf = (document, actor) => WorkspaceWardrobe.Wearing(workspace, document, actor);
            bool wide = quick == false && position.width >= WIDE_WIDTH;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    panel.DrawBrowser(actors, wear, takeOff, wearingOf, wide ? position.width - PREVIEW_SIZE - PADDING : position.width);
                    EditorGUILayout.EndScrollView();
                }
                if (wide) DrawPreview(panel, workspace, actors);
            }
        }

        // A pick in the drop-down closes it after the outfit changed.
        private bool Pick(string outfitId, string actor)
        {
            bool done = owner.WearChecked(outfitId, actor);
            if (done && quick) EditorApplication.delayCall += Close;
            return done;
        }

        private void DrawPreview(WardrobePanel panel, ContentWorkspace workspace, string[] actors)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(PREVIEW_SIZE)))
            {
                Rect area = GUILayoutUtility.GetRect(PREVIEW_SIZE, PREVIEW_SIZE, GUILayout.Width(PREVIEW_SIZE), GUILayout.Height(PREVIEW_SIZE));
                if (Event.current.type != EventType.Repaint || workspace.IsLoaded == false) return;
                GUI.DrawTexture(area, preview.Render(workspace, panel.CurrentWorn(actors) != null, PREVIEW_PIXELS), ScaleMode.ScaleToFit);
            }
        }
    }
}
