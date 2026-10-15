using System;
using System.Collections.Generic;

namespace Motion
{
    public static class PoseClipEditing
    {
        private const float KEY_TIME_TOLERANCE = 0.00001f;

        public static PoseClip Record(PoseClip clip, string node, float time, PoseValue pose)
        {
            new PoseClipSampler(clip);
            PoseKey key = new PoseKey
            {
                Time = time,
                Position = new[] { pose.Position.X, pose.Position.Y, pose.Position.Z },
                Rotation = new[] { pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z, pose.Rotation.W },
                Scale = new[] { pose.Scale.X, pose.Scale.Y, pose.Scale.Z }
            };
            List<PoseTrack> tracks = new List<PoseTrack>();
            bool found = false;
            foreach (PoseTrack track in clip.Tracks)
            {
                if (track.Node != node)
                {
                    tracks.Add(track);
                    continue;
                }
                List<PoseKey> keys = new List<PoseKey>();
                foreach (PoseKey existing in track.Keys)
                {
                    if (Math.Abs(existing.Time - time) > KEY_TIME_TOLERANCE)
                    {
                        keys.Add(existing);
                    }
                }
                keys.Add(key);
                keys.Sort((left, right) => left.Time.CompareTo(right.Time));
                tracks.Add(new PoseTrack { Node = node, Keys = keys.ToArray() });
                found = true;
            }
            if (found == false)
            {
                tracks.Add(new PoseTrack { Node = node, Keys = new[] { key } });
            }
            PoseClip result = new PoseClip
            {
                Id = clip.Id, Duration = clip.Duration, Loop = clip.Loop, Tracks = tracks.ToArray()
            };
            new PoseClipSampler(result);
            return result;
        }
    }
}
