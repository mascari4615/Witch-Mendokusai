using System;

namespace Motion
{
    internal static class KeyTimes
    {
        public static void Ensure(float time, float duration)
        {
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0 || time > duration)
            {
                throw new ArgumentException("Key time must be finite and inside the clip duration");
            }
        }

        public static int Closest<T>(T[] keys, Func<T, float> time, float seconds)
        {
            const float TOLERANCE = .00001f;
            int closest = -1;
            float distance = TOLERANCE;
            for (int index = 0; index < keys.Length; index++)
            {
                float candidate = Math.Abs(time(keys[index]) - seconds);
                if (candidate <= TOLERANCE && (closest < 0 || candidate < distance))
                {
                    closest = index;
                    distance = candidate;
                }
            }
            return closest;
        }
    }
}
