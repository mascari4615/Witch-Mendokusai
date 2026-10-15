using System;
using System.Collections.Generic;

namespace Motion
{
    public static class PoseClipEditing
    {
        public static PoseClip Record(PoseClip clip, string node, float time, PoseValue pose)
        {
            new PoseClipSampler(clip);
            KeyTimes.Ensure(time, clip.Duration);
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
                int replace = KeyTimes.Closest(track.Keys, existing => existing.Time, time);
                if (replace >= 0) key.Time = track.Keys[replace].Time;
                for (int index = 0; index < track.Keys.Length; index++)
                {
                    if (index != replace)
                    {
                        keys.Add(track.Keys[index]);
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
                Id = clip.Id, Duration = clip.Duration, Loop = clip.Loop, Tracks = tracks.ToArray(), Values = clip.Values
            };
            new PoseClipSampler(result);
            return result;
        }

        public static PoseClip RecordValue(PoseClip clip, string target, float time, float value)
        {
            new PoseClipSampler(clip);
            KeyTimes.Ensure(time, clip.Duration);
            List<ScalarTrack> tracks = new List<ScalarTrack>();
            bool found = false;
            foreach (ScalarTrack track in clip.Values)
            {
                if (track.Target != target)
                {
                    tracks.Add(track);
                    continue;
                }
                List<ScalarKey> keys = new List<ScalarKey>();
                int replace = KeyTimes.Closest(track.Keys, existing => existing.Time, time);
                float keyTime = replace < 0 ? time : track.Keys[replace].Time;
                for (int index = 0; index < track.Keys.Length; index++)
                {
                    if (index != replace)
                    {
                        keys.Add(track.Keys[index]);
                    }
                }
                keys.Add(new ScalarKey { Time = keyTime, Value = value });
                keys.Sort((left, right) => left.Time.CompareTo(right.Time));
                tracks.Add(new ScalarTrack { Target = target, Keys = keys.ToArray() });
                found = true;
            }
            if (found == false)
            {
                tracks.Add(new ScalarTrack { Target = target, Keys = new[] { new ScalarKey { Time = time, Value = value } } });
            }
            PoseClip result = new PoseClip { Id = clip.Id, Duration = clip.Duration, Loop = clip.Loop, Tracks = clip.Tracks, Values = tracks.ToArray() };
            new PoseClipSampler(result);
            return result;
        }
    }
}
