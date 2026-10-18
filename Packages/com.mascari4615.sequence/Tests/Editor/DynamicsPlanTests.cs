using System;
using System.Collections.Generic;
using System.Numerics;
using Motion;
using NUnit.Framework;
using SceneContent;
using SceneContent.Serialization;

namespace SceneSequence.Tests
{
    public sealed class DynamicsPlanTests
    {
        [Test]
        public void FailedSeekRestoresPosesScalarsAndSimulationHistory()
        {
            SceneDocument scene = Document(); scene.Colliders[0].Radius = 2;
            Port port = new Port(scene); float number = 0;
            FloatPropertyTarget values = new FloatPropertyTarget(); values.Bind("number", () => number, value => number = value, 0, 100);
            SceneDynamicsClock clock = new SceneDynamicsClock(scene, port, values, new[] { "number" });
            Action<float> sample = time =>
            {
                PoseValue root = port.Read("root"); root.Position.X = time; port.Write("root", root);
                PoseValue obstacle = port.Read("obstacle"); obstacle.Position.X = time; obstacle.Position.Y = time >= .2f ? -.2f : -3; port.Write("obstacle", obstacle);
                values.Write("number", time);
            };
            clock.Evaluate(.1f, sample);
            Quaternion previous = port.Read("joint").Rotation;
            Assert.Throws<ArgumentException>(() => clock.Evaluate(.5f, sample));
            Assert.That(port.Read("root").Position.X, Is.EqualTo(.1f));
            Assert.That(port.Read("obstacle").Position.Y, Is.EqualTo(-3));
            Assert.That(number, Is.EqualTo(.1f));
            Assert.That(port.Read("joint").Rotation, Is.EqualTo(previous));
            clock.Evaluate(.15f, sample);
            Quaternion resumed = port.Read("joint").Rotation;
            clock.Invalidate(); clock.Evaluate(.15f, sample);
            Assert.That(Math.Abs(Quaternion.Dot(port.Read("joint").Rotation, resumed)), Is.EqualTo(1).Within(.000001));
        }

        [Test]
        public void ParameterChangesCannotCollapseASpringTipOrChangeItsProfileOnFailure()
        {
            SceneDocument scene = Document();
            scene.Parameters = new SceneParameterProfile
            {
                Profile = new Appearance.ParameterProfile
                {
                    Id = "height", Properties = new[] { new Appearance.ParameterProperty { Id = "tip-height", Baseline = -.5f, Minimum = -.5f, Maximum = .5f } },
                    Controls = new[] { new Appearance.ParameterControl { Id = "raise", Label = "Height", Minimum = 0, Maximum = 1, Influences = new[] { new Appearance.ParameterInfluence { Property = "tip-height", Weight = .5f } } } }
                },
                Bindings = new[] { new SceneParameterBinding { Property = "tip-height", Kind = "position", Node = "tip", Component = 1 } }
            };
            Assert.Throws<ArgumentException>(() => SceneParameters.SetValues(scene, new Dictionary<string, float> { { "raise", 1 } }));
            Assert.That(scene.Nodes[2].Position[1], Is.EqualTo(-.5f));
            Assert.That(scene.Parameters.Profile.Controls[0].Value, Is.Zero);
        }

        [Test]
        public void PausedRefreshPreservesLiveInputsWithoutResamplingMotion()
        {
            SceneDocument scene = Document(); Port port = new Port(scene);
            SceneDynamicsClock clock = new SceneDynamicsClock(scene, port);
            int calls = 0;
            Action<float> sample = time => { calls++; PoseValue root = port.Read("root"); root.Position.X = time; port.Write("root", root); };
            clock.Evaluate(.5f, sample);
            int sampled = calls;
            PoseValue changed = port.Read("root"); changed.Position.X = .7f; port.Values["root"] = changed;
            clock.Refresh(sample, () => { });
            Assert.That(calls, Is.EqualTo(sampled));
            Assert.That(port.Read("root").Position.X, Is.EqualTo(.7f));
        }

