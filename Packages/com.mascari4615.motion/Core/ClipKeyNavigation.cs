using System;

namespace Motion
{
    public static class ClipKeyNavigation
    {
        public static float? Adjacent(PoseClip clip, float time, bool forward)
        {
            new PoseClipSampler(clip);
            if (float.IsNaN(time) || float.IsInfinity(time))
            {
                throw new ArgumentException("Key navigation time must be finite");
            }
            float? closest = null;
            foreach (PoseTrack track in clip.Tracks)
            {
                foreach (PoseKey key in track.Keys)
                {
                    Consider(key.Time, time, forward, ref closest);
                }
            }
            foreach (ScalarTrack track in clip.Values)
            {
                foreach (ScalarKey key in track.Keys)
                {
                    Consider(key.Time, time, forward, ref closest);
                }
            }
            return closest;
        }

        private static void Consider(float candidate, float current, bool forward, ref float? closest)
        {
            if (forward ? candidate <= current : candidate >= current)
            {
                return;
            }
            if (closest.HasValue == false || (forward ? candidate < closest.Value : candidate > closest.Value))
            {
                closest = candidate;
            }
        }
    }
}
