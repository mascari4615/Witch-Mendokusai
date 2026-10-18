using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed partial class MotionClipPanel
    {
        private bool expanded;
        public bool IsPlaying { get { return playing; } }

        public void TogglePlayback()
        {
            if (clip.Tracks.Length == 0 && clip.Values.Length == 0 && (dynamics == null || dynamics.Count == 0)) return;
            if (playing == false && Time >= clip.Duration) Seek(0);
            playing = playing == false;
            lastTick = EditorApplication.timeSinceStartup;
        }

        public void DrawTransport()
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(clip.Tracks.Length == 0 && clip.Values.Length == 0 && (dynamics == null || dynamics.Count == 0)))
            {
                if (GUILayout.Button(AuthoringText.Get(playing ? "Pause" : "Play"), GUILayout.Height(32))) Run(TogglePlayback);
            }
            if (GUILayout.Button(AuthoringText.Get("Start"), GUILayout.Height(32)))
            {
                playing = false;
                Run(delegate { Seek(0); });
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            float seconds = EditorGUILayout.Slider(AuthoringText.Get("Time"), Time, 0, clip.Duration);
            if (EditorGUI.EndChangeCheck())
            {
                playing = false;
                Run(delegate { Seek(seconds); });
            }
            if (string.IsNullOrEmpty(error) == false) EditorGUILayout.HelpBox(AuthoringText.Format("Action failed. Details: {0}", error), MessageType.Error);
        }
    }
}
