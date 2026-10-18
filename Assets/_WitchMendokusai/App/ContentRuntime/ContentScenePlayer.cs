using System;
using System.Collections.Generic;
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
        private PoseClipSampler[] samplers = new PoseClipSampler[0];
        private SceneConstraintPlan constraints;
        private TransformPoseTarget poses;
        private SceneDynamicsClock dynamics;
        private Action<float> sampleFrame;
        private float elapsed;
        private bool unscaled;
        public string Failure { get; private set; }
        public SceneInstance Instance { get; private set; }
        public bool IsLoaded { get { return Instance != null; } }
        public bool IsPlaying { get; private set; }
        public float Time
        {
            get
            {
                float seconds = 0;
                if (samplers.Length == 0) return elapsed;
                foreach (PoseClipSampler sampler in samplers) seconds = Mathf.Max(seconds, ClipTime.Normalize(elapsed, sampler.Duration, sampler.Loop));
                return seconds;
            }
        }

        public void Initialize(SequenceDocument sequence, IContentSource source, ISurfaceFactory surfaces, bool useUnscaledTime = false)
        {
            SequenceValidation.Ensure(sequence);
            Initialize(sequence.Scene, source, surfaces, sequence.Clips, useUnscaledTime);
        }

        public void Initialize(SceneDocument document, IContentSource source, ISurfaceFactory surfaces, PoseClip[] clips, bool useUnscaledTime = false)
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
                SceneConstraintPlan plan = new SceneConstraintPlan(document);
                FloatPropertyTarget values = SceneMotionBindings.Create(document, () => scene);
                PoseClipSampler[] next = new PoseClipSampler[clips.Length];
                for (int index = 0; index < clips.Length; index++)
                {
                    next[index] = new PoseClipSampler(clips[index]);
                    next[index].ValidateTargets(poses, values);
                }
                Action<float> sample = seconds => { foreach (PoseClipSampler sampler in next) sampler.Apply(seconds, poses, values); plan.Apply(poses); };
                List<string> properties = new List<string>();
                foreach (PoseClip clip in clips) foreach (ScalarTrack track in clip.Values) properties.Add(track.Target);
                SceneDynamicsClock clock = new SceneDynamicsClock(document, poses, values, properties);
                clock.Evaluate(0, sample);
                loadedRoot = staged;
                constraints = plan;
                this.poses = poses;
                samplers = next;
                dynamics = clock;
                sampleFrame = sample;
                elapsed = 0;
                unscaled = useUnscaledTime;
                Failure = null;
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
            Failure = null;
            IsPlaying = samplers.Length > 0 || dynamics.Count > 0;
        }

        public void Pause()
        {
            IsPlaying = false;
        }

        public void Seek(float time)
        {
            EnsureLoaded();
            if (float.IsNaN(time) || float.IsInfinity(time)) throw new ArgumentException("Playback time must be finite");
            float seconds = dynamics.Count > 0 ? Mathf.Max(0, time) : time;
            if (samplers.Length > 0 && Array.Exists(samplers, sampler => sampler.Loop) == false)
            {
                float duration = 0;
                foreach (PoseClipSampler sampler in samplers) duration = Mathf.Max(duration, sampler.Duration);
                seconds = ClipTime.Normalize(seconds, duration, false);
            }
            dynamics.Evaluate(seconds, sampleFrame);
            elapsed = seconds;
            Failure = null;
        }

        private void LateUpdate()
        {
            if (IsLoaded == false || Failure != null) return;
            try
            {
                if (IsPlaying)
                {
                    Seek(elapsed + (unscaled ? UnityEngine.Time.unscaledDeltaTime : UnityEngine.Time.deltaTime));
                    IsPlaying = samplers.Length == 0 ? dynamics.Count > 0 : Array.Exists(samplers, sampler => sampler.Loop || elapsed < sampler.Duration);
                }
                else if (dynamics.Count > 0) dynamics.Refresh(sampleFrame, () => constraints.Apply(poses));
                else if (constraints.Count > 0) constraints.Apply(poses);
            }
            catch (Exception exception) { IsPlaying = false; Failure = exception.Message; Debug.LogException(exception); }
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
            samplers = new PoseClipSampler[0];
            constraints = null;
            poses = null;
            dynamics = null;
            sampleFrame = null;
            Failure = null;
        }

        private void EnsureLoaded()
        {
            if (IsLoaded == false) throw new InvalidOperationException("Load a content scene first");
        }
    }
}
