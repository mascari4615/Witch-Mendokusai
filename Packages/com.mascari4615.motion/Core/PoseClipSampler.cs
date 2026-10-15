using System;
using System.Collections.Generic;
using System.Numerics;

namespace Motion
{
    public sealed class PoseClipSampler
    {
        private readonly string[] nodes;
        private readonly float[][] times;
        private readonly PoseValue[][] poses;
        private readonly ScalarClipSampler scalar;
        public float Duration { get; private set; }
        public bool Loop { get; private set; }

        public PoseClipSampler(PoseClip clip)
        {
            if (clip == null || clip.Format != "pose-clip" || clip.Version != 1 || string.IsNullOrEmpty(clip.Id) ||
                Finite(clip.Duration) == false || clip.Duration <= 0 || clip.Tracks == null || clip.Tracks.Length > 10000)
            {
                throw new ArgumentException("Invalid pose clip header");
            }
            Duration = clip.Duration;
            Loop = clip.Loop;
            nodes = new string[clip.Tracks.Length];
            times = new float[nodes.Length][];
            poses = new PoseValue[nodes.Length][];
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            int keyCount = 0;
            for (int track = 0; track < nodes.Length; track++)
            {
                PoseTrack source = clip.Tracks[track];
                if (source == null || string.IsNullOrEmpty(source.Node) || source.Node.Length > 1024 ||
                    unique.Add(source.Node) == false || source.Keys == null || source.Keys.Length == 0)
                {
                    throw new ArgumentException("Invalid pose track");
                }
                keyCount += source.Keys.Length;
                if (keyCount > 1000000)
                {
                    throw new ArgumentException("Pose key count exceeds limit");
                }
                nodes[track] = source.Node;
                times[track] = new float[source.Keys.Length];
                poses[track] = new PoseValue[source.Keys.Length];
                for (int key = 0; key < source.Keys.Length; key++)
                {
                    PoseKey value = source.Keys[key];
                    if (value == null || Finite(value.Time) == false || value.Time < 0 || value.Time > Duration ||
                        (key > 0 && value.Time <= times[track][key - 1]))
                    {
                        throw new ArgumentException("Pose keys must have strictly increasing times inside duration");
                    }
                    times[track][key] = value.Time;
                    CheckArray(value.Position, 3);
                    CheckArray(value.Rotation, 4);
                    CheckArray(value.Scale, 3);
                    Quaternion rotation = new Quaternion(value.Rotation[0], value.Rotation[1], value.Rotation[2], value.Rotation[3]);
                    if (Math.Abs(rotation.LengthSquared() - 1) > 0.001f)
                    {
                        throw new ArgumentException("Pose rotations must be normalized");
                    }
                    poses[track][key] = new PoseValue(
                        new Vector3(value.Position[0], value.Position[1], value.Position[2]),
                        Quaternion.Normalize(rotation), new Vector3(value.Scale[0], value.Scale[1], value.Scale[2]));
                }
            }
            scalar = new ScalarClipSampler(clip, 1000000 - keyCount);
        }

        public PoseSample[] Sample(float time)
        {
            if (Finite(time) == false)
            {
                throw new ArgumentException("Sample time must be finite");
            }
            float local = ClipTime.Normalize(time, Duration, Loop);
            PoseSample[] samples = new PoseSample[nodes.Length];
            for (int track = 0; track < nodes.Length; track++)
            {
                float[] keys = times[track];
                int right = Array.BinarySearch(keys, local);
                if (right < 0)
                {
                    right = ~right;
                }
                PoseValue value;
                if (right == 0)
                {
                    value = poses[track][0];
                }
                else if (right >= keys.Length)
                {
                    value = poses[track][keys.Length - 1];
                }
                else
                {
                    PoseValue leftValue = poses[track][right - 1];
                    PoseValue rightValue = poses[track][right];
                    float amount = (local - keys[right - 1]) / (keys[right] - keys[right - 1]);
                    value = new PoseValue(Interpolate(leftValue.Position, rightValue.Position, amount),
                        Quaternion.Normalize(Quaternion.Slerp(leftValue.Rotation, rightValue.Rotation, amount)),
                        Interpolate(leftValue.Scale, rightValue.Scale, amount));
                }
                samples[track] = new PoseSample(nodes[track], value);
            }
            return samples;
        }

        public ValueSample[] SampleValues(float time)
        {
            if (Finite(time) == false)
            {
                throw new ArgumentException("Sample time must be finite");
            }
            float local = ClipTime.Normalize(time, Duration, Loop);
            return scalar.Sample(local);
        }

        public void ValidateTargets(IPoseTarget target, IValueTarget valueTarget = null)
        {
            if (nodes.Length > 0 && target == null)
            {
                throw new ArgumentException("Pose motion requires a pose port");
            }
            foreach (string node in nodes)
            {
                target.Read(node);
            }
            scalar.ValidateTargets(valueTarget);
        }

        public void Apply(float time, IPoseTarget target, IValueTarget valueTarget = null)
        {
            PoseSample[] samples = Sample(time);
            ValueSample[] properties = SampleValues(time);
            if ((samples.Length > 0 && target == null) || (properties.Length > 0 && valueTarget == null))
            {
                throw new ArgumentException("Motion clip requires bound pose and value ports");
            }
            foreach (PoseSample sample in samples)
            {
                target.Read(sample.Node);
            }
            foreach (ValueSample sample in properties)
            {
                valueTarget.Validate(sample.Target, sample.Value);
            }
            foreach (PoseSample sample in samples)
            {
                target.Write(sample.Node, sample.Pose);
            }
            foreach (ValueSample sample in properties)
            {
                valueTarget.Write(sample.Target, sample.Value);
            }
        }

        private static void CheckArray(float[] values, int length)
        {
            if (values == null || values.Length != length)
            {
                throw new ArgumentException("Invalid pose component size");
            }
            foreach (float value in values)
            {
                if (Finite(value) == false)
                {
                    throw new ArgumentException("Pose components must be finite");
                }
            }
        }

        private static Vector3 Interpolate(Vector3 left, Vector3 right, float amount)
        {
            double weight = amount;
            return new Vector3((float)(left.X * (1 - weight) + right.X * weight),
                (float)(left.Y * (1 - weight) + right.Y * weight), (float)(left.Z * (1 - weight) + right.Z * weight));
        }

        private static bool Finite(float value)
        {
            return float.IsNaN(value) == false && float.IsInfinity(value) == false;
        }
    }
}
