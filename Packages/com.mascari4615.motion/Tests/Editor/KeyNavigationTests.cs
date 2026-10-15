using NUnit.Framework;

namespace Motion.Tests
{
    public sealed class KeyNavigationTests
    {
        [Test]
        public void VeryCloseKeysCanBeVisitedAndOnlyTheNearestOneIsDeleted()
        {
            PoseClip clip = new PoseClip
            {
                Id = "dense", Duration = 1, Tracks = new PoseTrack[0],
                Values = new[] { new ScalarTrack { Target = "value", Keys = new[] { new ScalarKey { Time = 0, Value = 1 }, new ScalarKey { Time = .000001f, Value = 2 } } } }
            };
            Assert.That(ClipKeyNavigation.Adjacent(clip, 0, true), Is.EqualTo(.000001f));
            PoseClip recorded = PoseClipEditing.RecordValue(clip, "value", 0, 5);
            Assert.That(recorded.Values[0].Keys.Length, Is.EqualTo(2));
            Assert.That(recorded.Values[0].Keys[0].Value, Is.EqualTo(5));
            Assert.That(recorded.Values[0].Keys[1].Value, Is.EqualTo(2));
            Assert.Throws<System.ArgumentException>(() => PoseClipEditing.RecordValue(clip, "value", -.000001f, 5));
            PoseClip edited = TrackEditing.Remove(clip, "value", TrackKind.Value, 0);
            Assert.That(edited.Values[0].Keys.Length, Is.EqualTo(1));
            Assert.That(edited.Values[0].Keys[0].Time, Is.EqualTo(.000001f));
            Assert.That(clip.Values[0].Keys.Length, Is.EqualTo(2));
        }

        [Test]
        public void NavigatesBothKindsAndClampedPreviewCanReachLoopEnd()
        {
            PoseClip clip = new PoseClip { Id = "navigation", Duration = 2, Loop = true, Tracks = new PoseTrack[0] };
            clip = PoseClipEditing.Record(clip, "joint", 0, new PoseValue(System.Numerics.Vector3.Zero, System.Numerics.Quaternion.Identity, System.Numerics.Vector3.One));
            clip = PoseClipEditing.Record(clip, "joint", 2, new PoseValue(System.Numerics.Vector3.UnitX * 2, System.Numerics.Quaternion.Identity, System.Numerics.Vector3.One));
            clip = PoseClipEditing.RecordValue(clip, "value", 0, 10);
            clip = PoseClipEditing.RecordValue(clip, "value", 1, 20);
            clip = PoseClipEditing.RecordValue(clip, "value", 2, 30);
            Assert.That(ClipKeyNavigation.Adjacent(clip, .5f, true), Is.EqualTo(1));
            Assert.That(ClipKeyNavigation.Adjacent(clip, .5f, false), Is.EqualTo(0));
            Assert.That(ClipKeyNavigation.Adjacent(clip, 2, true), Is.Null);
            PoseClipSampler sampler = new PoseClipSampler(clip);
            Assert.That(sampler.Sample(2)[0].Pose.Position.X, Is.EqualTo(0));
            Assert.That(sampler.Sample(2, false)[0].Pose.Position.X, Is.EqualTo(2));
            Assert.That(sampler.SampleValues(2)[0].Value, Is.EqualTo(10));
            Assert.That(sampler.SampleValues(2, false)[0].Value, Is.EqualTo(30));
        }
    }
}
