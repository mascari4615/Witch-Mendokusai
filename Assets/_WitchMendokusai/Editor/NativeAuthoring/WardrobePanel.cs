using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Wardrobe;
using Wardrobe.Serialization;

namespace WitchMendokusai.ContentAuthoring
{
    [Serializable]
    public sealed class WardrobePanel
    {
        private const float MIN_CELL_WIDTH = 96f;
        private const float THUMB_RATIO = 0.8f;
        [SerializeField] private float nameHeight = 18f;
        private const float CELL_GAP = 4f;
        private const float SIDE_ROOM = 28f;
        private const string SEARCH_CONTROL = "wardrobe-search";

        private string file;
        private WardrobeDocument document;
        private readonly Dictionary<string, string> worn = new Dictionary<string, string>();
        private readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();
        private int actor;
        private bool expanded = true;
        private bool stale = true;
        private string query = "";
        private bool wornOnly;
        private int category;
        private int pictureSet;
        private string[] pictureSets = new string[0];

        public string File { get { return file; } }
        public bool HasDocument { get { return document != null; } }
        // Set by the window that owns the panel so a button can open the larger wardrobe window.
        public Action OpenWindow;
        public bool FocusSearch;

        public void Open(string path)
        {
            WardrobeDocument loaded = WardrobeJson.Read(System.IO.File.ReadAllText(path));
            file = Path.GetFullPath(path);
            document = loaded;
            worn.Clear();
            ClearThumbs();
            pictureSets = FindPictureSets();
            pictureSet = 0;
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

        // Picture sets are the folders under thumbs/ beside the outfit file, one per character the pictures were taken on. Loose pictures directly in thumbs/ count as a set named after the folder.
        private string[] FindPictureSets()
        {
            string directory = Path.Combine(Path.GetDirectoryName(file), "thumbs");
            List<string> names = new List<string>();
            if (Directory.Exists(directory) == false) return names.ToArray();
            foreach (string sub in Directory.GetDirectories(directory)) names.Add(Path.GetFileName(sub));
            names.Sort(StringComparer.Ordinal);
            if (names.Count == 0 && Directory.GetFiles(directory, "*.png").Length > 0) names.Add("");
            return names.ToArray();
        }

        // A picture of each outfit sits in the chosen picture set as <outfit id>.png. An outfit without one is drawn as a plain tile.
        private Texture2D Thumb(string outfitId)
        {
            Texture2D texture;
            if (thumbs.TryGetValue(outfitId, out texture)) return texture;
            string set = pictureSet < pictureSets.Length ? pictureSets[pictureSet] : "";
            string path = Path.Combine(Path.Combine(Path.Combine(Path.GetDirectoryName(file), "thumbs"), set), outfitId + ".png");
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
            if (OpenWindow != null && GUILayout.Button(AuthoringText.Get("Open wardrobe window"))) OpenWindow();
            EditorGUILayout.LabelField(AuthoringText.Get("Quick search"), EditorStyles.miniLabel);
            DrawBrowser(actors, wear, takeOff, wearingOf, EditorGUIUtility.currentViewWidth);
        }

        // The character choice, the worn row, search with categories, and the picture tiles, drawn at the given width.
        public void DrawBrowser(string[] actors, Func<string, string, bool> wear, Func<string, bool> takeOff, Func<WardrobeDocument, string, string> wearingOf, float width)
        {
            if (document == null || actors.Length == 0) return;
            if (stale) Restore(actors, wearingOf);
            actor = Mathf.Clamp(actor, 0, actors.Length - 1);
            if (actors.Length > 1)
            {
                string[] labels = new string[actors.Length];
                for (int index = 0; index < labels.Length; index++) labels[index] = CharacterLabel(actors[index]);
                actor = EditorGUILayout.Popup(AuthoringText.Get("Character"), actor, labels);
            }
            if (pictureSets.Length > 1)
            {
                int chosen = EditorGUILayout.Popup(AuthoringText.Get("Picture set"), pictureSet, pictureSets);
                if (chosen != pictureSet)
                {
                    pictureSet = chosen;
                    ClearThumbs();
                }
            }
            string current = actors[actor];
            string wearing;
            worn.TryGetValue(current, out wearing);
            DrawWorn(current, wearing, takeOff);
            DrawFilters();
            DrawGrid(current, wearing, wear, takeOff, width);
        }

        // The outfit id the chosen character wears, or null. The wardrobe window frames its preview on it.
        public string CurrentWorn(string[] actors)
        {
            if (actors.Length == 0) return null;
            string wearing;
            worn.TryGetValue(actors[Mathf.Clamp(actor, 0, actors.Length - 1)], out wearing);
            return wearing;
        }

        // Outfit names end in a number ("headtop 01"), so the text before it names the group.
        private static string CategoryOf(Outfit outfit)
        {
            return LabelOf(outfit).TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ' ', '-', '_');
        }

