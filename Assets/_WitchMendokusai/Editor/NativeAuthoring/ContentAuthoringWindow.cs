using System;
using System.IO;
using SceneContent;
using SceneContent.Serialization;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed partial class ContentAuthoringWindow : EditorWindow
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
        private CameraLightPanel cameras;
        private LibraryPanel library;
        private double nextRecovery;
        private SurfacePanel surfaces;

        public ContentWorkspace Workspace { get { return workspace; } }
        public MotionClipPanel Motion { get { return motion; } }
        public SurfacePanel Surfaces { get { return surfaces; } }

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
            expressions = new ExpressionPanel(motion.RecordValues);
            limbs = new LimbPanel(motion.RecordNodes);
            cameras = new CameraLightPanel(motion.RecordValues);
            library = new LibraryPanel();
            surfaces = new SurfacePanel();
            Undo.undoRedoPerformed += Repaint;
            EditorApplication.delayCall += RestoreLastScene;
            EditorApplication.update += CheckpointRecovery;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
            EditorApplication.delayCall -= RestoreLastScene;
            EditorApplication.update -= CheckpointRecovery;
            try
            {
                if (workspace != null && workspace.IsLoaded && (workspace.HasChanges || motion.HasChanges))
                {
                    WorkspaceRecovery.Write(workspace, motion);
                }
                else if (workspace != null && workspace.IsLoaded)
                {
                    WorkspaceRecovery.Clear();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Workspace recovery could not be updated: " + exception.Message);
            }
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
            ShowLoaded();
            WorkspaceRecovery.Clear();
        }

        private void ShowLoaded()
        {
            motion.Bind(workspace, Repaint);
            expressions.Bind(workspace);
            limbs.Bind(workspace);
            cameras.Bind(workspace, ApplyChange, () => stage);
            surfaces.Bind(workspace, ApplyChange);
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
            stage.sceneLighting = workspace.Document.Lights.Length > 0;
            stage.Frame(workspace.GetBounds(), true);
            Repaint();
        }

        private void RestoreLastScene()
        {
            if (workspace.IsLoaded)
            {
                return;
            }
            if (WorkspaceRecovery.Exists)
            {
                Run(RestoreRecovery);
                return;
            }
            string file = EditorPrefs.GetString(Application.dataPath + "/ContentWorkspace/LastScene", "");
            if (workspace.IsLoaded == false && File.Exists(file))
            {
                Run(delegate { LoadFile(file); });
            }
        }

        public void SaveRecovery()
        {
            WorkspaceRecovery.Write(workspace, motion);
        }

        private void CheckpointRecovery()
        {
            if (EditorApplication.timeSinceStartup < nextRecovery)
            {
                return;
            }
            nextRecovery = EditorApplication.timeSinceStartup + 10;
            if (EditorApplication.isPlayingOrWillChangePlaymode || workspace.IsLoaded == false)
            {
                return;
            }
            try
            {
                if (workspace.HasChanges || motion.HasChanges) WorkspaceRecovery.Write(workspace, motion);
                else WorkspaceRecovery.Clear();
            }
            catch (Exception exception)
            {
                message = "Recovery could not be updated: " + exception.Message;
                failure = true;
            }
        }

        public void RestoreRecovery()
        {
            WorkspaceDraft draft = WorkspaceRecovery.Read();
            workspace.Restore(draft.SourceFile, SceneJson.Read(draft.Scene));
            ShowLoaded();
            motion.RestoreDraft(draft.Clip, draft.Time, draft.ClipChanged);
            message = "Recovered the working scene and motion draft. Source files were not changed.";
        }

        private void OnSelectionChange()
        {
            if (workspace == null || workspace.IsLoaded == false)
            {
                return;
            }
            Transform candidate = Selection.activeTransform;
            while (candidate != null && candidate != workspace.Instance.transform)
            {
                for (int index = 0; index < workspace.Document.Nodes.Length; index++)
                {
                    if (workspace.Instance.FindNode(workspace.Document.Nodes[index].Id) == candidate)
                    {
                        selected = index;
                        Repaint();
                        return;
                    }
                }
                candidate = candidate.parent;
            }
        }

        public void AddActor(string file)
        {
            string draft = motion.ExportDraft();
            float seconds = motion.Time;
            bool changed = motion.HasChanges;
            string combined = WorkspaceComposition.AddActor(workspace, file);
            workspace.Load(combined);
            ShowLoaded();
            motion.RestoreDraft(draft, seconds, changed);
            message = "Added actor with separate node identities and shared resources.";
            WorkspaceRecovery.Write(workspace, motion);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Open scene...", EditorStyles.toolbarButton))
            {
                string file = EditorUtility.OpenFilePanel("Open scene document", "", "json");
                if (string.IsNullOrEmpty(file) == false)
                {
                    bool changed;
                    try { changed = workspace.IsLoaded && (workspace.HasChanges || motion.HasChanges); }
                    catch { changed = true; }
                    if (changed == false || EditorUtility.DisplayDialog("Unsaved workspace", "Discard current scene and motion edits? Save a copy first to keep them.", "Discard", "Cancel"))
                    {
                        Run(delegate { LoadFile(file); });
                    }
                }
            }
            using (new EditorGUI.DisabledScope(workspace.IsLoaded == false))
            {
                if (GUILayout.Button("Add actor...", EditorStyles.toolbarButton))
                {
                    string file = EditorUtility.OpenFilePanel("Add an actor from a native scene", "", "json");
                    if (string.IsNullOrEmpty(file) == false) Run(delegate { AddActor(file); });
                }
                if (GUILayout.Button("Save as...", EditorStyles.toolbarButton))
                {
                    string file = EditorUtility.SaveFilePanel("Save scene and resources", Path.GetDirectoryName(workspace.FilePath), "scene-edited", "json");
                    if (string.IsNullOrEmpty(file) == false)
                    {
                        Run(delegate
                        {
                            workspace.Save(file);
                            if (motion.HasChanges) WorkspaceRecovery.Write(workspace, motion);
                            else WorkspaceRecovery.Clear();
                            message = "Saved scene and verified resources.";
                        });
                    }
                }
                if (GUILayout.Button("Frame", EditorStyles.toolbarButton) && stage != null)
                {
                    stage.Frame(workspace.GetBounds(), true);
                }
            }
            EditorGUILayout.EndHorizontal();
            DrawSequenceFiles();
            DrawSceneHistory();
            EditorGUILayout.HelpBox(message, failure ? MessageType.Error : MessageType.Info);
            library.Draw(LoadFromLibrary);
            if (workspace.IsLoaded == false)
            {
                EditorGUILayout.LabelField("Pose and expression editing", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Open a native scene, select a node, and use the stage handles or the fields below. Save a separate copy for your game.", EditorStyles.wordWrappedLabel);
                return;
            }
            bodyScroll = EditorGUILayout.BeginScrollView(bodyScroll);
            filter = EditorGUILayout.TextField("Find node", filter);
            labeledOnly = EditorGUILayout.Toggle("Named controls and roots", labeledOnly);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(position.height * 0.38f));
            for (int index = 0; index < workspace.Document.Nodes.Length; index++)
            {
                SceneNode node = workspace.Document.Nodes[index];
                if (labeledOnly && string.IsNullOrEmpty(node.Label) && node.Parent >= 0)
                {
                    continue;
                }
                string label = string.IsNullOrEmpty(node.Label) ? (node.Parent < 0 ? "Root " + index : node.Id) : node.Label;
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
            cameras.Draw();
            surfaces.Draw();
            EditorGUILayout.EndScrollView();
        }

        private void ApplyChange(Action<SceneDocument> change)
        {
            Run(delegate
            {
                ChangeScene(change);
            });
        }

        public void ChangeScene(Action<SceneDocument> change)
        {
            workspace.Change(change, ValidateMotionTargets);
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            motion.RefreshValues();
            stage.ShowPreview(workspace.PreviewScene);
            stage.sceneLighting = workspace.Document.Lights.Length > 0;
            expressions.Bind(workspace);
            limbs.Bind(workspace);
            surfaces.Bind(workspace, ApplyChange);
            if (selected >= workspace.Document.Nodes.Length) selected = -1;
            if (selected >= 0) Selection.activeTransform = workspace.Instance.FindNode(workspace.Document.Nodes[selected].Id);
            Repaint();
        }

        private void LoadFromLibrary(string file)
        {
            if (workspace.IsLoaded && (workspace.HasChanges || motion.HasChanges) &&
                EditorUtility.DisplayDialog("Unsaved workspace", "Discard current edits and open the library scene?", "Discard", "Cancel") == false)
            {
                return;
            }
            LoadFile(file);
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
