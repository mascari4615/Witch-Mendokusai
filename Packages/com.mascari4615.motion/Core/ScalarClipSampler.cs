using System;
using System.Collections.Generic;

namespace Motion
{
    internal sealed class ScalarClipSampler
    {
        private readonly string[] targets;
        private readonly float[][] times;
        private readonly float[][] values;

        public ScalarClipSampler(PoseClip clip, int keyBudget)
        {
            if (clip.Values == null || clip.Values.Length > 10000)
            {
                throw new ArgumentException("Invalid scalar track count");
            }
            targets = new string[clip.Values.Length];
            times = new float[targets.Length][];
            values = new float[targets.Length][];
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            for (int track = 0; track < targets.Length; track++)
            {
                ScalarTrack source = clip.Values[track];
                if (source == null || string.IsNullOrEmpty(source.Target) || source.Target.Length > 1024 ||
                    unique.Add(source.Target) == false || source.Keys == null || source.Keys.Length == 0)
                {
                    throw new ArgumentException("Invalid scalar track");
                }
                count += source.Keys.Length;
                if (count > keyBudget)
                {
                    throw new ArgumentException("Motion key count exceeds limit");
                }
                targets[track] = source.Target;
                times[track] = new float[source.Keys.Length];
                values[track] = new float[source.Keys.Length];
                for (int key = 0; key < source.Keys.Length; key++)
                {
                    ScalarKey value = source.Keys[key];
                    if (value == null || Finite(value.Time) == false || Finite(value.Value) == false || value.Time < 0 || value.Time > clip.Duration ||
                        (key > 0 && value.Time <= times[track][key - 1]))
                    {
                        throw new ArgumentException("Scalar keys require finite values and strictly increasing times inside duration");
                    }
                    times[track][key] = value.Time;
                    values[track][key] = value.Value;
                }
            }
        }

        public void ValidateTargets(IValueTarget target)
        {
            if (targets.Length > 0 && target == null)
            {
                throw new ArgumentException("Scalar motion requires a value port");
            }
            for (int track = 0; track < targets.Length; track++)
            {
                foreach (float value in values[track])
                {
                    target.Validate(targets[track], value);
                }
            }
        }

        public ValueSample[] Sample(float time)
        {
            ValueSample[] samples = new ValueSample[targets.Length];
            for (int track = 0; track < targets.Length; track++)
            {
                float[] keys = times[track];
                int right = Array.BinarySearch(keys, time);
                if (right < 0)
                {
                    right = ~right;
                }
                float value;
                if (right == 0)
                {
                    value = values[track][0];
                }
                else if (right >= keys.Length)
                {
                    value = values[track][keys.Length - 1];
                }
                else
                {
                    float amount = (time - keys[right - 1]) / (keys[right] - keys[right - 1]);
                    double weight = amount;
                    value = (float)(values[track][right - 1] * (1 - weight) + values[track][right] * weight);
                    if (Finite(value) == false)
                    {
                        throw new ArgumentException("Interpolated scalar exceeds numeric range");
                    }
                }
                samples[track] = new ValueSample { Target = targets[track], Value = value };
            }
            return samples;
        }

        private static bool Finite(float value)
        {
            return float.IsNaN(value) == false && float.IsInfinity(value) == false;
        }
    }
}
