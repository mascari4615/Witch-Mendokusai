using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Wardrobe;
using Wardrobe.Serialization;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed class WardrobePanel
    {
        private string file;
        private WardrobeDocument document;
        private string worn;
        private bool expanded = true;

        public string File { get { return file; } }

        public void Open(string path)
        {
            WardrobeDocument loaded = WardrobeJson.Read(System.IO.File.ReadAllText(path));
            file = Path.GetFullPath(path);
            document = loaded;
            worn = null;
        }

        public void Draw(Action<string> open, Action<string> wear, Action takeOff)
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
            foreach (Outfit outfit in document.Outfits)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.IsNullOrEmpty(outfit.Label) ? outfit.Id : outfit.Label, worn == outfit.Id ? EditorStyles.boldLabel : EditorStyles.label);
                    using (new EditorGUI.DisabledScope(worn == outfit.Id))
                    {
                        if (GUILayout.Button(AuthoringText.Get("Wear"), GUILayout.Width(70)))
                        {
                            wear(outfit.Id);
                            worn = outfit.Id;
                        }
                    }
                }
            }
            if (GUILayout.Button(AuthoringText.Get("Take everything off")))
            {
                takeOff();
                worn = null;
            }
        }
    }
}
