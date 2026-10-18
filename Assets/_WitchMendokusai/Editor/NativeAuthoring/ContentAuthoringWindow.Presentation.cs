using KarmoLab.KarmoEditor;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed partial class ContentAuthoringWindow
    {
        private bool poseExpanded;
        private bool sceneLighting;
        private bool sceneIcons;
        private static readonly string[] LANGUAGES = { "한국어", "English" };

        private void DrawWelcome()
        {
            titleContent = new GUIContent(AuthoringText.Get("Scene workspace"));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(titleContent, EditorStyles.boldLabel);
            int language = EditorGUILayout.Popup((int)KarmoEditorLocalization.CurrentLanguage, LANGUAGES, GUILayout.Width(95));
            if (language != (int)KarmoEditorLocalization.CurrentLanguage)
            {
                KarmoEditorLocalization.CurrentLanguage = (KarmoEditorLocalization.Language)language;
                if (stage != null)
                {
                    stage.titleContent = new GUIContent(AuthoringText.Get("Content stage"));
                    stage.Repaint();
                }
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button(AuthoringText.Get("Open greeting example"), GUILayout.Height(32)))
            {
                if (workspace.IsLoaded == false || (workspace.HasChanges == false && motion.HasChanges == false) ||
                    EditorUtility.DisplayDialog(AuthoringText.Get("Unsaved workspace"), AuthoringText.Get("Discard current scene and motion edits?"), AuthoringText.Get("Discard"), AuthoringText.Get("Cancel")))
                {
                    Run(OpenExample);
                }
            }
            if (workspace.IsLoaded == false)
            {
                EditorGUILayout.LabelField(AuthoringText.Get("Two block figures wave. Open the example, then press Play in the character view."), EditorStyles.wordWrappedLabel);
            }
            if (GUILayout.Button(AuthoringText.Get("Open secondary motion example")))
            {
                if (workspace.IsLoaded == false || (workspace.HasChanges == false && motion.HasChanges == false) ||
                    EditorUtility.DisplayDialog(AuthoringText.Get("Unsaved workspace"), AuthoringText.Get("Discard current scene and motion edits?"), AuthoringText.Get("Discard"), AuthoringText.Get("Cancel"))) Run(OpenDynamicsExample);
            }
            using (new EditorGUI.DisabledScope(workspace.IsLoaded == false))
            {
                if (GUILayout.Button(AuthoringText.Get("Show character view"), GUILayout.Height(32))) ShowCharacterView();
            }
        }

        public void OpenExample()
        {
            LoadSequence(AuthoringExample.Create());
        }

        public void OpenDynamicsExample()
        {
            LoadSequence(DynamicsExample.Create());
            selected = System.Array.FindIndex(workspace.Document.Nodes, node => node.Id == "spring-joint");
            dynamics.Reveal();
            message = "Left: spring motion and collision. Right: direct motion. Press Play or move Time to compare.";
        }

        public void ShowCharacterView()
        {
            if (workspace.IsLoaded == false) return;
            if (stage == null)
            {
                stage = CreateInstance<ContentStageView>();
                stage.ShowUtility();
            }
            stage.titleContent = new GUIContent(AuthoringText.Get("Content stage"));
            stage.ShowPreview(workspace.PreviewScene);
            stage.sceneLighting = sceneLighting;
            stage.drawGizmos = sceneIcons;
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            stage.position = new Rect(main.x + 16, main.y + 64, Mathf.Max(420, main.width * .65f - 24), Mathf.Max(320, main.height - 100));
            stage.Focus();
            stage.rotation = Quaternion.Euler(5, 180, 0);
            stage.Frame(workspace.GetBounds(), true);
            stage.Repaint();
        }

        private void PrepareStage(SceneView view)
        {
            if (view == stage) stage.PreparePreviewCamera();
        }

        private void DrawPoseSelection()
        {
            poseExpanded = EditorGUILayout.Foldout(poseExpanded, AuthoringText.Get("Pose controls"), true);
            if (poseExpanded == false) return;
            EditorGUILayout.LabelField(AuthoringText.Get("Select a body part to edit its pose."), EditorStyles.wordWrappedLabel);
            DrawNodeList();
        }

        private void DrawStageControls(SceneView view)
        {
            if (view != stage || workspace == null || workspace.IsLoaded == false) return;
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12, 44, Mathf.Min(350, view.position.width - 24), 180), GUI.skin.box);
            GUILayout.Label(AuthoringText.Get("Content stage"), EditorStyles.boldLabel);
            motion.DrawTransport();
            sceneLighting = EditorGUILayout.Toggle(AuthoringText.Get("Use scene lighting"), sceneLighting);
            stage.sceneLighting = sceneLighting;
            sceneIcons = EditorGUILayout.Toggle(AuthoringText.Get("Show scene icons"), sceneIcons);
            stage.drawGizmos = sceneIcons;
            if (GUILayout.Button(AuthoringText.Get("Edit details"))) Focus();
            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
}
