using System;
using System.IO;
using History;
using Motion;
using Motion.Serialization;
using Motion.Unity;
using SceneContent.Storage;
using UnityEditor;
using UnityEngine;
using WitchMendokusai.ContentRuntime;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed partial class MotionClipPanel : IDisposable
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
        private readonly SnapshotHistory<PoseClip> history = new SnapshotHistory<PoseClip>(PoseClipJson.Write, PoseClipJson.Read);
        private bool showTracks;
        private Vector2 trackScroll;

        public void RemoveKey(string targetId, TrackKind kind, float? seconds = null)
        {
            playing = false;
            PoseClip next = TrackEditing.Remove(clip, targetId, kind, seconds);
            history.Record(clip);
            clip = next;
            sampler = new PoseClipSampler(clip);
        }

        public void UndoEdit()
        {
            playing = false;
            clip = history.Undo(clip);
            sampler = new PoseClipSampler(clip);
            Time = Mathf.Min(Time, clip.Duration);
        }

        public void RedoEdit()
        {
            playing = false;
            clip = history.Redo(clip);
            sampler = new PoseClipSampler(clip);
            Time = Mathf.Min(Time, clip.Duration);
        }

        public string ExportDraft()
        {
            return PoseClipJson.Write(clip);
        }

        public void ValidateDraft()
        {
            sampler.ValidateTargets(target, values);
        }

        public void RestoreDraft(string json, float seconds, bool changed)
        {
            PoseClip restored = PoseClipJson.Read(json);
            PoseClipSampler restoredSampler = new PoseClipSampler(restored);
            restoredSampler.ValidateTargets(target, values);
            if (float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                throw new ArgumentException("Draft time must be finite");
            }
            clip = restored;
            sampler = restoredSampler;
            Time = Mathf.Clamp(seconds, 0, clip.Duration);
            savedClip = changed ? null : PoseClipJson.Write(clip);
            playing = false;
            history.Clear();
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
            history.Clear();
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
            history.Record(clip);
            clip = next;
            sampler = new PoseClipSampler(clip);
        }

        public void Record(string node)
        {
            PoseClip next = PoseClipEditing.Record(clip, node, Time, target.Read(node));
            history.Record(clip);
            clip = next;
            sampler = new PoseClipSampler(clip);
        }

        public void RecordNodes(string[] nodes)
        {
            PoseClip next = clip;
            foreach (string node in nodes)
            {
                next = PoseClipEditing.Record(next, node, Time, target.Read(node));
            }
            history.Record(clip);
            clip = next;
            sampler = new PoseClipSampler(clip);
        }

        public void Seek(float seconds)
        {
            sampler.Apply(seconds, target, values, loopOverride: false);
            Time = ClipTime.Normalize(seconds, clip.Duration, false);
            SceneView.RepaintAll();
        }

        public void MoveToAdjacentKey(bool forward)
        {
            playing = false;
            float? seconds = ClipKeyNavigation.Adjacent(clip, Time, forward);
            if (seconds.HasValue)
            {
                Seek(seconds.Value);
            }
        }

        public void Draw(string selectedNode)
        {
            EditorGUILayout.Space();
            expanded = EditorGUILayout.Foldout(expanded, AuthoringText.Get("Motion clip"), true);
            if (expanded == false) return;
            EditorGUI.BeginChangeCheck();
            float duration = EditorGUILayout.FloatField(AuthoringText.Get("Duration (seconds)"), clip.Duration);
            bool loop = EditorGUILayout.Toggle(AuthoringText.Get("Loop"), clip.Loop);
            if (EditorGUI.EndChangeCheck())
            {
                Run(delegate
                {
                    PoseClip candidate = new PoseClip { Id = clip.Id, Duration = duration, Loop = loop, Tracks = clip.Tracks, Values = clip.Values };
                    PoseClipSampler next = new PoseClipSampler(candidate);
                    history.Record(clip);
                    clip = candidate;
                    sampler = next;
                    Time = Mathf.Min(Time, duration);
                });
            }
            EditorGUI.BeginChangeCheck();
            float seconds = EditorGUILayout.Slider(AuthoringText.Get("Time"), Time, 0, clip.Duration);
            if (EditorGUI.EndChangeCheck())
            {
                playing = false;
                Run(delegate { Seek(seconds); });
            }
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(selectedNode)))
            {
                if (GUILayout.Button(AuthoringText.Get("Record node key")))
                {
                    Run(delegate { Record(selectedNode); });
                }
            }
            using (new EditorGUI.DisabledScope(clip.Tracks.Length == 0 && clip.Values.Length == 0))
            {
                if (GUILayout.Button(playing ? AuthoringText.Get("Pause") : AuthoringText.Get("Play")))
                {
                    Run(TogglePlayback);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(AuthoringText.Get("Previous key")))
            {
                Run(delegate { MoveToAdjacentKey(false); });
            }
            if (GUILayout.Button(AuthoringText.Get("Next key")))
            {
                Run(delegate { MoveToAdjacentKey(true); });
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(AuthoringText.Get("New clip")))
            {
                if (ConfirmDiscard()) Reset();
            }
            if (GUILayout.Button(AuthoringText.Get("Open clip...")) && ConfirmDiscard())
            {
                string file = EditorUtility.OpenFilePanel(AuthoringText.Get("Open motion clip"), "", "json");
                if (string.IsNullOrEmpty(file) == false)
                {
                    Run(delegate
                    {
                        PoseClip candidate = PoseClipJson.Read(File.ReadAllText(file));
                        PoseClipSampler next = new PoseClipSampler(candidate);
                        next.ValidateTargets(target, values);
                        next.Apply(0, target, values);
                        clip = candidate;
                        sampler = next;
                        playing = false;
                        Time = 0;
                        savedClip = PoseClipJson.Write(clip);
                        history.Clear();
                    });
                }
            }
            if (GUILayout.Button(AuthoringText.Get("Save clip...")))
            {
                string file = EditorUtility.SaveFilePanel(AuthoringText.Get("Save reusable motion clip"), "", "motion-clip", "json");
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
            EditorGUILayout.LabelField(AuthoringText.Format("{0} pose tracks, {1} value tracks, {2} keys", clip.Tracks.Length, clip.Values.Length, keys));
            DrawTrackEdits();
            if (string.IsNullOrEmpty(error) == false)
            {
                EditorGUILayout.HelpBox(AuthoringText.Format("Action failed. Details: {0}", error), MessageType.Error);
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
            Run(delegate { Seek(ClipTime.Normalize(next, clip.Duration, clip.Loop)); });
            if (clip.Loop == false && Time >= clip.Duration)
            {
                playing = false;
            }
            repaint();
        }

        private void DrawTrackEdits()
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(history.CanUndo == false))
            {
                if (GUILayout.Button(AuthoringText.Get("Undo key edit"))) UndoEdit();
            }
            using (new EditorGUI.DisabledScope(history.CanRedo == false))
            {
                if (GUILayout.Button(AuthoringText.Get("Redo key edit"))) RedoEdit();
            }
            EditorGUILayout.EndHorizontal();
            showTracks = EditorGUILayout.Foldout(showTracks, AuthoringText.Get("Tracks and keys"), true);
            if (showTracks == false)
            {
                return;
            }
            trackScroll = EditorGUILayout.BeginScrollView(trackScroll, GUILayout.Height(150));
            foreach (PoseTrack track in clip.Tracks)
            {
                TrackRow(track.Node, TrackKind.Pose, track.Keys.Length);
                EditorGUILayout.BeginHorizontal();
                for (int index = 0; index < Mathf.Min(8, track.Keys.Length); index++)
                {
                    KeyButton(track.Keys[index].Time);
                }
                EditorGUILayout.EndHorizontal();
            }
            foreach (ScalarTrack track in clip.Values)
            {
                TrackRow(track.Target, TrackKind.Value, track.Keys.Length);
                EditorGUILayout.BeginHorizontal();
                for (int index = 0; index < Mathf.Min(8, track.Keys.Length); index++)
                {
                    KeyButton(track.Keys[index].Time);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField(AuthoringText.Get("First 8 keys per track shown. Previous/Next visits every key."), EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        private void KeyButton(float seconds)
        {
            if (GUILayout.Button(new GUIContent(seconds.ToString("G6") + "s", seconds.ToString("R") + " seconds")))
            {
                playing = false;
                Run(delegate { Seek(seconds); });
            }
        }

        private void TrackRow(string id, TrackKind kind, int count)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(id + " (" + count + ")", id), GUILayout.MinWidth(70), GUILayout.MaxWidth(Mathf.Max(70, EditorGUIUtility.currentViewWidth - 210)));
            if (GUILayout.Button(AuthoringText.Get("Delete key"), GUILayout.Width(70))) RemoveKey(id, kind, Time);
            if (GUILayout.Button(AuthoringText.Get("Delete track"), GUILayout.Width(78))) RemoveKey(id, kind);
            EditorGUILayout.EndHorizontal();
        }

        private bool ConfirmDiscard()
        {
            return HasChanges == false || EditorUtility.DisplayDialog(AuthoringText.Get("Unsaved motion clip"), AuthoringText.Get("Discard the current motion draft? Save the clip first to keep it."), AuthoringText.Get("Discard"), AuthoringText.Get("Cancel"));
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