        [Test]
        public void TipKeysCannotCrossTheJointEvenWhenTheirEndpointsAreNonzero()
        {
            SceneDocument scene = Document();
            PoseClip clip = new PoseClip { Id = "crossing", Duration = 1, Tracks = new PoseTrack[0] };
            clip = PoseClipEditing.Record(clip, "tip", 0, new PoseValue(-Vector3.UnitY, Quaternion.Identity, Vector3.One));
            clip = PoseClipEditing.Record(clip, "tip", 1, new PoseValue(Vector3.UnitY, Quaternion.Identity, Vector3.One));
            SequenceDocument sequence = new SequenceDocument { Id = "crossing", Scene = scene, Clips = new[] { clip } };
            Assert.Throws<ArgumentException>(() => SequenceValidation.Ensure(sequence));
            scene.Springs[0].Enabled = false;
            Assert.DoesNotThrow(() => SequenceValidation.Ensure(sequence));
        }

        [Test]
        public void WorkBudgetIsCheckedBeforeSamplingOrWriting()
        {
            SceneDocument scene = Document(); Port port = new Port(scene);
            SceneDynamicsClock clock = new SceneDynamicsClock(scene, port) { StepBudget = 1 };
            int calls = 0;
            Assert.Throws<ArgumentException>(() => clock.Evaluate(1, time => calls++));
            Assert.That(calls, Is.Zero); Assert.That(port.Writes, Is.Zero);
        }

        [Test]
        public void LaterImpossibleCollisionDoesNotPublishEarlierJointSolutions()
        {
            SceneDocument scene = Document();
            List<SceneNode> nodes = new List<SceneNode>(scene.Nodes) { Node("end", 2, -.3f) };
            scene.Nodes = nodes.ToArray(); scene.Nodes[3].Position[1] = -.7f; scene.Colliders[0].Radius = 2;
            scene.Springs[0].Colliders = new string[0];
            scene.Springs = new[] { scene.Springs[0], new SceneSpring { Id = "second", Joint = "tip", Tip = "end", Colliders = new[] { "guard" } } };
            Port port = new Port(scene);
            Assert.Throws<ArgumentException>(() => new SceneDynamicsPlan(scene).Apply(port, 1f / 120));
            Assert.That(port.Writes, Is.Zero);
            Assert.That(port.Read("joint").Rotation, Is.EqualTo(Quaternion.Identity));
        }

        [Test]
        public void FixedStepPlaybackAndBackwardSeekingProduceTheSamePose()
        {
            SceneDocument scene = Document();
            Port first = new Port(scene); Port second = new Port(scene);
            SceneDynamicsClock incremental = new SceneDynamicsClock(scene, first);
            SceneDynamicsClock direct = new SceneDynamicsClock(scene, second);
            Action<float, Port> sample = (time, port) => { PoseValue root = port.Read("root"); root.Position.X = (float)Math.Sin(time * 5) * .2f; port.Write("root", root); };
            for (int index = 0; index <= 60; index++) incremental.Evaluate(index / 60f, time => sample(time, first));
            direct.Evaluate(1, time => sample(time, second));
            Assert.That(Quaternion.Dot(first.Read("joint").Rotation, second.Read("joint").Rotation), Is.EqualTo(1).Within(.000001));
            direct.Evaluate(.5f, time => sample(time, second));
            SceneDynamicsClock fresh = new SceneDynamicsClock(scene, first);
            fresh.Evaluate(.5f, time => sample(time, first));
            Assert.That(Math.Abs(Quaternion.Dot(first.Read("joint").Rotation, second.Read("joint").Rotation)), Is.EqualTo(1).Within(.000001));
            Assert.That(Math.Abs(first.Read("joint").Rotation.Z), Is.GreaterThan(.01f));
        }

        [Test]
        public void InvalidLiveScaleAndMissingPoseDoNotWriteAnySpringJoint()
        {
            SceneDocument scene = Document();
            Port port = new Port(scene);
            PoseValue root = port.Read("root"); root.Scale.X = 2; port.Values["root"] = root;
            SceneDynamicsPlan plan = new SceneDynamicsPlan(scene);
            Assert.Throws<ArgumentException>(() => plan.Apply(port, 1f / 120));
            Assert.That(port.Writes, Is.Zero);
            port = new Port(scene); port.Values.Remove("tip");
            Assert.Throws<KeyNotFoundException>(() => plan.Apply(port, 1f / 120));
            Assert.That(port.Writes, Is.Zero);
        }

