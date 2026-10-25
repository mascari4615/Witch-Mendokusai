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
        public void TextureWrapIsOptionalVersionedAndSurvivesCopyAndComposition()
        {
            SceneDocument document = SceneFixtures.Document(SceneFixtures.Bytes());
            string legacy = SceneJson.Write(document);
            Assert.That(legacy, Does.Not.Contain("TextureWrap"));
            Assert.That(SceneJson.Write(SceneJson.Read(legacy)), Is.EqualTo(legacy));
            document.Materials[0].TextureWrap = "clamp";
            Assert.Throws<ArgumentException>(() => SceneValidation.Ensure(document));
            document.Version = 2;
            SceneDocument copy = SceneCloning.Copy(SceneJson.Read(SceneJson.Write(document)));
            Assert.That(copy.Materials[0].TextureWrap, Is.EqualTo("clamp"));
            SceneDocument composed = SceneComposition.Append(copy, document, "another", new[] { 0f, 0f, 0f });
            Assert.That(composed.Materials[1].TextureWrap, Is.EqualTo("clamp"));
            document.Materials[0].TextureWrap = "mirror";
            Assert.Throws<ArgumentException>(() => SceneJson.Write(document));
        }

        [Test]
        public void TextureCacheSeparatesWrapAndSharesOnlyEquivalentSamplers()
        {
            SkinWeights previous = QualitySettings.skinWeights;
            Texture2D image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            SceneInstance instance = null;
            Texture clamp = null;
            Texture repeat = null;
            try
            {
                QualitySettings.skinWeights = SkinWeights.FourBones;
                image.SetPixels(new[] { Color.red, Color.blue, Color.green, Color.white });
                image.Apply();
                byte[] png = image.EncodeToPNG();
                byte[] geometry = SceneFixtures.Bytes();
                SceneDocument document = SceneFixtures.Document(geometry);
                document.Version = 2;
                string hash;
                using (System.Security.Cryptography.SHA256 algorithm = System.Security.Cryptography.SHA256.Create())
                    hash = BitConverter.ToString(algorithm.ComputeHash(png)).Replace("-", "").ToLowerInvariant();
                document.Resources = new[] { document.Resources[0], new ContentResource { Id = "image", Path = "image.png", MediaType = "image/png", Sha256 = hash } };
                document.Materials = new SurfaceMaterial[4];
                document.Meshes = new MeshInstance[4];
                string[] wraps = { "clamp", "repeat", null, "clamp" };
                for (int index = 0; index < wraps.Length; index++)
                {
                    document.Materials[index] = new SurfaceMaterial { BaseColor = new[] { 1f, 1f, 1f, 1f }, BaseColorTexture = "image", TextureWrap = wraps[index] };
                    document.Meshes[index] = new MeshInstance { Node = 0, Geometry = "geometry", RootBone = 1, Bones = new[] { 1, 2, 3, 4 }, Materials = new[] { index }, MorphWeights = new[] { .5f } };
                }
                instance = SceneContentLoader.Load(document, new TextureSource { Geometry = geometry, Image = png }, Shader.Find("Universal Render Pipeline/Unlit"));
                clamp = instance.FindMesh(0).sharedMaterial.GetTexture("_BaseMap");
                repeat = instance.FindMesh(1).sharedMaterial.GetTexture("_BaseMap");
                Assert.That(clamp.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(repeat.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
                Assert.That(clamp, Is.Not.SameAs(repeat));
                Assert.That(instance.FindMesh(2).sharedMaterial.GetTexture("_BaseMap"), Is.SameAs(repeat));
                Assert.That(instance.FindMesh(3).sharedMaterial.GetTexture("_BaseMap"), Is.SameAs(clamp));
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
                instance = null;
                Assert.That(clamp == null && repeat == null, Is.True, "Both owned sampler textures must be released");
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance.gameObject);
                UnityEngine.Object.DestroyImmediate(image);
                QualitySettings.skinWeights = previous;
            }
        }

        private sealed class TextureSource : IContentSource
        {
            public byte[] Geometry, Image;
            public byte[] Read(ContentResource resource) { return resource.Id == "image" ? Image : Geometry; }
        }

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
