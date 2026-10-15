using System;
using System.IO;
using Motion;
using Motion.Serialization;
using Motion.Unity;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed class MotionClipPanel : IDisposable
    {
        private PoseClip clip;
        private PoseClipSampler sampler;
        private ContentWorkspace workspace;
        private TransformPoseTarget target;
        private Action repaint;
        private double lastTick;
        private bool playing;
        private string error;
        public float Time { get; private set; }
        public PoseClip Clip { get { return clip; } }

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
            error = null;
        }

        public void Bind(ContentWorkspace value, Action repaintAction)
        {
            workspace = value;
            target = new TransformPoseTarget(id => workspace.Instance.FindNode(id));
            repaint = repaintAction;
            Reset();
        }

        public void Record(string node)
        {
            clip = PoseClipEditing.Record(clip, node, Time, target.Read(node));
            sampler = new PoseClipSampler(clip);
        }

        public void Seek(float seconds)
        {
            sampler.Apply(seconds, target);
            Time = clip.Loop ? ((seconds % clip.Duration) + clip.Duration) % clip.Duration : Mathf.Clamp(seconds, 0, clip.Duration);
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
                    PoseClip candidate = new PoseClip { Id = clip.Id, Duration = duration, Loop = loop, Tracks = clip.Tracks };
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
            using (new EditorGUI.DisabledScope(clip.Tracks.Length == 0))
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
                Reset();
            }
            if (GUILayout.Button("Open clip..."))
            {
                string file = EditorUtility.OpenFilePanel("Open motion clip", "", "json");
                if (string.IsNullOrEmpty(file) == false)
                {
                    Run(delegate
                    {
                        PoseClip candidate = PoseClipJson.Read(File.ReadAllText(file));
                        PoseClipSampler next = new PoseClipSampler(candidate);
                        next.Apply(0, target);
                        clip = candidate;
                        sampler = next;
                        playing = false;
                        Time = 0;
                    });
                }
            }
            if (GUILayout.Button("Save clip..."))
            {
                string file = EditorUtility.SaveFilePanel("Save reusable motion clip", "", "motion-clip", "json");
                if (string.IsNullOrEmpty(file) == false)
                {
                    Run(delegate { File.WriteAllText(file, PoseClipJson.Write(clip)); });
                }
            }
            EditorGUILayout.EndHorizontal();
            int keys = 0;
            foreach (PoseTrack track in clip.Tracks)
            {
                keys += track.Keys.Length;
            }
            EditorGUILayout.LabelField(clip.Tracks.Length + " tracks, " + keys + " keys");
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
