using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Wardrobe;
using Wardrobe.Serialization;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed class WardrobePanel
    {
        private const float CELL_WIDTH = 104f;
        private const float THUMB_HEIGHT = 84f;
        private const float NAME_HEIGHT = 18f;
        private const float GRID_MAX_HEIGHT = 360f;
        private const float CELL_GAP = 4f;
        private const float SCROLLBAR_ROOM = 24f;

        private string file;
        private WardrobeDocument document;
        private readonly Dictionary<string, string> worn = new Dictionary<string, string>();
        private readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();
        private int actor;
        private bool expanded = true;
        private bool stale = true;
        private string query = "";
        private bool wornOnly;
        private Vector2 scroll;

        public string File { get { return file; } }

        public void Open(string path)
        {
            WardrobeDocument loaded = WardrobeJson.Read(System.IO.File.ReadAllText(path));
            file = Path.GetFullPath(path);
            document = loaded;
            worn.Clear();
            ClearThumbs();
            stale = true;
        }

        // The scene changed under the panel (another scene was opened or the working scene was rebuilt), so the worn markers must be read again.
        public void Invalidate()
        {
            stale = true;
        }

        // Reads which outfit each character already wears from the scene, so opening an outfit file does not forget outfits that are on.
        public void Restore(string[] actors, Func<WardrobeDocument, string, string> wearingOf)
        {
            stale = false;
            worn.Clear();
            foreach (string name in actors)
            {
                string outfit = wearingOf(document, name);
                if (outfit != null) worn[name] = outfit;
            }
        }

        private void ClearThumbs()
        {
            foreach (Texture2D texture in thumbs.Values)
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
            thumbs.Clear();
        }

        // A picture of each outfit sits beside the outfit file as thumbs/<outfit id>.png. An outfit without one is drawn as a plain tile.
        private Texture2D Thumb(string outfitId)
        {
            Texture2D texture;
            if (thumbs.TryGetValue(outfitId, out texture)) return texture;
            string path = Path.Combine(Path.Combine(Path.GetDirectoryName(file), "thumbs"), outfitId + ".png");
            if (System.IO.File.Exists(path))
            {
                texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                texture.hideFlags = HideFlags.HideAndDontSave;
                if (texture.LoadImage(System.IO.File.ReadAllBytes(path)) == false)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    texture = null;
                }
            }
            thumbs[outfitId] = texture;
            return texture;
        }

        private static string CharacterLabel(string name)
        {
            if (name.Length == 0) return AuthoringText.Get("Character") + " 1";
            return AuthoringText.Get("Character") + " " + (name.StartsWith("actor-", StringComparison.Ordinal) ? name.Substring(6) : name);
        }

        private static string LabelOf(Outfit outfit)
        {
            return string.IsNullOrEmpty(outfit.Label) ? outfit.Id : outfit.Label;
        }

        // wear and takeOff return whether the change happened, so a failed attempt is not shown as worn.
        public void Draw(string[] actors, Action<string> open, Func<string, string, bool> wear, Func<string, bool> takeOff, Func<WardrobeDocument, string, string> wearingOf)
        {
            expanded = EditorGUILayout.Foldout(expanded, AuthoringText.Get("Outfits"), true);
            if (expanded == false) return;
            if (GUILayout.Button(AuthoringText.Get("Open outfit file...")))
            {
                string chosen = EditorUtility.OpenFilePanel(AuthoringText.Get("Open an outfit file"), "", "json");
                if (string.IsNullOrEmpty(chosen) == false) open(chosen);
            }
            if (document == null)
            {
                EditorGUILayout.LabelField(AuthoringText.Get("Open an outfit file to dress this character. The character needs body role bindings."), EditorStyles.wordWrappedLabel);
                return;
            }
            EditorGUILayout.LabelField(Path.GetFileName(file), EditorStyles.miniLabel);
            if (actors.Length == 0)
            {
                EditorGUILayout.LabelField(AuthoringText.Get("This scene has no body role bindings, so outfits cannot be worn."), EditorStyles.wordWrappedLabel);
                return;
            }
            if (stale) Restore(actors, wearingOf);
            actor = Mathf.Clamp(actor, 0, actors.Length - 1);
            if (actors.Length > 1)
            {
                string[] labels = new string[actors.Length];
                for (int index = 0; index < labels.Length; index++) labels[index] = CharacterLabel(actors[index]);
                actor = EditorGUILayout.Popup(AuthoringText.Get("Character"), actor, labels);
            }
            string current = actors[actor];
            string wearing;
            worn.TryGetValue(current, out wearing);
            DrawWorn(current, wearing, takeOff);
            DrawFilters();
            DrawGrid(current, wearing, wear, takeOff);
        }

        // One row above the grid: what is on now, with a button that takes it off.
        private void DrawWorn(string current, string wearing, Func<string, bool> takeOff)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (wearing == null)
                {
                    EditorGUILayout.LabelField(AuthoringText.Get("Nothing worn"), EditorStyles.miniLabel);
                    return;
                }
                Outfit outfit = Array.Find(document.Outfits, item => item.Id == wearing);
                EditorGUILayout.LabelField(AuthoringText.Get("Worn"), EditorStyles.miniLabel, GUILayout.Width(60));
                if (GUILayout.Button((outfit != null ? LabelOf(outfit) : wearing) + "  ×", EditorStyles.miniButton))
                {
                    if (takeOff(current)) worn.Remove(current);
                }
            }
        }

        private void DrawFilters()
        {
            query = EditorGUILayout.TextField(AuthoringText.Get("Search"), query);
            string[] tabs = { AuthoringText.Get("All") + " " + document.Outfits.Length, AuthoringText.Get("Worn only") };
            wornOnly = GUILayout.Toolbar(wornOnly ? 1 : 0, tabs) == 1;
        }

        // Picture tiles. A click wears the outfit, and a click on the worn tile takes it off.
        private void DrawGrid(string current, string wearing, Func<string, string, bool> wear, Func<string, bool> takeOff)
        {
            string needle = (query ?? "").Trim();
            List<Outfit> shown = new List<Outfit>();
            foreach (Outfit outfit in document.Outfits)
            {
                if (wornOnly && outfit.Id != wearing) continue;
                if (needle.Length > 0 && LabelOf(outfit).IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) continue;
                shown.Add(outfit);
            }
            if (shown.Count == 0)
            {
                EditorGUILayout.LabelField(AuthoringText.Get("No outfit matches"), EditorStyles.miniLabel);
                return;
            }
            int columns = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - SCROLLBAR_ROOM) / (CELL_WIDTH + CELL_GAP)));
            float cellHeight = THUMB_HEIGHT + NAME_HEIGHT;
            int rows = (shown.Count + columns - 1) / columns;
            float gridHeight = Mathf.Min(GRID_MAX_HEIGHT, rows * (cellHeight + CELL_GAP) + CELL_GAP);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(gridHeight));
            for (int row = 0; row < rows; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < columns && row * columns + column < shown.Count; column++)
                    {
                        DrawCell(shown[row * columns + column], cellHeight, current, wearing, wear, takeOff);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawCell(Outfit outfit, float cellHeight, string current, string wearing, Func<string, string, bool> wear, Func<string, bool> takeOff)
        {
            Rect cell = GUILayoutUtility.GetRect(CELL_WIDTH, cellHeight, GUILayout.Width(CELL_WIDTH), GUILayout.Height(cellHeight));
            bool on = outfit.Id == wearing;
            EditorGUI.DrawRect(cell, on ? new Color(.42f, .77f, .55f) : new Color(.23f, .25f, .29f));
            Rect picture = new Rect(cell.x + 2f, cell.y + 2f, cell.width - 4f, THUMB_HEIGHT - 2f);
            Texture2D thumb = Thumb(outfit.Id);
            if (thumb != null) GUI.DrawTexture(picture, thumb, ScaleMode.ScaleAndCrop);
            else EditorGUI.DrawRect(picture, new Color(.13f, .15f, .17f));
            GUI.Label(new Rect(cell.x + 2f, cell.y + THUMB_HEIGHT, cell.width - 4f, NAME_HEIGHT), LabelOf(outfit), EditorStyles.miniLabel);
            if (GUI.Button(cell, GUIContent.none, GUIStyle.none) == false) return;
            if (on)
            {
                if (takeOff(current)) worn.Remove(current);
            }
            else if (wear(outfit.Id, current)) worn[current] = outfit.Id;
        }
    }
}
