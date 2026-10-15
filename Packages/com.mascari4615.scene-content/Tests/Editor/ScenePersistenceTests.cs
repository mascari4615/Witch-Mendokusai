using System;
using System.IO;
using NUnit.Framework;
using SceneContent.Storage;
using SceneContent.Unity;
using UnityEngine;

namespace SceneContent.Tests
{
    public sealed class ScenePersistenceTests
    {
        [Test]
        public void PoseAndMorphSnapshotRoundTripWithoutChangingSource()
        {
            SkinWeights previous = QualitySettings.skinWeights;
            SceneInstance instance = null;
            string directory = Path.Combine(Path.GetTempPath(), "scene-content-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                QualitySettings.skinWeights = SkinWeights.FourBones;
                byte[] bytes = SceneFixtures.Bytes();
                SceneDocument original = SceneFixtures.Document(bytes);
                original.Nodes[1].Label = "Joint";
                MemorySource source = new MemorySource { Bytes = bytes };
                instance = SceneContentLoader.Load(original, source, Shader.Find("Universal Render Pipeline/Unlit"));
                instance.FindNode("node-1").localPosition = new Vector3(1, 2, 3);
                instance.FindNode("node-1").localRotation = Quaternion.Euler(12, 30, 40);
                instance.FindMesh(0).name = "Renamed visible mesh";
                instance.GetComponentInChildren<SkinnedMeshRenderer>().SetBlendShapeWeight(0, 37);
                SceneDocument snapshot = SceneSnapshot.Capture(original, instance);
                string file = Path.Combine(directory, "scene.json");
                SceneFiles.Save(file, snapshot, source);
                SceneFiles.Save(file, snapshot, source);
                SceneDocument loaded = SceneFiles.Read(file);
                Assert.That(loaded.Nodes[1].Position, Is.EqualTo(new[] { 1f, 2f, -3f }));
                Assert.That(loaded.Nodes[1].Label, Is.EqualTo("Joint"));
                Assert.That(loaded.Meshes[0].MorphWeights[0], Is.EqualTo(.37f).Within(.00001));
                Assert.That(new DirectoryContentSource(directory).Read(loaded.Resources[0]), Is.EqualTo(bytes));
                Assert.That(original.Nodes[1].Position, Is.EqualTo(new[] { 0f, 0f, 0f }));
                Assert.That(original.Meshes[0].MorphWeights[0], Is.EqualTo(.5f));
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance.gameObject);
                }
                QualitySettings.skinWeights = previous;
                foreach (string file in Directory.GetFiles(directory))
                {
                    File.Delete(file);
                }
                Directory.Delete(directory);
            }
        }

        [Test]
        public void BadSourceCannotPublishResourceOrManifest()
        {
            string directory = Path.Combine(Path.GetTempPath(), "scene-content-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                SceneDocument document = SceneFixtures.Document(SceneFixtures.Bytes());
                MemorySource source = new MemorySource { Bytes = new byte[] { 1, 2, 3 } };
                Assert.Throws<IOException>(() => SceneFiles.Save(Path.Combine(directory, "scene.json"), document, source));
                Assert.That(Directory.GetFiles(directory), Is.Empty);
            }
            finally
            {
                Directory.Delete(directory);
            }
        }

        [Test]
        public void CorruptExistingResourceCannotReplaceSavedManifest()
        {
            string directory = Path.Combine(Path.GetTempPath(), "scene-content-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                byte[] bytes = SceneFixtures.Bytes();
                SceneDocument document = SceneFixtures.Document(bytes);
                MemorySource source = new MemorySource { Bytes = bytes };
                string file = Path.Combine(directory, "scene.json");
                SceneFiles.Save(file, document, source);
                string original = File.ReadAllText(file);
                File.WriteAllBytes(Path.Combine(directory, document.Resources[0].Path), new byte[] { 1, 2, 3 });
                document.Id = "changed";
                Assert.Catch<Exception>(() => SceneFiles.Save(file, document, source));
                Assert.That(File.ReadAllText(file), Is.EqualTo(original));
            }
            finally
            {
                foreach (string file in Directory.GetFiles(directory))
                {
                    File.Delete(file);
                }
                Directory.Delete(directory);
            }
        }
    }
}
