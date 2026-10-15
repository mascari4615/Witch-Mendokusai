using System;
using System.IO;
using Motion;
using Motion.Serialization;
using Motion.Unity;
using SceneContent.Storage;
using UnityEditor;
using UnityEngine;
using WitchMendokusai.ContentRuntime;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed class MotionClipPanel : IDisposable
    {
        private PoseClip clip;
        private PoseClipSampler sampler;
        private ContentWorkspace workspace;
        private TransformPoseTarget target;
        private FloatPropertyTarget values;
        private Action repaint;
        private double lastTick;
        private bool playing;
        private string error;
        public float Time { get; private set; }
        public PoseClip Clip { get { return clip; } }
        private string savedClip;
        public bool HasChanges { get { return PoseClipJson.Write(clip) != savedClip; } }

        public string ExportDraft()
        {
            return PoseClipJson.Write(clip);
        }

        public void RestoreDraft(string json, float seconds, bool changed)
        {
            PoseClip restored = PoseClipJson.Read(json);
            PoseClipSampler restoredSampler = new PoseClipSampler(restored);
            if (float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                throw new ArgumentException("Draft time must be finite");
            }
            clip = restored;
            sampler = restoredSampler;
            Time = Mathf.Clamp(seconds, 0, clip.Duration);
            savedClip = changed ? null : PoseClipJson.Write(clip);
            playing = false;
        }

        public MotionClipPanel()
        {
            Reset();
            EditorApplication.update += Tick;
        }

        public void Reset()
        {
            playing = false;
            Time = 0;
            clip = new PoseClip { Id = Guid.NewGuid().ToString("N"), Duration = 3, Tracks = new PoseTrack[0] };
            sampler = new PoseClipSampler(clip);
            savedClip = PoseClipJson.Write(clip);
            error = null;
        }

        public void Bind(ContentWorkspace value, Action repaintAction)
        {
            workspace = value;
            target = new TransformPoseTarget(id => workspace.Instance.FindNode(id));
            RefreshValues();
            repaint = repaintAction;
            Reset();
        }

        public void RefreshValues()
        {
            values = SceneMotionBindings.Create(workspace.Document, () => workspace.Instance);
        }

        public void RecordValues(string[] properties)
        {
            PoseClip next = clip;
            foreach (string property in properties)
            {
                next = PoseClipEditing.RecordValue(next, property, Time, values.Read(property));
            }
            clip = next;
            sampler = new PoseClipSampler(clip);
        }

        public void Record(string node)
        {
            clip = PoseClipEditing.Record(clip, node, Time, target.Read(node));
            sampler = new PoseClipSampler(clip);
        }

        public void RecordNodes(string[] nodes)
        {
            PoseClip next = clip;
            foreach (string node in nodes)
            {
                next = PoseClipEditing.Record(next, node, Time, target.Read(node));
            }
            clip = next;
            sampler = new PoseClipSampler(clip);
        }

        public void Seek(float seconds)
        {
            sampler.Apply(seconds, target, values);
            Time = ClipTime.Normalize(seconds, clip.Duration, clip.Loop);
            SceneView.RepaintAll();
        }

        public void Draw(string selectedNode)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Motion clip", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            float duration = EditorGUILayout.FloatField("Duration (seconds)", clip.Duration);
            bool loop = EditorGUILayout.Toggle("Loop", clip.Loop);
            if (EditorGUI.EndChangeCheck())
            {
                Run(delegate
                {
                    PoseClip candidate = new PoseClip { Id = clip.Id, Duration = duration, Loop = loop, Tracks = clip.Tracks, Values = clip.Values };
                    PoseClipSampler next = new PoseClipSampler(candidate);
                    clip = candidate;
                    sampler = next;
                    Time = Mathf.Min(Time, duration);
                });
            }
            EditorGUI.BeginChangeCheck();
            float seconds = EditorGUILayout.Slider("Time", Time, 0, clip.Duration);
            if (EditorGUI.EndChangeCheck())
            {
                playing = false;
                Run(delegate { Seek(seconds); });
            }
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(selectedNode)))
            {
                if (GUILayout.Button("Record node key"))
                {
                    Run(delegate { Record(selectedNode); });
                }
            }
            using (new EditorGUI.DisabledScope(clip.Tracks.Length == 0 && clip.Values.Length == 0))
            {
                if (GUILayout.Button(playing ? "Pause" : "Play"))
                {
                    if (playing == false && Time >= clip.Duration)
                    {
                        Run(delegate { Seek(0); });
                    }
                    playing = playing == false;
                    lastTick = EditorApplication.timeSinceStartup;
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New clip"))
            {
                if (ConfirmDiscard()) Reset();
            }
            if (GUILayout.Button("Open clip...") && ConfirmDiscard())
            {
                string file = EditorUtility.OpenFilePanel("Open motion clip", "", "json");
                if (string.IsNullOrEmpty(file) == false)
                {
                    Run(delegate
                    {
                        PoseClip candidate = PoseClipJson.Read(File.ReadAllText(file));
                        PoseClipSampler next = new PoseClipSampler(candidate);
                        next.Apply(0, target, values);
                        clip = candidate;
                        sampler = next;
                        playing = false;
                        Time = 0;
                        savedClip = PoseClipJson.Write(clip);
                    });
                }
            }
            if (GUILayout.Button("Save clip..."))
            {
                string file = EditorUtility.SaveFilePanel("Save reusable motion clip", "", "motion-clip", "json");
                if (string.IsNullOrEmpty(file) == false)
                {
                    Run(delegate
                    {
                        string json = PoseClipJson.Write(clip);
                        AtomicFiles.WriteText(file, json);
                        savedClip = json;
                    });
                }
            }
            EditorGUILayout.EndHorizontal();
            int keys = 0;
            foreach (PoseTrack track in clip.Tracks)
            {
                keys += track.Keys.Length;
            }
            foreach (ScalarTrack track in clip.Values)
            {
                keys += track.Keys.Length;
            }
            EditorGUILayout.LabelField(clip.Tracks.Length + " pose tracks, " + clip.Values.Length + " value tracks, " + keys + " keys");
            if (string.IsNullOrEmpty(error) == false)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }

        private void Tick()
        {
            if (playing == false || workspace == null || workspace.IsLoaded == false)
            {
                return;
            }
            double now = EditorApplication.timeSinceStartup;
            float next = Time + (float)(now - lastTick);
            lastTick = now;
            Run(delegate { Seek(next); });
            if (clip.Loop == false && Time >= clip.Duration)
            {
                playing = false;
            }
            repaint();
        }

        private bool ConfirmDiscard()
        {
            return HasChanges == false || EditorUtility.DisplayDialog("Unsaved motion clip", "Discard the current motion draft? Save the clip first to keep it.", "Discard", "Cancel");
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
                playing = false;
            }
        }

        public void Dispose()
        {
            playing = false;
            EditorApplication.update -= Tick;
        }
    }
}
