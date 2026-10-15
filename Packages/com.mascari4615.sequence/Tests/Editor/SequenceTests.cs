using System;
using System.IO;
using System.Security.Cryptography;
using Motion;
using NUnit.Framework;
using SceneContent;
using SceneSequence.Serialization;
using SceneSequence.Storage;

namespace SceneSequence.Tests
{
    public sealed class SequenceTests
    {
        [Test]
        public void RoundTripUsesStrictNestedFormatsAndRejectsConflictingBindings()
        {
            SequenceDocument sequence = Document();
            string json = SequenceJson.Write(sequence);
            Assert.That(SequenceJson.Read(json).Clips[0].Tracks[0].Node, Is.EqualTo("node"));
            Assert.Throws<ArgumentException>(() => SequenceJson.Read(json.Replace("\"Version\": 1", "\"Version\": 2")));
            Assert.Throws<ArgumentException>(() => SequenceJson.Read(json.Replace("\"Scene\":", "\"Extra\": 1, \"Scene\":")));
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => SequenceJson.Read(json.Replace("\"CoordinateFrame\":", "\"UnknownScene\": 1, \"CoordinateFrame\":")));
            sequence.Clips = new[] { sequence.Clips[0], sequence.Clips[0] };
            Assert.Throws<ArgumentException>(() => SequenceValidation.Ensure(sequence));
            sequence = Document();
            sequence.Clips[0].Tracks[0].Node = "absent";
            Assert.Throws<ArgumentException>(() => SequenceValidation.Ensure(sequence));
        }

        [Test]
        public void ExportSharesResourcesAndCorruptReadOrFailedSaveKeepsPreviousSequence()
        {
            string directory = Path.Combine(Path.GetTempPath(), "sequence-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                byte[] bytes = { 1, 2, 3 };
                SequenceDocument sequence = Document();
                string hash;
                using (SHA256 algorithm = SHA256.Create())
                {
                    hash = BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                }
                sequence.Scene.Resources = new[] { new ContentResource { Id = "resource", Path = "image.png", MediaType = "image/png", Sha256 = hash } };
                string file = Path.Combine(directory, "first.sequence.json");
                SequenceFiles.Save(file, sequence, new MemorySource { Bytes = bytes });
                SequenceFiles.Save(Path.Combine(directory, "second.sequence.json"), sequence, new MemorySource { Bytes = bytes });
                Assert.That(Directory.GetFiles(Path.Combine(directory, "objects"), "*.blob", SearchOption.AllDirectories).Length, Is.EqualTo(1));
                Assert.That(SequenceFiles.Read(file).Scene.Resources[0].Path, Does.StartWith("objects/"));
                string before = File.ReadAllText(file);
                Assert.Throws<IOException>(() => SequenceFiles.Save(file, sequence, new MemorySource { Bytes = new byte[] { 4 } }));
                Assert.That(File.ReadAllText(file), Is.EqualTo(before));
                string resource = Path.Combine(directory, SequenceFiles.Read(file).Scene.Resources[0].Path);
                File.WriteAllBytes(resource, new byte[] { 4 });
                Assert.Throws<InvalidDataException>(() => SequenceFiles.Read(file));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static SequenceDocument Document()
        {
            PoseClip clip = new PoseClip { Id = "clip", Duration = 1, Tracks = new PoseTrack[0] };
            clip = PoseClipEditing.Record(clip, "node", 0, new PoseValue(System.Numerics.Vector3.Zero, System.Numerics.Quaternion.Identity, System.Numerics.Vector3.One));
            return new SequenceDocument
            {
                Id = "sequence", Clips = new[] { clip },
                Scene = new SceneDocument
                {
                    Id = "scene", Nodes = new[] { new SceneNode { Id = "node", Position = new float[3], Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } } },
                    Meshes = new MeshInstance[0], Materials = new SurfaceMaterial[0], Resources = new ContentResource[0]
                }
            };
        }

        private sealed class MemorySource : IContentSource
        {
            public byte[] Bytes;
            public byte[] Read(ContentResource resource) { return Bytes; }
        }
    }
}
