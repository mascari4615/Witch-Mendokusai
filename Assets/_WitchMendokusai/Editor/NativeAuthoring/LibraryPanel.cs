using System;
using System.IO;
using Rigging.Serialization;
using SceneContent;
using SceneContent.Storage;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed class LibraryPanel
    {
        private ContentLibrary library;
        private string[] entries = new string[0];
        private string[] names = new string[0];
        private LibraryUsage usage;
        private string error;
        private bool expanded;
        private Vector2 scroll;

        public LibraryPanel()
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WitchMendokusai", "ContentLibrary");
            library = new ContentLibrary(EditorPrefs.GetString("WM.ContentLibrary.Directory", directory));
            Run(Refresh);
        }

        public void Draw(Action<string> open)
        {
            expanded = EditorGUILayout.Foldout(expanded, "Shared content library", true);
            if (expanded == false)
            {
                return;
            }
            EditorGUILayout.LabelField("Same resource bytes are stored once, across scene variants.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Import scene..."))
            {
                string file = EditorUtility.OpenFilePanel("Import a native scene", "", "json");
                if (string.IsNullOrEmpty(file) == false)
                {
                    Run(delegate { Import(file); });
                }
            }
            if (GUILayout.Button("Choose library..."))
            {
                string directory = EditorUtility.OpenFolderPanel("Content library", library.Root, "");
                if (string.IsNullOrEmpty(directory) == false)
                {
                    library = new ContentLibrary(directory);
                    EditorPrefs.SetString("WM.ContentLibrary.Directory", directory);
                    Run(Refresh);
                }
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Refresh library"))
            {
                Run(Refresh);
            }
            EditorGUILayout.LabelField(usage.Documents + " scenes, " + usage.Objects + " unique resources, " + (usage.ResourceBytes / 1048576d).ToString("F2") + " MiB");
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(130));
            for (int index = 0; index < entries.Length; index++)
            {
                if (GUILayout.Button(new GUIContent(names[index], entries[index])))
                {
                    string file = entries[index];
                    Run(delegate { open(file); });
                }
            }
            EditorGUILayout.EndScrollView();
            if (string.IsNullOrEmpty(error) == false)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }

        public string Import(string file)
        {
            SceneDocument document = SceneFiles.Read(file);
            string directory = Path.GetDirectoryName(Path.GetFullPath(file));
            string rig = RigFiles.Read(file, document);
            string imported = library.Import(document, new DirectoryContentSource(directory));
            if (rig != null)
            {
                RigFiles.Save(imported, rig);
            }
            Refresh();
            return imported;
        }

        private void Refresh()
        {
            entries = library.Entries();
            names = new string[entries.Length];
            for (int index = 0; index < entries.Length; index++)
            {
                try
                {
                    SceneDocument scene = SceneFiles.Read(entries[index]);
                    names[index] = scene.Id + "  [" + Path.GetFileName(entries[index]).Substring(0, 8) + "]";
                }
                catch (Exception)
                {
                    names[index] = "Unreadable scene: " + Path.GetFileName(entries[index]);
                }
            }
            usage = library.Usage();
        }

        private void Run(Action action)
        {
            try
            {
                action();
                error = null;
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }
        }
    }
}
