using System;
using System.Collections.Generic;
using System.Numerics;
using Motion;
using NUnit.Framework;
using SceneContent;
using SceneSequence.Serialization;

namespace SceneSequence.Tests
{
    public sealed class ConstraintPlanTests
    {
        [Test]
        public void MovingTargetsRemainReachedAfterMotionAndRepeatedEvaluation()
        {
            SceneDocument document = Document();
            PosePort port = new PosePort(document);
            SceneConstraintPlan plan = new SceneConstraintPlan(document);
            PoseClip clip = new PoseClip { Id = "motion", Duration = 1, Tracks = new PoseTrack[0] };
            clip = PoseClipEditing.Record(clip, "upper", 0, new PoseValue(Vector3.Zero, Quaternion.Identity, Vector3.One));
            clip = PoseClipEditing.Record(clip, "upper", 1, new PoseValue(new Vector3(.2f, 0, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, .4f), Vector3.One));
            clip = PoseClipEditing.Record(clip, "goal", 0, new PoseValue(new Vector3(1, 1, 0), Quaternion.Identity, Vector3.One));
            clip = PoseClipEditing.Record(clip, "goal", 1, new PoseValue(new Vector3(1.3f, .8f, 0), Quaternion.Identity, Vector3.One));
            PoseClipSampler sampler = new PoseClipSampler(clip);
            foreach (float seconds in new[] { 0, .25f, .5f, 1 })
            {
                sampler.Apply(seconds, port);
                Assert.That(plan.Apply(port)[0].Reachable, Is.True);
                Vector3 goal = port.Read("goal").Position;
                Assert.That(Vector3.Distance(port.End(), goal), Is.LessThan(.00001f));
            }
            for (int iteration = 0; iteration < 100; iteration++) plan.Apply(port);
            Assert.That(Vector3.Distance(port.End(), port.Read("goal").Position), Is.LessThan(.00001f));
        }

        [Test]
        public void InvalidScaleOrMissingPortCannotWriteHalfAConstraintFrame()
        {
            SceneDocument document = Document();
            PosePort port = new PosePort(document);
            SceneConstraintPlan plan = new SceneConstraintPlan(document);
            port.Poses["lower"] = new PoseValue(Vector3.UnitX, Quaternion.Identity, new Vector3(1, 2, 1));
            Assert.Throws<ArgumentException>(() => plan.Apply(port));
            Assert.That(port.Writes, Is.Zero);
            port.Poses["lower"] = new PoseValue(Vector3.UnitX, Quaternion.Identity, Vector3.One);
            port.Poses.Remove("pole");
            Assert.Throws<KeyNotFoundException>(() => plan.Apply(port));
            Assert.That(port.Writes, Is.Zero);
        }

        [Test]
        public void ConstraintsRoundTripComposePruneAndDisableWithoutLosingTargets()
        {
            SceneDocument document = Document();
            SequenceDocument source = new SequenceDocument { Id = "sequence", Scene = document };
            SceneDocument loaded = SequenceJson.Read(SequenceJson.Write(source)).Scene;
            SceneDocument composed = SceneComposition.Append(loaded, loaded, "guest", new[] { 3f, 0f, 0f });
            SceneDocument pruned = ScenePruning.Snapshot(composed, new string[0]);
            Assert.That(pruned.LimbConstraints.Length, Is.EqualTo(2));
            Assert.That(Array.Exists(pruned.Nodes, node => node.Id == "guest:goal"), Is.True);
            Assert.That(pruned.LimbConstraints[1].Upper, Is.EqualTo("guest:upper"));
            loaded.LimbConstraints[0].Enabled = false;
            Assert.That(new SceneConstraintPlan(loaded).Count, Is.Zero);
            Assert.That(document.LimbConstraints[0].Enabled, Is.True);
        }

        [Test]
        public void CompetingWritesAndFeedbackCyclesAreRejectedBeforeActivation()
        {
            SceneDocument document = Document();
            document.LimbConstraints = new[] { document.LimbConstraints[0], new SceneLimbConstraint { Id = "duplicate", Upper = "upper", Lower = "lower", End = "end", Goal = "goal", Pole = "pole" } };
            Assert.Throws<ArgumentException>(() => SceneValidation.Ensure(document));
            document = Document();
            document.Nodes[3].Parent = 2;
            Assert.Throws<ArgumentException>(() => SceneValidation.Ensure(document));
            document = SceneComposition.Append(Document(), Document(), "other", new float[3]);
            int firstGoal = Array.FindIndex(document.Nodes, node => node.Id == "goal");
            int secondGoal = Array.FindIndex(document.Nodes, node => node.Id == "other:goal");
            document.Nodes[firstGoal].Parent = Array.FindIndex(document.Nodes, node => node.Id == "other:end");
            document.Nodes[secondGoal].Parent = Array.FindIndex(document.Nodes, node => node.Id == "end");
            Assert.Throws<ArgumentException>(() => SceneValidation.Ensure(document));
        }

