using System;
using System.Collections.Generic;
using System.IO;
using Geometry;
using Geometry.Serialization;
using NUnit.Framework;
using SceneContent.Serialization;
using SceneContent.Unity;
using UnityEngine;

namespace SceneContent.Tests
{
    public sealed class SceneContentTests
    {
        [Test]
        public void SparseMorphSurvivesBinaryAndRejectsDuplicateIndices()
        {
            MeshGeometry geometry = SceneFixtures.Geometry();
            MeshGeometry decoded = GeometryBinary.Read(GeometryBinary.Write(geometry));
            Assert.That(decoded.Morphs[0].Frames[0].Positions[0], Is.EqualTo(.2f));
            decoded.Morphs[0].Frames[0].Indices = new[] { 0, 0 };
            decoded.Morphs[0].Frames[0].Positions = new float[6];
            Assert.Throws<ArgumentException>(() => GeometryBinary.Write(decoded));
        }

        [Test]
        public void OversizedAndTruncatedBuffersAreRejected()
        {
            byte[] bytes = SceneFixtures.Bytes();
            Array.Copy(BitConverter.GetBytes(int.MaxValue), 0, bytes, 4, 4);
            Assert.Throws<IOException>(() => GeometryBinary.Read(bytes));
            Assert.Catch<IOException>(() => GeometryBinary.Read(new byte[1]));
        }

        [Test]
        public void JsonPreservesBindingAndCyclesAreRejected()
        {
            SceneDocument document = SceneFixtures.Document(SceneFixtures.Bytes());
            SceneDocument decoded = SceneJson.Read(SceneJson.Write(document));
            Assert.That(decoded.Meshes[0].MorphWeights[0], Is.EqualTo(.5f));
            Assert.That(decoded.Meshes[0].RootBone, Is.EqualTo(1));
            decoded.Nodes[0].Parent = 1;
            Assert.Throws<ArgumentException>(() => SceneValidation.Ensure(decoded));
        }

        [Test]
        public void LoaderRejectsHostThatDropsInfluences()
        {
            SkinWeights previous = QualitySettings.skinWeights;
            try
            {
                QualitySettings.skinWeights = SkinWeights.TwoBones;
                byte[] bytes = SceneFixtures.Bytes();
                Assert.Throws<InvalidOperationException>(() => SceneContentLoader.Load(SceneFixtures.Document(bytes), new MemorySource { Bytes = bytes }, Shader.Find("Universal Render Pipeline/Unlit")));
            }
            finally { QualitySettings.skinWeights = previous; }
        }

        [Test]
        public void RuntimeConsumerAppliesMorphAndOwnsResources()
        {
            SkinWeights previous = QualitySettings.skinWeights;
            SceneInstance instance = null;
            Mesh evaluated = new Mesh();
            try
            {
                QualitySettings.skinWeights = SkinWeights.FourBones;
                byte[] bytes = SceneFixtures.Bytes();
                instance = SceneContentLoader.Load(SceneFixtures.Document(bytes), new MemorySource { Bytes = bytes }, Shader.Find("Universal Render Pipeline/Unlit"));
                Assert.That(instance.FindNode("node-1").parent, Is.EqualTo(instance.FindNode("node-0")));
                SkinnedMeshRenderer renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                renderer.BakeMesh(evaluated);
                Assert.That(evaluated.vertices[0].x, Is.EqualTo(.1f).Within(.00001f));
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance.gameObject);
                UnityEngine.Object.DestroyImmediate(evaluated);
                QualitySettings.skinWeights = previous;
            }
        }

        [Test]
        public void FailedLoadCleansAssetsBeforeRootWasActivated()
        {
            HashSet<Material> before = NativeMaterials();
            try
            {
                byte[] bytes = new byte[4];
                Assert.Throws<IOException>(() => SceneContentLoader.Load(SceneFixtures.Document(bytes), new MemorySource { Bytes = bytes }, Shader.Find("Universal Render Pipeline/Unlit")));
                Assert.That(NativeMaterials().Count, Is.EqualTo(before.Count), "Failed import leaked a material");
            }
            finally
            {
                foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
                    if (material.name == "Native surface" && before.Contains(material) == false) UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static HashSet<Material> NativeMaterials()
        {
            HashSet<Material> ids = new HashSet<Material>();
            foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
                if (material.name == "Native surface") ids.Add(material);
            return ids;
        }
    }
}
