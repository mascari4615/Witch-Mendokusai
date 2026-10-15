using System;

namespace Motion
{
    public static class ClipTime
    {
        public static float Normalize(float time, float duration, bool loop)
        {
            if (float.IsNaN(time) || float.IsInfinity(time) || float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0)
            {
                throw new ArgumentException("Clip time must be finite and duration must be positive");
            }
            if (loop == false)
            {
                return Math.Max(0, Math.Min(duration, time));
            }
            float wrapped = time % duration;
            if (wrapped < 0)
            {
                wrapped += duration;
            }
            return wrapped >= duration ? 0 : wrapped;
        }
    }
}
