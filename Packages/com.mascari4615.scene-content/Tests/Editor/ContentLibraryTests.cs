using System;
using System.IO;
using NUnit.Framework;
using SceneContent.Storage;

namespace SceneContent.Tests
{
    public sealed class ContentLibraryTests
    {
        [Test]
        public void SceneVariantsShareBlobsAndRemainExportable()
        {
            string directory = Path.Combine(Path.GetTempPath(), "content-library-" + Guid.NewGuid().ToString("N"));
            string export = Path.Combine(directory, "export");
            Directory.CreateDirectory(export);
            try
            {
                byte[] bytes = SceneFixtures.Bytes();
                MemorySource source = new MemorySource { Bytes = bytes };
                SceneDocument document = SceneFixtures.Document(bytes);
                ContentLibrary library = new ContentLibrary(directory);
                string first = library.Import(document, source);
                document.Nodes[0].Position[0] = 4;
                string second = library.Import(document, source);
                Assert.That(first, Is.Not.EqualTo(second));
                Assert.That(library.Import(document, source), Is.EqualTo(second));
                LibraryUsage usage = library.Usage();
                Assert.That(usage.Documents, Is.EqualTo(2));
                Assert.That(usage.Objects, Is.EqualTo(1));
                Assert.That(usage.ResourceBytes, Is.EqualTo(bytes.Length));
                SceneDocument loaded = SceneFiles.Read(second);
                Assert.That(document.Resources[0].Path, Is.EqualTo("mesh.bin"));
                SceneFiles.Save(Path.Combine(export, "scene.json"), loaded, new DirectoryContentSource(directory));
                Assert.That(new DirectoryContentSource(export).Read(loaded.Resources[0]), Is.EqualTo(bytes));
            }
            finally
            {
                DeleteOwnedTree(directory);
            }
        }

        [Test]
        public void NestedSaveRejectsTraversalAndManifestCollisionsBeforeWriting()
        {
            string directory = Path.Combine(Path.GetTempPath(), "content-library-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                byte[] bytes = SceneFixtures.Bytes();
                SceneDocument document = SceneFixtures.Document(bytes);
                MemorySource source = new MemorySource { Bytes = bytes };
                string file = Path.Combine(directory, "scene.json");
                document.Resources[0].Path = "../escaped.bin";
                Assert.Throws<ArgumentException>(() => SceneFiles.Save(file, document, source));
                document.Resources[0].Path = "scene.json";
                Assert.Throws<IOException>(() => SceneFiles.Save(file, document, source));
                Assert.That(Directory.GetFiles(directory), Is.Empty);
            }
            finally
            {
                DeleteOwnedTree(directory);
            }
        }

        private static void DeleteOwnedTree(string directory)
        {
            foreach (string file in Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                File.Delete(file);
            }
            foreach (string child in Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                DeleteOwnedTree(child);
            }
            Directory.Delete(directory);
        }
    }
}
