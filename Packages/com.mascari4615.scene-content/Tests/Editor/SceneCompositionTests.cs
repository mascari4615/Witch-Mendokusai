using System;
using System.Collections.Generic;
using NUnit.Framework;
using SceneContent.Unity;
using UnityEngine;

namespace SceneContent.Tests
{
    public sealed class SceneCompositionTests
    {
        [Test]
        public void ImportedRootNameCannotCollideWithActorContainer()
        {
            SceneDocument source = new SceneDocument
            {
                Id = "root-only", Nodes = new[] { new SceneNode { Id = "root", Position = new float[3], Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } } },
                Meshes = new MeshInstance[0], Materials = new SurfaceMaterial[0], Resources = new ContentResource[0]
            };
            SceneDocument combined = SceneComposition.Append(source, source, "second", new float[3]);
            Assert.That(combined.Nodes[1].Id, Is.EqualTo("second"));
            Assert.That(combined.Nodes[2].Id, Is.EqualTo("second:root"));
        }

        [Test]
        public void NamespacesAndRemapsEveryBindingWithoutMutatingInputs()
        {
            SceneDocument first = SceneFixtures.Document(SceneFixtures.Bytes());
            SceneDocument second = SceneFixtures.Document(SceneFixtures.Bytes());
            second.Cameras = new[] { new SceneCamera { Node = 2 } };
            second.Lights = new[] { new SceneLight { Node = 3 } };
            SceneDocument combined = SceneComposition.Append(first, second, "other", new[] { 2f, 0f, 0f });
            Assert.That(combined.Nodes.Length, Is.EqualTo(11));
            Assert.That(combined.Nodes[6].Id, Is.EqualTo("other:node-0"));
            Assert.That(combined.Nodes[6].Parent, Is.EqualTo(5));
            Assert.That(combined.Meshes[1].Node, Is.EqualTo(6));
            Assert.That(combined.Meshes[1].RootBone, Is.EqualTo(7));
            Assert.That(combined.Meshes[1].Materials[0], Is.EqualTo(1));
            Assert.That(combined.Cameras[0].Node, Is.EqualTo(8));
            Assert.That(combined.Lights[0].Node, Is.EqualTo(9));
            Assert.That(second.Nodes[0].Id, Is.EqualTo("node-0"));
            Assert.That(second.Meshes[0].Bones[0], Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => SceneComposition.Append(combined, second, "other", new float[3]));
        }

        [Test]
        public void DuplicateResourceAliasesShareOneRuntimeMesh()
        {
            SkinWeights previous = QualitySettings.skinWeights;
            SceneInstance instance = null;
            try
            {
                QualitySettings.skinWeights = SkinWeights.FourBones;
                byte[] bytes = SceneFixtures.Bytes();
                SceneDocument combined = SceneComposition.Append(SceneFixtures.Document(bytes), SceneFixtures.Document(bytes), "other", new float[3]);
                instance = SceneContentLoader.Load(combined, new MemorySource { Bytes = bytes }, Shader.Find("Universal Render Pipeline/Unlit"));
                Mesh first = ((SkinnedMeshRenderer)instance.FindMesh(0)).sharedMesh;
                Mesh second = ((SkinnedMeshRenderer)instance.FindMesh(1)).sharedMesh;
                Assert.That(first, Is.SameAs(second));
                Assert.That(instance.FindNode("other:node-0"), Is.Not.SameAs(instance.FindNode("node-0")));
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance.gameObject);
                QualitySettings.skinWeights = previous;
            }
        }

        [Test]
        public void AttachedPiecesHangFromTheHostNodeAndHiddenMeshesAreNotDrawn()
        {
            SkinWeights previous = QualitySettings.skinWeights;
            SceneInstance instance = null;
            try
            {
                QualitySettings.skinWeights = SkinWeights.FourBones;
                byte[] bytes = SceneFixtures.Bytes();
                SceneDocument host = SceneFixtures.Document(bytes);
                SceneDocument piece = SceneFixtures.Document(bytes);
                SceneDocument attached = SceneComposition.Attach(host, piece, "piece", host.Nodes[0].Id, new Dictionary<string, string>());
                Assert.That(attached.Nodes.Length, Is.EqualTo(host.Nodes.Length + piece.Nodes.Length));
                Assert.That(attached.Nodes[host.Nodes.Length].Parent, Is.EqualTo(0));
                Assert.That(host.Nodes.Length, Is.EqualTo(SceneFixtures.Document(bytes).Nodes.Length), "host must not change");
                attached.Meshes[0].Hidden = true;
                SceneDocument copy = SceneCloning.Copy(attached);
                Assert.That(copy.Meshes[0].Hidden, Is.True);
                Assert.That(copy.Meshes[1].Hidden, Is.False);
                instance = SceneContentLoader.Load(copy, new MemorySource { Bytes = bytes }, Shader.Find("Universal Render Pipeline/Unlit"));
                Assert.That(instance.FindMesh(0).enabled, Is.False);
                Assert.That(instance.FindMesh(1).enabled, Is.True);
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance.gameObject);
                QualitySettings.skinWeights = previous;
            }
        }
    }
}
