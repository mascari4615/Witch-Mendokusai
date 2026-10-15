using System;
using System.IO;
using SceneContent;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed class ContentAuthoringWindow : EditorWindow
    {
        private ContentWorkspace workspace;
        private ContentStageView stage;
        private Vector2 scroll;
        private string filter = "";
        private string message = "Open a scene document to begin.";
        private bool failure;
        private int selected = -1;
        private MotionClipPanel motion;
        private ExpressionPanel expressions;
        private bool labeledOnly;
        private LimbPanel limbs;
        private Vector2 bodyScroll;

        public ContentWorkspace Workspace { get { return workspace; } }

        [MenuItem("WM/Authoring/Scene workspace")]
        public static ContentAuthoringWindow Open()
        {
            return GetWindow<ContentAuthoringWindow>("Scene workspace");
        }

        private void OnEnable()
        {
            workspace = new ContentWorkspace();
            minSize = new Vector2(380, 480);
            motion = new MotionClipPanel();
            expressions = new ExpressionPanel();
            limbs = new LimbPanel(motion.RecordNodes);
            Undo.undoRedoPerformed += Repaint;
            EditorApplication.delayCall += RestoreLastScene;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
            EditorApplication.delayCall -= RestoreLastScene;
            motion.Dispose();
            limbs.Dispose();
            if (stage != null)
            {
                stage.Close();
            }
            if (workspace != null)
            {
                workspace.Dispose();
            }
        }

        public void LoadFile(string file)
        {
            workspace.Load(file);
            motion.Bind(workspace, Repaint);
            expressions.Bind(workspace);
            limbs.Bind(workspace);
            labeledOnly = Array.Exists(workspace.Document.Nodes, node => string.IsNullOrEmpty(node.Label) == false);
            selected = -1;
            failure = false;
            message = workspace.Document.Nodes.Length + " nodes, " + workspace.Document.Meshes.Length + " meshes. Select a node to pose it.";
            EditorPrefs.SetString(Application.dataPath + "/ContentWorkspace/LastScene", workspace.FilePath);
            if (stage == null)
            {
                stage = CreateInstance<ContentStageView>();
                stage.titleContent = new GUIContent("Content stage");
                stage.Show();
            }
            stage.ShowPreview(workspace.PreviewScene);
            stage.sceneLighting = false;
            stage.Frame(workspace.GetBounds(), true);
            Repaint();
        }

        private void RestoreLastScene()
        {
            string file = EditorPrefs.GetString(Application.dataPath + "/ContentWorkspace/LastScene", "");
            if (workspace.IsLoaded == false && File.Exists(file))
            {
                Run(delegate { LoadFile(file); });
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Open scene...", EditorStyles.toolbarButton))
            {
                string file = EditorUtility.OpenFilePanel("Open scene document", "", "json");
                if (string.IsNullOrEmpty(file) == false)
                {
                    Run(delegate { LoadFile(file); });
                }
            }
            using (new EditorGUI.DisabledScope(workspace.IsLoaded == false))
            {
                if (GUILayout.Button("Save as...", EditorStyles.toolbarButton))
                {
                    string file = EditorUtility.SaveFilePanel("Save scene and resources", Path.GetDirectoryName(workspace.FilePath), "scene-edited", "json");
                    if (string.IsNullOrEmpty(file) == false)
                    {
                        Run(delegate { workspace.Save(file); message = "Saved scene and verified resources."; });
                    }
                }
                if (GUILayout.Button("Frame", EditorStyles.toolbarButton) && stage != null)
                {
                    stage.Frame(workspace.GetBounds(), true);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(message, failure ? MessageType.Error : MessageType.Info);
            if (workspace.IsLoaded == false)
            {
                EditorGUILayout.LabelField("Pose and expression editing", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Open a native scene, select a node, and use the stage handles or the fields below. Save a separate copy for your game.", EditorStyles.wordWrappedLabel);
                return;
            }
            bodyScroll = EditorGUILayout.BeginScrollView(bodyScroll);
            filter = EditorGUILayout.TextField("Find node", filter);
            labeledOnly = EditorGUILayout.Toggle("Named controls only", labeledOnly);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(position.height * 0.38f));
            for (int index = 0; index < workspace.Document.Nodes.Length; index++)
            {
                SceneNode node = workspace.Document.Nodes[index];
                if (labeledOnly && string.IsNullOrEmpty(node.Label))
                {
                    continue;
                }
                string label = string.IsNullOrEmpty(node.Label) ? node.Id : node.Label;
                if (label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 && node.Id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                if (GUILayout.Toggle(selected == index, new GUIContent(label, node.Id), "Button"))
                {
                    if (selected != index)
                    {
                        selected = index;
                        Selection.activeTransform = workspace.Instance.FindNode(node.Id);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
            motion.Draw(selected < 0 ? null : workspace.Document.Nodes[selected].Id);
            DrawSelected();
            limbs.Draw();
            expressions.Draw();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSelected()
        {
            if (selected < 0)
            {
                return;
            }
            Transform target = workspace.Instance.FindNode(workspace.Document.Nodes[selected].Id);
            EditorGUILayout.LabelField("Local pose", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            Vector3 positionValue = EditorGUILayout.Vector3Field("Position", target.localPosition);
            Vector3 euler = EditorGUILayout.Vector3Field("Rotation", target.localEulerAngles);
            Vector3 scale = EditorGUILayout.Vector3Field("Scale", target.localScale);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Change node pose");
                target.localPosition = positionValue;
                target.localRotation = Quaternion.Euler(euler);
                target.localScale = scale;
                SceneView.RepaintAll();
            }
        }

        private void Run(Action action)
        {
            try
            {
                action();
                failure = false;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                failure = true;
                Debug.LogException(exception);
            }
        }
    }
}
