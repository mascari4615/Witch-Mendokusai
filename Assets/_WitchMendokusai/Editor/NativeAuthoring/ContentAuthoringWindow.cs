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
        private ParameterPanel parameters;

        public ContentWorkspace Workspace { get { return workspace; } }
        public MotionClipPanel Motion { get { return motion; } }
        public SurfacePanel Surfaces { get { return surfaces; } }

        [MenuItem("WM/Authoring/Scene workspace")]
        public static ContentAuthoringWindow Open()
        {
            return GetWindow<ContentAuthoringWindow>(AuthoringText.Get("Scene workspace"));
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(libraryTitle)) libraryTitle = AuthoringText.Get("New sequence");
            workspace = new ContentWorkspace();
            minSize = new Vector2(380, 480);
            motion = new MotionClipPanel();
            expressions = new ExpressionPanel(motion.RecordValues);
            limbs = new LimbPanel(motion.RecordNodes);
            cameras = new CameraLightPanel(motion.RecordValues);
            library = new LibraryPanel();
            surfaces = new SurfacePanel();
            parameters = new ParameterPanel();
            Undo.undoRedoPerformed += Repaint;
            SceneView.duringSceneGui += DrawStageControls;
            SceneView.beforeSceneGui += PrepareStage;
            EditorApplication.delayCall += RestoreLastScene;
            EditorApplication.update += CheckpointRecovery;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
            SceneView.duringSceneGui -= DrawStageControls;
            SceneView.beforeSceneGui -= PrepareStage;
            EditorApplication.delayCall -= RestoreLastScene;
            EditorApplication.update -= CheckpointRecovery;
            try
            {
                if (workspace != null && workspace.IsLoaded)
                {
                    WorkspaceRecovery.Write(workspace, motion);
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
            limbs.Bind(workspace, ApplyChange);
            cameras.Bind(workspace, ApplyChange, () => stage);
            surfaces.Bind(workspace, ApplyChange);
            parameters.Bind(workspace, ApplyChange, ExportParameterSequence);
            labeledOnly = Array.Exists(workspace.Document.Nodes, node => string.IsNullOrEmpty(node.Label) == false);
            selected = -1;
            failure = false;
            message = null;
            EditorPrefs.SetString(Application.dataPath + "/ContentWorkspace/LastScene", workspace.FilePath);
            if (stage == null)
            {
                stage = CreateInstance<ContentStageView>();
                stage.titleContent = new GUIContent(AuthoringText.Get("Content stage"));
                stage.ShowUtility();
            }
            stage.ShowPreview(workspace.PreviewScene);
            stage.sceneLighting = sceneLighting;
            stage.Frame(workspace.GetBounds(), true);
            ShowCharacterView();
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
            DrawWelcome();
            if (workspace.IsLoaded)
            {
                motion.DrawTransport();
            }
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(AuthoringText.Get("Open scene..."), EditorStyles.toolbarButton))
            {
                string file = EditorUtility.OpenFilePanel(AuthoringText.Get("Open scene document"), "", "json");
                if (string.IsNullOrEmpty(file) == false)
                {
                    bool changed;
                    try { changed = workspace.IsLoaded && (workspace.HasChanges || motion.HasChanges); }
                    catch { changed = true; }
                    if (changed == false || EditorUtility.DisplayDialog(AuthoringText.Get("Unsaved workspace"), AuthoringText.Get("Discard current scene and motion edits? Save a copy first to keep them."), AuthoringText.Get("Discard"), AuthoringText.Get("Cancel")))
                    {
                        Run(delegate { LoadFile(file); });
                    }
                }
            }
            using (new EditorGUI.DisabledScope(workspace.IsLoaded == false))
            {
                if (GUILayout.Button(AuthoringText.Get("Add actor..."), EditorStyles.toolbarButton))
                {
                    string file = EditorUtility.OpenFilePanel(AuthoringText.Get("Add an actor from a native scene"), "", "json");
                    if (string.IsNullOrEmpty(file) == false) Run(delegate { AddActor(file); });
                }
                if (GUILayout.Button(AuthoringText.Get("Save as..."), EditorStyles.toolbarButton))
                {
                    string file = EditorUtility.SaveFilePanel(AuthoringText.Get("Save scene and resources"), Path.GetDirectoryName(workspace.FilePath), "scene-edited", "json");
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
                if (GUILayout.Button(AuthoringText.Get("Frame"), EditorStyles.toolbarButton) && stage != null)
                {
                    stage.Frame(workspace.GetBounds(), true);
                }
            }
            EditorGUILayout.EndHorizontal();
            DrawSequenceFiles();
            DrawSceneHistory();
            string status = message == null ? AuthoringText.Format("{0} nodes, {1} meshes. Select a node to pose it.", workspace.Document.Nodes.Length, workspace.Document.Meshes.Length) : AuthoringText.Get(message);
            EditorGUILayout.HelpBox(failure ? AuthoringText.Format("Action failed. Details: {0}", status) : status, failure ? MessageType.Error : MessageType.Info);
            library.Draw(LoadFromLibrary);
            if (workspace.IsLoaded == false)
            {
                EditorGUILayout.LabelField(AuthoringText.Get("Pose and expression editing"), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(AuthoringText.Get("Open a native scene, select a node, and use the stage handles or the fields below. Save a separate copy for your game."), EditorStyles.wordWrappedLabel);
                return;
            }
            bodyScroll = EditorGUILayout.BeginScrollView(bodyScroll);
            parameters.Draw(selected);
            DrawPoseSelection();
            motion.Draw(selected < 0 ? null : workspace.Document.Nodes[selected].Id);
            limbs.Draw();
            expressions.Draw();
            cameras.Draw();
            surfaces.Draw();
            EditorGUILayout.EndScrollView();
        }

        private void DrawNodeList()
        {
            filter = EditorGUILayout.TextField(AuthoringText.Get("Find node"), filter);
            labeledOnly = EditorGUILayout.Toggle(AuthoringText.Get("Named controls and roots"), labeledOnly);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(200));
            for (int index = 0; index < workspace.Document.Nodes.Length; index++)
            {
                SceneNode node = workspace.Document.Nodes[index];
                if (labeledOnly && string.IsNullOrEmpty(node.Label) && node.Parent >= 0)
                {
                    continue;
                }
                string label = string.IsNullOrEmpty(node.Label) ? (node.Parent < 0 ? AuthoringText.Get("Root ") + index : node.Id) : AuthoringText.NodeLabel(node.Label);
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
            DrawSelected();
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
            stage.sceneLighting = sceneLighting;
            expressions.Bind(workspace);
            limbs.Bind(workspace, ApplyChange);
            surfaces.Bind(workspace, ApplyChange);
            parameters.Bind(workspace, ApplyChange, ExportParameterSequence);
            if (selected >= workspace.Document.Nodes.Length) selected = -1;
            if (selected >= 0) Selection.activeTransform = workspace.Instance.FindNode(workspace.Document.Nodes[selected].Id);
            Repaint();
        }

        private void LoadFromLibrary(string file)
        {
            if (workspace.IsLoaded && (workspace.HasChanges || motion.HasChanges) &&
                EditorUtility.DisplayDialog(AuthoringText.Get("Unsaved workspace"), AuthoringText.Get("Discard current edits and open the library scene?"), AuthoringText.Get("Discard"), AuthoringText.Get("Cancel")) == false)
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
            EditorGUILayout.LabelField(AuthoringText.Get("Local pose"), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            Vector3 positionValue = EditorGUILayout.Vector3Field(AuthoringText.Get("Position"), target.localPosition);
            Vector3 euler = EditorGUILayout.Vector3Field(AuthoringText.Get("Rotation"), target.localEulerAngles);
            Vector3 scale = EditorGUILayout.Vector3Field(AuthoringText.Get("Scale"), target.localScale);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, AuthoringText.Get("Change node pose"));
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
