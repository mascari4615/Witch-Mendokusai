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
        private string file;
        private WardrobeDocument document;
        private readonly Dictionary<string, string> worn = new Dictionary<string, string>();
        private int actor;
        private bool expanded = true;

        public string File { get { return file; } }

        public void Open(string path)
        {
            WardrobeDocument loaded = WardrobeJson.Read(System.IO.File.ReadAllText(path));
            file = Path.GetFullPath(path);
            document = loaded;
            worn.Clear();
        }

        // Reads which outfit each character already wears from the scene, so opening an outfit file does not forget outfits that are on.
        public void Restore(string[] actors, Func<WardrobeDocument, string, string> wearingOf)
        {
            worn.Clear();
            foreach (string name in actors)
            {
                string outfit = wearingOf(document, name);
                if (outfit != null) worn[name] = outfit;
            }
        }

        private static string CharacterLabel(string name)
        {
            if (name.Length == 0) return AuthoringText.Get("Character") + " 1";
            return AuthoringText.Get("Character") + " " + (name.StartsWith("actor-", StringComparison.Ordinal) ? name.Substring(6) : name);
        }

        // wear and takeOff return whether the change happened, so a failed attempt is not shown as worn.
        public void Draw(string[] actors, Action<string> open, Func<string, string, bool> wear, Func<string, bool> takeOff)
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
            foreach (Outfit outfit in document.Outfits)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.IsNullOrEmpty(outfit.Label) ? outfit.Id : outfit.Label, wearing == outfit.Id ? EditorStyles.boldLabel : EditorStyles.label);
                    using (new EditorGUI.DisabledScope(wearing == outfit.Id))
                    {
                        if (GUILayout.Button(AuthoringText.Get("Wear"), GUILayout.Width(70)))
                        {
                            if (wear(outfit.Id, current)) worn[current] = outfit.Id;
                        }
                    }
                }
            }
            if (GUILayout.Button(AuthoringText.Get("Take everything off")))
            {
                if (takeOff(current)) worn.Remove(current);
            }
        }
    }
}
