using System;
using System.Collections.Generic;
using Appearance;
using Motion;
using NUnit.Framework;
using SceneContent;
using SceneSequence.Serialization;

namespace SceneSequence.Tests
{
    public sealed class SequenceParameterTests
    {
        [Test]
        public void ScaleVariantKeepsAnimatedProportionsAtKeysAndBetweenThem()
        {
            SequenceDocument source = Document();
            string before = SequenceJson.Write(source);
            SequenceDocument changed = SequenceParameters.WithValues(source, new Dictionary<string, float> { { "size", 1.5f } });
            Assert.That(changed.Scene.Nodes[0].Scale[0], Is.EqualTo(1.8f).Within(.000001f));
            PoseSample[] samples = new PoseClipSampler(changed.Clips[0]).Sample(.5f);
            Assert.That(samples[0].Pose.Scale.X, Is.EqualTo(1.8f).Within(.000001f));
            Assert.That(samples[0].Pose.Position.X, Is.EqualTo(3));
            Assert.That(changed.Clips[0].Tracks[0].Keys[1].Time, Is.EqualTo(1));
            SequenceDocument reopened = SequenceJson.Read(SequenceJson.Write(changed));
            SequenceDocument restored = SequenceParameters.WithValues(reopened, new Dictionary<string, float> { { "size", 1 } });
            Assert.That(restored.Clips[0].Tracks[0].Keys[1].Scale[0], Is.EqualTo(1.4f).Within(.000001f));
            changed.Clips[0].Tracks[0].Keys[0].Rotation[0] = .5f;
            Assert.That(SequenceJson.Write(source), Is.EqualTo(before));
        }

        [Test]
        public void PositionVariantOffsetsSnapshotAndKeysWhilePreservingMovement()
        {
            SequenceDocument source = Document();
            source.Scene.Parameters = new SceneParameterProfile
            {
                Profile = new ParameterProfile { Id = "offset", Properties = new[] { new ParameterProperty { Id = "position", Baseline = 0, Minimum = -2, Maximum = 2 } },
                    Controls = new[] { new ParameterControl { Id = "offset", Label = "Offset", Minimum = -2, Maximum = 2, Default = 0, Value = 0,
                        Influences = new[] { new ParameterInfluence { Property = "position", Weight = 1 } } } } },
                Bindings = new[] { new SceneParameterBinding { Property = "position", Kind = "position", Node = "actor", Component = 0 } }
            };
            SequenceDocument changed = SequenceParameters.WithValues(source, new Dictionary<string, float> { { "offset", 1 } });
            Assert.That(changed.Scene.Nodes[0].Position[0], Is.EqualTo(4));
            Assert.That(changed.Clips[0].Tracks[0].Keys[0].Position[0], Is.EqualTo(3));
            Assert.That(changed.Clips[0].Tracks[0].Keys[1].Position[0], Is.EqualTo(5));
            Assert.That(source.Scene.Nodes[0].Position[0], Is.EqualTo(3));
        }

        [Test]
        public void MorphVariantRequiresResolvedBindingsAndRejectsAnyOverflowWithoutChangingSource()
        {
            SequenceDocument source = Document();
            source.Scene.Resources = new[] { new ContentResource { Id = "geometry", Path = "mesh.bin", Sha256 = new string('a', 64), MediaType = "application/vnd.native.geometry" } };
            source.Scene.Materials = new[] { new SurfaceMaterial { BaseColor = new[] { 1f, 1f, 1f, 1f } } };
            source.Scene.Meshes = new[] { new MeshInstance { Node = 0, Geometry = "geometry", Bones = new int[0], Materials = new[] { 0 }, MorphWeights = new[] { .4f } } };
            source.Scene.Parameters = new SceneParameterProfile
            {
                Profile = new ParameterProfile { Id = "face", Properties = new[] { new ParameterProperty { Id = "expression", Baseline = .4f, Minimum = 0, Maximum = 1 } },
                    Controls = new[] { new ParameterControl { Id = "face", Label = "Expression", Minimum = 0, Maximum = 1, Default = .4f, Value = .4f,
                        Influences = new[] { new ParameterInfluence { Property = "expression", Weight = 1 } } } } },
                Bindings = new[] { new SceneParameterBinding { Property = "expression", Kind = "morph", Mesh = 0, Component = 0 } }
            };
            source.Clips[0].Values = new[] { new ScalarTrack { Target = "expression-channel", Keys = new[] { new ScalarKey { Time = 0, Value = .2f }, new ScalarKey { Time = 1, Value = .8f } } } };
            string before = SequenceJson.Write(source);
            Assert.Throws<ArgumentException>(() => SequenceParameters.WithValues(source, new Dictionary<string, float> { { "face", .5f } }));
            SequenceDocument changed = SequenceParameters.WithValues(source, new Dictionary<string, float> { { "face", .5f } }, (mesh, component) => "expression-channel");
            Assert.That(changed.Clips[0].Values[0].Keys[0].Value, Is.EqualTo(.3f).Within(.000001f));
            Assert.That(changed.Scene.Meshes[0].MorphWeights[0], Is.EqualTo(.5f));
            Assert.Throws<ArgumentException>(() => SequenceParameters.WithValues(source, new Dictionary<string, float> { { "face", 1 } }, (mesh, component) => "expression-channel"));
            Assert.Throws<ArgumentException>(() => SequenceParameters.WithValues(source, new Dictionary<string, float> { { "face", float.NaN } }, (mesh, component) => "expression-channel"));
            Assert.That(SequenceJson.Write(source), Is.EqualTo(before));
        }

        private static SequenceDocument Document()
        {
            SceneDocument scene = new SceneDocument { Id = "scene", Nodes = new[] { new SceneNode { Id = "actor", Position = new[] { 3f, 0f, 0f }, Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } } },
                Meshes = new MeshInstance[0], Materials = new SurfaceMaterial[0], Resources = new ContentResource[0] };
            SceneParameterAuthoring.AddScaleControl(scene, "actor", "size", "Size", -1, .5f, 2);
            scene.Nodes[0].Scale = new[] { 1.2f, 1.2f, 1.2f };
            PoseClip clip = new PoseClip { Id = "motion", Duration = 1, Loop = true, Tracks = new[] { new PoseTrack { Node = "actor", Keys = new[]
            {
                new PoseKey { Time = 0, Position = new[] { 2f, 0f, 0f }, Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } },
                new PoseKey { Time = 1, Position = new[] { 4f, 0f, 0f }, Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1.4f, 1.4f, 1.4f } }
            } } } };
            return new SequenceDocument { Id = "sequence", Scene = scene, Clips = new[] { clip } };
        }
    }
}
