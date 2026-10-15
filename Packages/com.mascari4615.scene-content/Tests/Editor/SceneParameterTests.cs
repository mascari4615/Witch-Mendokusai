using System;
using System.Collections.Generic;
using Appearance;
using NUnit.Framework;
using SceneContent.Serialization;

namespace SceneContent.Tests
{
    public sealed class SceneParameterTests
    {
        private static SceneDocument Document()
        {
            SceneDocument document = SceneFixtures.Document(SceneFixtures.Bytes());
            SceneParameterAuthoring.AddScaleControl(document, "node-1", "size", "Size", -1, .7f, 1.4f);
            return document;
        }

        [Test]
        public void CalibratedControlsRoundTripAndResetWithoutCompoundingOrMutatingSource()
        {
            SceneDocument source = Document();
            SceneDocument changed = SceneParameters.WithValues(source, new Dictionary<string, float> { { "size", 1.25f } });
            Assert.That(changed.Nodes[1].Scale, Is.EqualTo(new[] { 1.25f, 1.25f, 1.25f }));
            SceneDocument loaded = SceneJson.Read(SceneJson.Write(changed));
            SceneParameters.SetValues(loaded, new Dictionary<string, float> { { "size", 1.4f } });
            Assert.That(loaded.Nodes[1].Scale, Is.EqualTo(new[] { 1.4f, 1.4f, 1.4f }));
            SceneParameters.SetValues(loaded, new Dictionary<string, float> { { "size", 1 } });
            Assert.That(loaded.Nodes[1].Scale, Is.EqualTo(new[] { 1f, 1f, 1f }));
            Assert.That(source.Parameters.Profile.Controls[0].Value, Is.EqualTo(1));
            Assert.That(source.Nodes[1].Scale, Is.EqualTo(new[] { 1f, 1f, 1f }));
            loaded.Parameters.Profile.Controls[0].Influences[0].Weight = 5;
            Assert.That(source.Parameters.Profile.Controls[0].Influences[0].Weight, Is.EqualTo(1));
        }

        [Test]
        public void InvalidValuesAndCompetingEndpointsCannotPartiallyChangeTheDocument()
        {
            SceneDocument document = Document();
            string before = SceneJson.Write(document);
            Assert.Throws<ArgumentException>(() => SceneParameters.SetValues(document, new Dictionary<string, float> { { "size", 2 } }));
            Assert.That(SceneJson.Write(document), Is.EqualTo(before));
            Assert.Throws<ArgumentException>(() => SceneParameterAuthoring.AddScaleControl(document, "node-1", "other", "Other", 0, .5f, 2));
            Assert.That(SceneJson.Write(document), Is.EqualTo(before));
            document.Parameters.Bindings[1].Component = 0;
            Assert.Throws<ArgumentException>(() => SceneValidation.Ensure(document));
        }

        [Test]
        public void CompositionNamespacesControlsAndRemapsMorphEndpoints()
        {
            SceneDocument document = Document();
            List<ParameterProperty> properties = new List<ParameterProperty>(document.Parameters.Profile.Properties)
            {
                new ParameterProperty { Id = "expression-value", Baseline = .5f, Minimum = 0, Maximum = 1 }
            };
            List<ParameterControl> controls = new List<ParameterControl>(document.Parameters.Profile.Controls)
            {
                new ParameterControl { Id = "expression", Label = "Expression", Minimum = -1, Maximum = 1, Default = 0, Value = 0,
                    Influences = new[] { new ParameterInfluence { Property = "expression-value", Weight = .5f } } }
            };
            List<SceneParameterBinding> bindings = new List<SceneParameterBinding>(document.Parameters.Bindings)
            {
                new SceneParameterBinding { Property = "expression-value", Kind = "morph", Mesh = 0, Component = 0 }
            };
            document.Parameters.Profile.Properties = properties.ToArray();
            document.Parameters.Profile.Controls = controls.ToArray();
            document.Parameters.Bindings = bindings.ToArray();
            SceneDocument composed = SceneComposition.Append(document, document, "guest", new float[3]);
            SceneParameters.SetValues(composed, new Dictionary<string, float> { { "guest:size", 1.2f }, { "guest:expression", 1 } });
            Assert.That(Array.Find(composed.Nodes, node => node.Id == "guest:node-1").Scale[0], Is.EqualTo(1.2f));
            Assert.That(composed.Nodes[1].Scale[0], Is.EqualTo(1));
            Assert.That(composed.Meshes[1].MorphWeights[0], Is.EqualTo(1));
            Assert.That(composed.Meshes[0].MorphWeights[0], Is.EqualTo(.5f));
            Assert.That(document.Parameters.Profile.Controls.Length, Is.EqualTo(2));
        }

        [Test]
        public void PruningKeepsAnOtherwiseUnusedParameterNode()
        {
            SceneDocument document = SceneFixtures.Document(SceneFixtures.Bytes());
            List<SceneNode> nodes = new List<SceneNode>(document.Nodes)
            {
                new SceneNode { Id = "control-anchor", Parent = 0, Position = new float[3], Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } }
            };
            document.Nodes = nodes.ToArray();
            SceneParameterAuthoring.AddScaleControl(document, "control-anchor", "size", "Size", -1, .5f, 2);
            SceneDocument pruned = ScenePruning.Snapshot(document, new string[0]);
            Assert.That(Array.Exists(pruned.Nodes, node => node.Id == "control-anchor"), Is.True);
            SceneParameters.SetValues(pruned, new Dictionary<string, float> { { "size", 1.5f } });
            Assert.That(Array.Find(pruned.Nodes, node => node.Id == "control-anchor").Scale[0], Is.EqualTo(1.5f));
        }
    }
}
