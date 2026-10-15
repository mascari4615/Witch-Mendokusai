using System;
using System.IO;
using SceneContent;
using SceneContent.Storage;
using SceneContent.Unity;
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

        public void Load(string file)
        {
            SceneDocument document = SceneFiles.Read(file);
            Build(document, file);
        }

        public void Change(Action<SceneDocument> action)
        {
            SceneDocument next = SceneSnapshot.Capture(Document, Instance);
            action(next);
            SceneValidation.Ensure(next);
            Build(next, FilePath);
        }

        private void Build(SceneDocument document, string file)
        {
            DirectoryContentSource source = new DirectoryContentSource(Path.GetDirectoryName(Path.GetFullPath(file)));
            Scene scene = EditorSceneManager.NewPreviewScene();
            SceneInstance instance = null;
            try
            {
                GameObject anchor = new GameObject("Content workspace");
                SceneManager.MoveGameObjectToScene(anchor, scene);
                StandardSurfaceFactory surfaces = new StandardSurfaceFactory(Shader.Find("Universal Render Pipeline/Unlit"), Shader.Find("Universal Render Pipeline/Lit"));
                instance = SceneContentLoader.LoadWithSurfaces(document, source, surfaces, anchor.transform, activateCameras: false);
                Dispose();
                PreviewScene = scene;
                Document = document;
                Instance = instance;
                FilePath = Path.GetFullPath(file);
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
            DirectoryContentSource source = new DirectoryContentSource(Path.GetDirectoryName(FilePath));
            SceneFiles.Save(file, snapshot, source);
            Document = snapshot;
            FilePath = Path.GetFullPath(file);
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
        }
    }
}
