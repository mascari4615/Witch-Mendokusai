using System;
using System.IO;
using System.Security.Cryptography;
using History;
using SceneContent;
using SceneContent.Storage;
using SceneContent.Unity;
using SceneContent.Serialization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed class ContentWorkspace : IDisposable
    {
        public SceneDocument Document { get; private set; }
        public SceneInstance Instance { get; private set; }
        public string FilePath { get; private set; }
        public Scene PreviewScene { get; private set; }
        public bool IsLoaded { get { return Instance != null; } }
        private string baseline;
        private readonly SnapshotHistory<WorkspaceState> history = new SnapshotHistory<WorkspaceState>(state => JsonUtility.ToJson(state), json => JsonUtility.FromJson<WorkspaceState>(json));
        private string rigFile;
        private string rigHash;
        public bool CanUndoChange { get { return history.CanUndo; } }
        public bool CanRedoChange { get { return history.CanRedo; } }

        public bool HasChanges
        {
            get { return IsLoaded && SceneJson.Write(SceneSnapshot.Capture(Document, Instance)) != baseline; }
        }

        public void Load(string file, Action<SceneDocument, SceneInstance> validate = null)
        {
            SceneDocument document = SceneFiles.Read(file);
            Build(document, file, validate);
            baseline = SceneJson.Write(SceneSnapshot.Capture(Document, Instance));
            history.Clear();
        }

        public void Restore(string file, SceneDocument document, Action<SceneDocument, SceneInstance> validate = null)
        {
            string sourceBaseline = SceneJson.Write(SceneFiles.Read(file));
            Build(document, file, validate);
            baseline = sourceBaseline;
            history.Clear();
        }

        public void Change(Action<SceneDocument> action, Action<SceneDocument, SceneInstance> validate = null)
        {
            WorkspaceState previous = CaptureState();
            SceneDocument next = SceneJson.Read(previous.Scene);
            action(next);
            SceneValidation.Ensure(next);
            string file = FilePath;
            history.Execute(previous, () => Build(next, file, validate, previous));
        }

        public void ChangeFile(string file, Action<SceneDocument, SceneInstance> validate = null)
        {
            WorkspaceState previous = CaptureState();
            SceneDocument next = SceneFiles.Read(file);
            history.Execute(previous, () => Build(next, file, validate));
        }

        public void UndoChange(Action<SceneDocument, SceneInstance> validate = null)
        {
            history.Undo(CaptureState(), state => Build(SceneJson.Read(state.Scene), state.FilePath, validate, state));
        }

        public void RedoChange(Action<SceneDocument, SceneInstance> validate = null)
        {
            history.Redo(CaptureState(), state => Build(SceneJson.Read(state.Scene), state.FilePath, validate, state));
        }

        private void Build(SceneDocument document, string file, Action<SceneDocument, SceneInstance> validate = null, WorkspaceState expected = null)
        {
            file = Path.GetFullPath(file);
            string nextRigFile = RigFiles.Find(file);
            string nextRigHash = RigHash(nextRigFile);
            if (expected != null && (nextRigFile != expected.RigFile || nextRigHash != expected.RigHash)) throw new IOException("Rig bindings changed outside this workspace. Restore the source or reopen it before restoring history.");
            DirectoryContentSource source = new DirectoryContentSource(Path.GetDirectoryName(file));
            Scene scene = EditorSceneManager.NewPreviewScene();
            SceneInstance instance = null;
            try
            {
                GameObject anchor = new GameObject("Content workspace");
                SceneManager.MoveGameObjectToScene(anchor, scene);
                StandardSurfaceFactory surfaces = new StandardSurfaceFactory(Shader.Find("Universal Render Pipeline/Unlit"), Shader.Find("Universal Render Pipeline/Lit"), Shader.Find("WM/Toon Surface"));
                instance = SceneContentLoader.LoadWithSurfaces(document, source, surfaces, anchor.transform, activateCameras: false);
                if (validate != null)
                {
                    validate(document, instance);
                }
                EnsureRigIdentity(file, nextRigFile, nextRigHash);
                Dispose();
                PreviewScene = scene;
                Document = document;
                Instance = instance;
                FilePath = file;
                rigFile = nextRigFile;
                rigHash = nextRigHash;
            }
            catch
            {
                EditorSceneManager.ClosePreviewScene(scene);
                throw;
            }
        }

        public void Save(string file)
        {
            if (IsLoaded == false)
            {
                throw new InvalidOperationException("Load a scene before saving");
            }
            SceneDocument snapshot = SceneSnapshot.Capture(Document, Instance);
            EnsureRigIdentity(FilePath, rigFile, rigHash);
            string rig = RigFiles.Read(FilePath, snapshot);
            file = Path.GetFullPath(file);
            string destinationRig = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".rig.json");
            if (rig == null && File.Exists(RigFiles.Find(file))) throw new IOException("Destination has unrelated rig bindings");
            // Keep matching sidecars byte-identical so a save does not invalidate earlier snapshots.
            bool sameRigSource = RigFiles.Find(file) == rigFile && RigHash(rigFile) == rigHash;
            if (rig != null && sameRigSource == false && (File.Exists(destinationRig) == false || RigFiles.Read(file, snapshot) != rig)) RigFiles.Save(file, rig);
            DirectoryContentSource source = new DirectoryContentSource(Path.GetDirectoryName(FilePath));
            SceneFiles.Save(file, snapshot, source);
            Document = snapshot;
            FilePath = Path.GetFullPath(file);
            rigFile = RigFiles.Find(FilePath);
            rigHash = RigHash(rigFile);
            baseline = SceneJson.Write(snapshot);
        }

        private WorkspaceState CaptureState()
        {
            if (IsLoaded == false) throw new InvalidOperationException("Load a scene before changing it");
            EnsureRigIdentity(FilePath, rigFile, rigHash);
            return new WorkspaceState { FilePath = FilePath, Scene = SceneJson.Write(SceneSnapshot.Capture(Document, Instance)), RigFile = rigFile, RigHash = rigHash };
        }

        private static void EnsureRigIdentity(string file, string expectedFile, string expectedHash)
        {
            if (RigFiles.Find(file) != expectedFile || RigHash(expectedFile) != expectedHash) throw new IOException("Rig bindings changed outside this workspace. Restore the source or reopen it before restoring history.");
        }

        private static string RigHash(string file)
        {
            if (File.Exists(file) == false) return "";
            using (FileStream stream = File.OpenRead(file))
            using (SHA256 algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        [Serializable]
        private sealed class WorkspaceState
        {
            public string FilePath;
            public string Scene;
            public string RigFile;
            public string RigHash;
        }

        public Bounds GetBounds()
        {
            Renderer[] renderers = Instance.GetComponentsInChildren<Renderer>();
            Bounds bounds = new Bounds(Instance.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer renderer in renderers)
            {
                if (renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)
                {
                    continue;
                }
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }

        public void Dispose()
        {
            if (PreviewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(PreviewScene);
            }
            PreviewScene = default(Scene);
            Instance = null;
            Document = null;
            FilePath = null;
            rigFile = null;
            rigHash = null;
        }
    }
}
