using System;
using Motion.Serialization;
using NUnit.Framework;

namespace Motion.Tests
{
    public sealed class ScalarMotionTests
    {
        [Test]
        public void ValueOnlyClipRecordsRoundTripsAndSamplesWithoutPosePort()
        {
            PoseClip clip = Empty();
            clip = PoseClipEditing.RecordValue(clip, "lens", 0, 30);
            clip = PoseClipEditing.RecordValue(clip, "lens", 2, 60);
            float lens = 30;
            FloatPropertyTarget target = new FloatPropertyTarget();
            target.Bind("lens", () => lens, value => lens = value, 1, 179);
            PoseClipSampler sampler = new PoseClipSampler(PoseClipJson.Read(PoseClipJson.Write(clip)));
            sampler.Apply(1, null, target);
            Assert.That(lens, Is.EqualTo(45));
            clip.Values[0].Keys[1].Value = 90;
            Assert.That(sampler.SampleValues(1)[0].Value, Is.EqualTo(45));
            Assert.Throws<ArgumentException>(() => sampler.Apply(1, null));
        }

        [Test]
        public void PropertyFailureRejectsWholeFrameBeforePoseWrites()
        {
            PoseClip clip = PoseClipEditing.RecordValue(Empty(), "lens", 0, 500);
            PoseValue pose = new PoseValue(System.Numerics.Vector3.One, System.Numerics.Quaternion.Identity, System.Numerics.Vector3.One);
            clip = PoseClipEditing.Record(clip, "node", 0, pose);
            Assert.That(clip.Values.Length, Is.EqualTo(1));
            float lens = 30;
            FloatPropertyTarget target = new FloatPropertyTarget();
            target.Bind("lens", () => lens, value => lens = value, 1, 179);
            CountingPoseTarget poses = new CountingPoseTarget();
            Assert.Throws<ArgumentException>(() => new PoseClipSampler(clip).Apply(0, poses, target));
            Assert.That(poses.Writes, Is.EqualTo(0));
            Assert.That(lens, Is.EqualTo(30));
        }

        [Test]
        public void InvalidScalarKeysAndLargeLoopTimesAreHandledExplicitly()
        {
            PoseClip clip = PoseClipEditing.RecordValue(Empty(), "value", 0, 1);
            clip.Values[0].Keys[0].Value = float.NaN;
            Assert.Throws<ArgumentException>(() => new PoseClipSampler(clip));
            float duration = float.MaxValue;
            float local = ClipTime.Normalize(duration * .75f, duration, true);
            Assert.That(float.IsNaN(local) || float.IsInfinity(local), Is.False);
            Assert.That(local, Is.GreaterThan(0));
            Assert.That(ClipTime.Normalize(-.5f, 2, true), Is.EqualTo(1.5f));
        }

        [Test]
        public void TargetPreflightRejectsFutureInvalidKeyWithoutWriting()
        {
            PoseClip clip = PoseClipEditing.RecordValue(Empty(), "lens", 0, 30);
            clip = PoseClipEditing.RecordValue(clip, "lens", 2, 200);
            float lens = 30;
            FloatPropertyTarget target = new FloatPropertyTarget();
            target.Bind("lens", () => lens, value => lens = value, 1, 179);
            PoseClipSampler sampler = new PoseClipSampler(clip);
            Assert.Throws<ArgumentException>(() => sampler.ValidateTargets(null, target));
            Assert.That(lens, Is.EqualTo(30));
        }

        private static PoseClip Empty()
        {
            return new PoseClip { Id = "values", Duration = 2, Tracks = new PoseTrack[0] };
        }

        private sealed class CountingPoseTarget : IPoseTarget
        {
            public int Writes;
            public PoseValue Read(string id) { return new PoseValue(); }
            public void Write(string id, PoseValue pose) { Writes++; }
        }
    }
}
