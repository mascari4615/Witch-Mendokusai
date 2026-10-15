using System;
using Motion;
using Motion.Unity;
using SceneContent;
using SceneContent.Unity;
using SceneSequence;
using UnityEngine;

namespace WitchMendokusai.ContentRuntime
{
    public sealed class ContentScenePlayer : MonoBehaviour
    {
        private GameObject loadedRoot;
        private PoseClipPlayer[] players = new PoseClipPlayer[0];
        public SceneInstance Instance { get; private set; }
        public bool IsLoaded { get { return Instance != null; } }

        public void Initialize(SequenceDocument sequence, IContentSource source, ISurfaceFactory surfaces)
        {
            SequenceValidation.Ensure(sequence);
            Initialize(sequence.Scene, source, surfaces, sequence.Clips);
        }

        public void Initialize(SceneDocument document, IContentSource source, ISurfaceFactory surfaces, PoseClip[] clips)
        {
            if (IsLoaded) throw new InvalidOperationException("Unload the current content scene before loading another");
            if (clips == null) throw new ArgumentNullException("clips");
            SequenceValidation.Ensure(new SequenceDocument { Id = "runtime", Scene = document, Clips = clips });
            GameObject staged = new GameObject("Content scene");
            staged.SetActive(false);
            staged.transform.SetParent(transform, false);
            try
            {
                SceneInstance scene = SceneContentLoader.LoadWithSurfaces(document, source, surfaces, staged.transform);
                TransformPoseTarget poses = new TransformPoseTarget(scene.FindNode);
                FloatPropertyTarget values = SceneMotionBindings.Create(document, () => scene);
                PoseClipPlayer[] next = new PoseClipPlayer[clips.Length];
                for (int index = 0; index < clips.Length; index++)
                {
                    next[index] = staged.AddComponent<PoseClipPlayer>();
                    next[index].Initialize(clips[index], poses, values);
                }
                loadedRoot = staged;
                players = next;
                Instance = scene;
                staged.SetActive(true);
            }
            catch
            {
                if (Application.isPlaying) Destroy(staged);
                else DestroyImmediate(staged);
                throw;
            }
        }

        public void Play()
        {
            EnsureLoaded();
            foreach (PoseClipPlayer player in players) player.Play();
        }

        public void Pause()
        {
            foreach (PoseClipPlayer player in players)
                if (player != null) player.Pause();
        }

        public void Seek(float time)
        {
            EnsureLoaded();
            foreach (PoseClipPlayer player in players) player.Seek(time);
        }

        public void Unload()
        {
            Pause();
            if (loadedRoot != null)
            {
                loadedRoot.SetActive(false);
                if (Application.isPlaying) Destroy(loadedRoot);
                else DestroyImmediate(loadedRoot);
            }
            loadedRoot = null;
            Instance = null;
            players = new PoseClipPlayer[0];
        }

        private void EnsureLoaded()
        {
            if (IsLoaded == false) throw new InvalidOperationException("Load a content scene first");
        }
    }
}