        private static SceneDocument Document()
        {
            string[] ids = { "upper", "lower", "end", "goal", "pole" };
            Vector3[] positions = { Vector3.Zero, Vector3.UnitX, Vector3.UnitX, new Vector3(1, 1, 0), new Vector3(0, 2, 0) };
            int[] parents = { -1, 0, 1, -1, -1 };
            SceneNode[] nodes = new SceneNode[ids.Length];
            for (int index = 0; index < ids.Length; index++) nodes[index] = new SceneNode { Id = ids[index], Parent = parents[index], Position = new[] { positions[index].X, positions[index].Y, positions[index].Z }, Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } };
            return new SceneDocument { Id = "limb-scene", Nodes = nodes, Meshes = new MeshInstance[0], Materials = new SurfaceMaterial[0], Resources = new ContentResource[0],
                LimbConstraints = new[] { new SceneLimbConstraint { Id = "arm", Upper = "upper", Lower = "lower", End = "end", Goal = "goal", Pole = "pole" } } };
        }

        [Test]
        public void UnsupportedScaleKeysAndControlChangesAreRejectedBeforeReplacingState()
        {
            SceneDocument document = Document();
            SceneParameterAuthoring.AddScaleControl(document, "upper", "width", "Width", 0, .5f, 2);
            SequenceDocument sequence = new SequenceDocument { Id = "sequence", Scene = document };
            string before = SequenceJson.Write(sequence);
            Assert.Throws<ArgumentException>(() => SceneParameters.SetValues(document, new Dictionary<string, float> { { "width", 1.2f } }));
            Assert.That(SequenceJson.Write(sequence), Is.EqualTo(before));
            PoseClip clip = PoseClipEditing.Record(new PoseClip { Id = "unsupported", Duration = 1, Tracks = new PoseTrack[0] }, "upper", 0,
                new PoseValue(Vector3.Zero, Quaternion.Identity, new Vector3(1, 2, 1)));
            sequence.Clips = new[] { clip };
            Assert.Throws<ArgumentException>(() => SequenceValidation.Ensure(sequence));
            document.LimbConstraints[0].Enabled = false;
            SceneParameters.SetValues(document, new Dictionary<string, float> { { "width", 1.2f } });
            Assert.That(new SceneConstraintPlan(document).Count, Is.Zero);
        }

        [Test]
        public void DependencyOrderFollowsMovingTargetsInsteadOfSerializedArrayOrder()
        {
            SceneDocument document = SceneComposition.Append(Document(), Document(), "other", new[] { 3f, 0f, 0f });
            int goal = Array.FindIndex(document.Nodes, node => node.Id == "other:goal");
            document.Nodes[goal].Parent = Array.FindIndex(document.Nodes, node => node.Id == "end");
            document.LimbConstraints = new[] { document.LimbConstraints[1], document.LimbConstraints[0] };
            SceneValidation.Ensure(document);
            Assert.That(SceneConstraints.Order(document), Is.EqualTo(new[] { 1, 0 }));
        }

        private sealed class PosePort : IPoseTarget
        {
            public readonly Dictionary<string, PoseValue> Poses = new Dictionary<string, PoseValue>();
            public int Writes;
            public PosePort(SceneDocument scene)
            {
                foreach (SceneNode node in scene.Nodes) Poses.Add(node.Id, new PoseValue(new Vector3(node.Position[0], node.Position[1], node.Position[2]), Quaternion.Identity, Vector3.One));
            }
            public PoseValue Read(string nodeId) { return Poses[nodeId]; }
            public void Write(string nodeId, PoseValue pose) { Poses[nodeId] = pose; Writes++; }
            public Vector3 End()
            {
                PoseValue upper = Read("upper");
                PoseValue lower = Read("lower");
                return upper.Position + Vector3.Transform(lower.Position, upper.Rotation) + Vector3.Transform(Read("end").Position, upper.Rotation * lower.Rotation);
            }
        }
    }
}