        private List<string> Categories()
        {
            List<string> names = new List<string>();
            foreach (Outfit outfit in document.Outfits)
            {
                string name = CategoryOf(outfit);
                if (name.Length > 0 && names.Contains(name) == false) names.Add(name);
            }
            return names;
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
            GUI.SetNextControlName(SEARCH_CONTROL);
            query = EditorGUILayout.TextField(AuthoringText.Get("Search"), query);
            if (FocusSearch)
            {
                EditorGUI.FocusTextInControl(SEARCH_CONTROL);
                FocusSearch = false;
            }
            string[] tabs = { AuthoringText.Get("All") + " " + document.Outfits.Length, AuthoringText.Get("Worn only") };
            wornOnly = GUILayout.Toolbar(wornOnly ? 1 : 0, tabs) == 1;
            List<string> groups = Categories();
            if (groups.Count < 2) { category = 0; return; }
            string[] names = new string[groups.Count + 1];
            names[0] = AuthoringText.Get("All");
            for (int index = 0; index < groups.Count; index++) names[index + 1] = groups[index];
            category = Mathf.Clamp(category, 0, groups.Count);
            category = GUILayout.SelectionGrid(category, names, 3, EditorStyles.miniButton);
        }

        // Picture tiles. A click wears the outfit, and a click on the worn tile takes it off.
        private void DrawGrid(string current, string wearing, Func<string, string, bool> wear, Func<string, bool> takeOff, float width)
        {
            string needle = (query ?? "").Trim();
            List<Outfit> shown = new List<Outfit>();
            List<string> groups = Categories();
            string group = category > 0 && category <= groups.Count ? groups[category - 1] : null;
            foreach (Outfit outfit in document.Outfits)
            {
                if (wornOnly && outfit.Id != wearing) continue;
                if (group != null && CategoryOf(outfit) != group) continue;
                if (needle.Length > 0 && LabelOf(outfit).IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) continue;
                shown.Add(outfit);
            }
            if (shown.Count == 0)
            {
                EditorGUILayout.LabelField(AuthoringText.Get("No outfit matches"), EditorStyles.miniLabel);
                return;
            }
            float room = width - SIDE_ROOM;
            int columns = Mathf.Max(1, Mathf.FloorToInt((room + CELL_GAP) / (MIN_CELL_WIDTH + CELL_GAP)));
            float cellWidth = (room - CELL_GAP * (columns - 1)) / columns;
            int rows = (shown.Count + columns - 1) / columns;
            for (int row = 0; row < rows; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < columns && row * columns + column < shown.Count; column++)
                    {
                        DrawCell(shown[row * columns + column], cellWidth, current, wearing, wear, takeOff);
                    }
                }
            }
        }

        private void DrawCell(Outfit outfit, float cellWidth, string current, string wearing, Func<string, string, bool> wear, Func<string, bool> takeOff)
        {
            float thumbHeight = cellWidth * THUMB_RATIO;
            float cellHeight = thumbHeight + nameHeight;
            Rect cell = GUILayoutUtility.GetRect(cellWidth, cellHeight, GUILayout.Width(cellWidth), GUILayout.Height(cellHeight));
            bool on = outfit.Id == wearing;
            EditorGUI.DrawRect(cell, on ? new Color(.42f, .77f, .55f) : new Color(.23f, .25f, .29f));
            Rect picture = new Rect(cell.x + 2f, cell.y + 2f, cell.width - 4f, thumbHeight - 2f);
            Texture2D thumb = Thumb(outfit.Id);
            if (thumb != null) GUI.DrawTexture(picture, thumb, ScaleMode.ScaleAndCrop);
            else EditorGUI.DrawRect(picture, new Color(.13f, .15f, .17f));
            GUI.Label(new Rect(cell.x + 2f, cell.y + thumbHeight, cell.width - 4f, nameHeight), LabelOf(outfit), EditorStyles.miniLabel);
            if (GUI.Button(cell, GUIContent.none, GUIStyle.none) == false) return;
            if (on)
            {
                if (takeOff(current)) worn.Remove(current);
            }
            else if (wear(outfit.Id, current)) worn[current] = outfit.Id;
        }
    }
}
