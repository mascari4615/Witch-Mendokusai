using Motion.Serialization;
using NUnit.Framework;

namespace Motion.Tests
{
    public sealed class TrackEditingTests
    {
        [Test]
        public void DraftHistorySnapshotsMutableInputAndSupportsRedo()
        {
            PoseClip clip = new PoseClip { Id = "history", Duration = 2, Tracks = new PoseTrack[0] };
            clip = PoseClipEditing.RecordValue(clip, "value", 0, 4);
            MotionDraftHistory history = new MotionDraftHistory();
            history.Record(clip);
            clip.Values[0].Keys[0].Value = 8;
            PoseClip previous = history.Undo(clip);
            Assert.That(previous.Values[0].Keys[0].Value, Is.EqualTo(4));
            Assert.That(history.Redo(previous).Values[0].Keys[0].Value, Is.EqualTo(8));
        }

        [Test]
        public void RemovesOnlyChosenKeyAndDropsEmptyTracksWithoutChangingSource()
        {
            PoseClip clip = new PoseClip { Id = "edit", Duration = 2, Tracks = new PoseTrack[0] };
            PoseValue pose = new PoseValue(System.Numerics.Vector3.Zero, System.Numerics.Quaternion.Identity, System.Numerics.Vector3.One);
            clip = PoseClipEditing.Record(clip, "joint", 0, pose);
            clip = PoseClipEditing.Record(clip, "joint", 2, pose);
            clip = PoseClipEditing.RecordValue(clip, "joint", 0, 5);
            PoseClip result = TrackEditing.Remove(clip, "joint", TrackKind.Pose, 0);
            Assert.That(result.Tracks[0].Keys.Length, Is.EqualTo(1));
            Assert.That(result.Values.Length, Is.EqualTo(1));
            Assert.That(clip.Tracks[0].Keys.Length, Is.EqualTo(2));
            result = TrackEditing.Remove(result, "joint", TrackKind.Pose, 2);
            Assert.That(result.Tracks, Is.Empty);
            result = TrackEditing.Remove(result, "joint", TrackKind.Value);
            Assert.That(result.Values, Is.Empty);
            Assert.That(PoseClipJson.Read(PoseClipJson.Write(result)).Tracks, Is.Empty);
        }
    }
}
