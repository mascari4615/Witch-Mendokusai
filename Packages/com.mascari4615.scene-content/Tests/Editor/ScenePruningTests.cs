using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace SceneContent.Tests
{
    public sealed class ScenePruningTests
    {
        [Test]
        public void PrunesUnusedNodesAndRetainsPinnedAnchorsWithoutChangingSource()
        {
            SceneDocument source = SceneFixtures.Document(SceneFixtures.Bytes());
            List<SceneNode> nodes = new List<SceneNode>(source.Nodes);
            nodes.Add(Node("unused-parent", 0));
            nodes.Add(Node("unused-child", 5));
            nodes.Add(Node("authored-anchor", 0));
            source.Nodes = nodes.ToArray();
            source.Nodes[1].Parent = 7;
            SceneDocument result = ScenePruning.Snapshot(source, new string[0]);
            Assert.That(result.Nodes.Length, Is.EqualTo(6));
            Assert.That(result.Nodes[5].Id, Is.EqualTo("authored-anchor"));
            Assert.That(result.Nodes[1].Parent, Is.EqualTo(5));
            Assert.That(source.Nodes[1].Parent, Is.EqualTo(7));
            result.Nodes[1].Position[0] = 3;
            Assert.That(source.Nodes[1].Position[0], Is.EqualTo(0));
            SceneDocument pinned = ScenePruning.Snapshot(source, new[] { "unused-child" });
            Assert.That(pinned.Nodes.Length, Is.EqualTo(8));
            Assert.Throws<ArgumentException>(() => ScenePruning.Snapshot(source, new[] { "missing" }));
        }

        private static SceneNode Node(string id, int parent)
        {
            return new SceneNode { Id = id, Parent = parent, Position = new float[3], Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } };
        }
    }
}