        [Test]
        public void MetadataSurvivesJsonCompositionAndPruningWithoutAliasing()
        {
            SceneDocument scene = Document();
            scene.Springs[0].Enabled = false;
            SceneDocument read = SceneJson.Read(SceneJson.Write(scene));
            SceneDocument copy = SceneCloning.Copy(read);
            copy.Springs[0].Acceleration[1] = 1;
            Assert.That(scene.Springs[0].Acceleration[1], Is.EqualTo(-9.81f));
            SceneDocument composed = SceneComposition.Append(scene, read, "second", new float[3]);
            Assert.That(composed.Springs[1].Joint, Is.EqualTo("second:joint"));
            Assert.That(composed.Springs[1].Colliders[0], Is.EqualTo("second:guard"));
            SceneDocument pruned = ScenePruning.Snapshot(read, new string[0]);
            Assert.That(Array.Exists(pruned.Nodes, node => node.Id == "joint"), Is.True);
            Assert.That(Array.Exists(pruned.Nodes, node => node.Id == "tip"), Is.True);
            Assert.That(pruned.Springs[0].Enabled, Is.False);
        }

        [Test]
        public void FeedbackColliderAndCompetingWritersAreRejected()
        {
            SceneDocument scene = Document();
            scene.Colliders[0].Node = "tip";
            Assert.Throws<ArgumentException>(() => SceneValidation.Ensure(scene));
            scene = Document(); scene.Springs = new[] { scene.Springs[0], new SceneSpring { Id = "other", Joint = "joint", Tip = "tip" } };
            Assert.Throws<ArgumentException>(() => SceneValidation.Ensure(scene));
        }

        [Test]
        public void ChainDistributionPreservesIdentityAndColliderBindings()
        {
            SceneDocument scene = Document();
            SceneSpringAuthoring.SetChain(scene, "root", "tip", new SpringDistribution { FrequencyStart = 1, FrequencyEnd = 3 });
            Assert.That(scene.Springs.Length, Is.EqualTo(2));
            SceneSpring joint = Array.Find(scene.Springs, value => value.Joint == "joint");
            Assert.That(joint.Id, Is.EqualTo("spring"));
            Assert.That(joint.FrequencyHz, Is.EqualTo(3));
            Assert.That(joint.Colliders, Is.EqualTo(new[] { "guard" }));
            Assert.That(Array.Find(scene.Springs, value => value.Joint == "root").FrequencyHz, Is.EqualTo(1));
        }

        private static SceneDocument Document()
        {
            return new SceneDocument
            {
                Id = "spring-test", Nodes = new[] { Node("root", -1, 0), Node("joint", 0, -.2f), Node("tip", 1, -.5f), Node("obstacle", -1, -3) },
                Meshes = new MeshInstance[0], Materials = new SurfaceMaterial[0], Resources = new ContentResource[0],
                Springs = new[] { new SceneSpring { Id = "spring", Joint = "joint", Tip = "tip", Colliders = new[] { "guard" } } },
                Colliders = new[] { new SceneCollider { Id = "guard", Node = "obstacle", Radius = .1f } }
            };
        }

        private static SceneNode Node(string id, int parent, float y)
        {
            return new SceneNode { Id = id, Parent = parent, Position = new[] { 0f, y, 0f }, Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } };
        }

        private sealed class Port : IPoseTarget
        {
            public readonly Dictionary<string, PoseValue> Values = new Dictionary<string, PoseValue>();
            public int Writes;
            public Port(SceneDocument scene)
            {
                foreach (SceneNode node in scene.Nodes) Values.Add(node.Id, new PoseValue(new Vector3(node.Position[0], node.Position[1], node.Position[2]), Quaternion.Identity, Vector3.One));
            }
            public PoseValue Read(string id) { return Values[id]; }
            public void Write(string id, PoseValue value) { Writes++; Values[id] = value; }
        }
    }
}
