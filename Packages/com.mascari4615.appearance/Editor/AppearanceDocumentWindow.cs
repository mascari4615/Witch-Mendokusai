using System;
using System.IO;
using Appearance.Serialization;
using UnityEditor;
using UnityEngine;

namespace Appearance.Editor
{
    public sealed class AppearanceDocumentWindow : EditorWindow
    {
        private AppearanceDocument document;
        private string error;
        private Vector2 scroll;

        [MenuItem("WM/Authoring/Appearance")]
        public static void Open()
        {
            GetWindow<AppearanceDocumentWindow>("Appearance");
        }

        public void LoadDocument(string path)
        {
            AppearanceDocument loaded = AppearanceJson.Read(File.ReadAllText(path));
            document = loaded;
            error = null;
            Repaint();
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Open appearance document"))
            {
                string path = EditorUtility.OpenFilePanel("Open appearance", "", "json");
                if (string.IsNullOrEmpty(path) == false)
                {
                    try
                    {
                        LoadDocument(path);
                    }
                    catch (Exception exception)
                    {
                        error = exception.Message;
                    }
                }
            }
            if (string.IsNullOrEmpty(error) == false)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
            if (document == null)
            {
                return;
            }
            EditorGUILayout.LabelField(document.DisplayName ?? document.Id, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Surface palette only. Geometry, rigging and materials are separate assets. Unspecified color spaces require calibration before rendering.", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (SurfaceColor color in document.Colors)
            {
                EditorGUILayout.LabelField(color.Surface + " / " + color.Channel);
                EditorGUILayout.LabelField(color.ColorSpace + ": " + string.Join(", ", color.Rgba));
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
