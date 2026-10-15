using System;
using System.Collections.Generic;
using System.Numerics;
using Motion.Serialization;
using Motion.Unity;
using NUnit.Framework;

namespace Motion.Tests
{
    public sealed class MotionTests
    {
        [Test]
        public void RecordsInOrderReplacesNearTimeAndPreservesPriorDraft()
        {
            PoseClip original = Clip();
            PoseValue pose = new PoseValue(new Vector3(7, 0, 0), Quaternion.Identity, Vector3.One);
            PoseClip recorded = PoseClipEditing.Record(original, "node", 1, pose);
            recorded = PoseClipEditing.Record(recorded, "node", 1.000001f, pose);
            Assert.That(recorded.Tracks[0].Keys.Length, Is.EqualTo(3));
            Assert.That(recorded.Tracks[0].Keys[1].Position[0], Is.EqualTo(7));
            Assert.That(original.Tracks[0].Keys.Length, Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => PoseClipEditing.Record(recorded, "node", 9, pose));
        }

        [Test]
        public void SamplesTranslationRotationScaleAndClamps()
        {
            PoseClipSampler sampler = new PoseClipSampler(Clip());
            PoseValue middle = sampler.Sample(1)[0].Pose;
            Assert.That(middle.Position.X, Is.EqualTo(1).Within(0.00001));
            Assert.That(middle.Scale.X, Is.EqualTo(1.5f).Within(0.00001));
            Assert.That(Vector3.Transform(Vector3.UnitX, middle.Rotation).Y, Is.EqualTo(1).Within(0.00001));
            Assert.That(sampler.Sample(-1)[0].Pose.Position.X, Is.EqualTo(0));
            Assert.That(sampler.Sample(20)[0].Pose.Position.X, Is.EqualTo(2));
        }

        [Test]
        public void LoopsNegativeTimeAndSnapshotsInput()
        {
            PoseClip clip = Clip();
            clip.Loop = true;
            PoseClipSampler sampler = new PoseClipSampler(clip);
            clip.Tracks[0].Keys[1].Position[0] = 500;
            Assert.That(sampler.Sample(-0.5f)[0].Pose.Position.X, Is.EqualTo(1.5f).Within(0.00001));
            Assert.That(sampler.Sample(2)[0].Pose.Position.X, Is.EqualTo(0));
        }

        [Test]
        public void RejectsInvalidTimesQuaternionAndDuplicateTargets()
        {
            PoseClip clip = Clip();
            clip.Tracks[0].Keys[1].Time = 0;
            Assert.Throws<ArgumentException>(() => new PoseClipSampler(clip));
            clip = Clip();
            clip.Tracks[0].Keys[1].Rotation[3] = 50;
            Assert.Throws<ArgumentException>(() => new PoseClipSampler(clip));
            clip = Clip();
            clip.Tracks = new[] { clip.Tracks[0], clip.Tracks[0] };
            Assert.Throws<ArgumentException>(() => new PoseClipSampler(clip));
            Assert.Throws<ArgumentException>(() => new PoseClipSampler(Clip()).Sample(float.NaN));
        }

        [Test]
        public void MissingTargetDoesNotPartiallyApplyFrame()
        {
            PoseClip clip = Clip();
            clip.Tracks = new[] { clip.Tracks[0], new PoseTrack { Node = "missing", Keys = clip.Tracks[0].Keys } };
            DictionaryTarget target = new DictionaryTarget();
            Assert.Throws<KeyNotFoundException>(() => new PoseClipSampler(clip).Apply(1, target));
            Assert.That(target.Values["node"].Position.X, Is.EqualTo(0));
        }

        [Test]
        public void JsonRoundTripRejectsUnknownAndDuplicateFields()
        {
            string json = PoseClipJson.Write(Clip());
            Assert.That(PoseClipJson.Read(json).Tracks.Length, Is.EqualTo(1));
            Assert.Catch<Exception>(() => PoseClipJson.Read(json.Replace("\"Version\": 1", "\"Version\": 1, \"extra\": true")));
            Assert.Catch<Exception>(() => PoseClipJson.Read(json.Replace("\"Version\": 1", "\"Version\": 1, \"Version\": 1")));
        }

        [Test]
        public void UnityAdapterUsesSameCoordinateFrameInBothDirections()
        {
            UnityEngine.GameObject node = new UnityEngine.GameObject("pose-test");
            try
            {
                TransformPoseTarget target = new TransformPoseTarget(id => node.transform);
                new PoseClipSampler(Clip()).Apply(1, target);
                PoseValue pose = target.Read("node");
                Assert.That(pose.Position.X, Is.EqualTo(1).Within(0.00001));
                Assert.That(node.transform.localPosition.z, Is.EqualTo(-3));
                Assert.That(Math.Abs(Quaternion.Dot(pose.Rotation, new PoseClipSampler(Clip()).Sample(1)[0].Pose.Rotation)), Is.EqualTo(1).Within(0.00001));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(node);
            }
        }

        private static PoseClip Clip()
        {
            return new PoseClip
            {
                Id = "turn", Duration = 2,
                Tracks = new[]
                {
                    new PoseTrack
                    {
                        Node = "node", Keys = new[]
                        {
                            new PoseKey { Time = 0, Position = new[] { 0f, 0f, 3f }, Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } },
                            new PoseKey { Time = 2, Position = new[] { 2f, 0f, 3f }, Rotation = new[] { 0f, 0f, 1f, 0f }, Scale = new[] { 2f, 2f, 2f } }
                        }
                    }
                }
            };
        }

        private sealed class DictionaryTarget : IPoseTarget
        {
            public readonly Dictionary<string, PoseValue> Values = new Dictionary<string, PoseValue>
            {
                { "node", new PoseValue(Vector3.Zero, Quaternion.Identity, Vector3.One) }
            };
            public PoseValue Read(string id) { return Values[id]; }
            public void Write(string id, PoseValue pose) { Values[id] = pose; }
        }
    }
}
