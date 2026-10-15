using System;
using System.Collections.Generic;

namespace Motion
{
    public enum TrackKind
    {
        Pose,
        Value
    }

    public static class TrackEditing
    {
        public static PoseClip Remove(PoseClip clip, string target, TrackKind kind, float? time = null)
        {
            new PoseClipSampler(clip);
            if (string.IsNullOrEmpty(target) || (kind != TrackKind.Pose && kind != TrackKind.Value) ||
                (time.HasValue && (float.IsNaN(time.Value) || float.IsInfinity(time.Value))))
            {
                throw new ArgumentException("Invalid motion edit target or time");
            }
            List<PoseTrack> poses = new List<PoseTrack>();
            foreach (PoseTrack track in clip.Tracks)
            {
                if (kind != TrackKind.Pose || track.Node != target)
                {
                    poses.Add(track);
                    continue;
                }
                if (time.HasValue == false) continue;
                List<PoseKey> keys = new List<PoseKey>();
                foreach (PoseKey key in track.Keys)
                    if (Math.Abs(key.Time - time.Value) > .00001f) keys.Add(key);
                if (keys.Count > 0) poses.Add(new PoseTrack { Node = track.Node, Keys = keys.ToArray() });
            }
            List<ScalarTrack> values = new List<ScalarTrack>();
            foreach (ScalarTrack track in clip.Values)
            {
                if (kind != TrackKind.Value || track.Target != target)
                {
                    values.Add(track);
                    continue;
                }
                if (time.HasValue == false) continue;
                List<ScalarKey> keys = new List<ScalarKey>();
                foreach (ScalarKey key in track.Keys)
                    if (Math.Abs(key.Time - time.Value) > .00001f) keys.Add(key);
                if (keys.Count > 0) values.Add(new ScalarTrack { Target = track.Target, Keys = keys.ToArray() });
            }
            return new PoseClip { Id = clip.Id, Duration = clip.Duration, Loop = clip.Loop, Tracks = poses.ToArray(), Values = values.ToArray() };
        }
    }
}
