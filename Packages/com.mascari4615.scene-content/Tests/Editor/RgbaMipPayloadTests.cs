using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using SceneContent.Serialization;
using SceneContent.Storage;
using SceneContent.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace SceneContent.Tests
{
    public sealed class RgbaMipPayloadTests
    {
        private static byte[] Authored(bool linear = true)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(new byte[] { 82, 71, 77, 80 });
                foreach (int value in new[] { 1, 4, 2, 1, linear ? 0 : 1, 3, 44,
                    4, 2, 0, 32, 2, 1, 32, 8, 1, 1, 40, 4 }) writer.Write(value);
                for (int index = 0; index < 44; index++) writer.Write((byte)(index + 1));
                return stream.ToArray();
            }
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        [Test]
        public void AuthoredMipsReadThroughStorageAndPreserveVersionAcrossJsonAndComposition()
        {
            string temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = "native-mips-" + Guid.NewGuid().ToString("N");
            string directory = Path.Combine(temporary, name);
            Directory.CreateDirectory(directory);
            try
            {
                byte[] bytes = Authored();
                File.WriteAllBytes(Path.Combine(directory, "authored.rgba"), bytes);
                ContentResource resource = new ContentResource { Id = "image", Path = "authored.rgba", MediaType = RgbaMipPayload.MediaType, Sha256 = Hash(bytes) };
                RgbaMipPayload payload = RgbaMipPayload.Read(new DirectoryContentSource(directory).Read(resource));
                Assert.That(payload.Width, Is.EqualTo(4));
                Assert.That(payload.Height, Is.EqualTo(2));
                Assert.That(payload.MipCount, Is.EqualTo(3));
                Assert.That(payload.Linear, Is.True);
                Assert.That(payload.Pixels, Is.EqualTo(bytes.Skip(80).ToArray()));
                Assert.That(File.ReadAllBytes(Path.Combine(directory, "authored.rgba")), Is.EqualTo(bytes));
                ContentResource wrong = new ContentResource { Path = resource.Path, Sha256 = new string('0', 64) };
                Assert.Throws<InvalidDataException>(() => new DirectoryContentSource(directory).Read(wrong));
                SceneDocument scene = SceneFixtures.Document(SceneFixtures.Bytes());
                scene.Resources = new[] { scene.Resources[0], resource };
                scene.Materials[0].BaseColorTexture = resource.Id;
                foreach (int oldVersion in new[] { 1, 2 })
                {
                    scene.Version = oldVersion;
                    Assert.Throws<ArgumentException>(() => SceneJson.Write(scene));
                }
                scene.Version = 3;
                SceneDocument copy = SceneCloning.Copy(SceneJson.Read(SceneJson.Write(scene)));
                SceneDocument composed = SceneComposition.Append(copy, scene, "other", new float[3]);
                Assert.That(copy.Version, Is.EqualTo(3));
                Assert.That(composed.Version, Is.EqualTo(3));
                Assert.That(composed.Materials[1].BaseColorTexture, Is.EqualTo("other:image"));
                Assert.That(composed.Resources.Last().MediaType, Is.EqualTo(RgbaMipPayload.MediaType));
            }
            finally { DeleteOwnedFixture(directory, temporary, name); }
        }

        private static void DeleteOwnedFixture(string directory, string temporary, string name)
        {
            string full = Path.GetFullPath(directory);
            if (!string.Equals(full, Path.Combine(temporary, name), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetDirectoryName(full), temporary, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(full) != name)
                throw new IOException("Fixture cleanup path escaped its owned TEMP child");
            for (DirectoryInfo parent = new DirectoryInfo(full); parent != null; parent = parent.Parent)
                if ((parent.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Fixture cleanup refuses reparse ancestors");
            foreach (string entry in Directory.GetFileSystemEntries(full))
                if (Path.GetFileName(entry) != "authored.rgba" ||
                    (File.GetAttributes(entry) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                    throw new IOException("Fixture cleanup refuses unexpected or linked entries");
            Directory.Delete(full, true);
        }

        [Test]
        public void MipReaderRejectsUnknownAndMalformedContractsBeforePixelsAreAllocated()
        {
            byte[] valid = Authored();
            foreach (int offset in new[] { 0, 4, 8, 12, 16, 20, 24, 28, 32, 40, 44, 48, 56, 60, 72, 76 })
            {
                byte[] invalid = (byte[])valid.Clone();
                invalid[offset] = 255;
                Assert.Throws<InvalidDataException>(() => RgbaMipPayload.Read(invalid), "offset " + offset);
            }
            Assert.Throws<InvalidDataException>(() => RgbaMipPayload.Read(valid.Take(valid.Length - 1).ToArray()));
            Assert.Throws<InvalidDataException>(() => RgbaMipPayload.Read(valid.Concat(new byte[1]).ToArray()));
            byte[] overflow = (byte[])valid.Clone();
            Array.Copy(new byte[] { 255, 255, 255, 255 }, 0, overflow, 8, 4);
            Assert.Throws<InvalidDataException>(() => RgbaMipPayload.Read(overflow));
        }

        [Test]
        public void MipTexturesPreserveLevelCountAndCacheOnlyEquivalentPoliciesAndReleaseOwnership()
        {
            SkinWeights previous = QualitySettings.skinWeights;
            SceneInstance instance = null;
            Texture[] textures = new Texture[4];
            try
            {
                QualitySettings.skinWeights = SkinWeights.FourBones;
                byte[] linear = Authored(), srgb = Authored(false), geometry = SceneFixtures.Bytes();
                SceneDocument scene = SceneFixtures.Document(geometry);
                scene.Version = 3;
                scene.Resources = new[] { scene.Resources[0],
                    new ContentResource { Id = "linear", Path = "linear.rgba", MediaType = RgbaMipPayload.MediaType, Sha256 = Hash(linear) },
                    new ContentResource { Id = "srgb", Path = "srgb.rgba", MediaType = RgbaMipPayload.MediaType, Sha256 = Hash(srgb) } };
                scene.Materials = new SurfaceMaterial[4];
                scene.Meshes = new MeshInstance[4];
                for (int index = 0; index < 4; index++)
                {
                    scene.Materials[index] = new SurfaceMaterial { BaseColor = new[] { 1f, 1f, 1f, 1f },
                        BaseColorTexture = index == 2 ? "srgb" : "linear", TextureWrap = index == 1 ? "repeat" : "clamp" };
                    scene.Meshes[index] = new MeshInstance { Node = 0, Geometry = "geometry", RootBone = 1,
                        Bones = new[] { 1, 2, 3, 4 }, Materials = new[] { index }, MorphWeights = new[] { .5f } };
                }
                instance = SceneContentLoader.Load(scene, new AuthoredSource { Geometry = geometry, Linear = linear, Srgb = srgb }, Shader.Find("Universal Render Pipeline/Unlit"));
                for (int index = 0; index < 4; index++)
                {
                    textures[index] = instance.FindMesh(index).sharedMaterial.GetTexture("_BaseMap");
                    Assert.That(((Texture2D)textures[index]).mipmapCount, Is.EqualTo(3));
                    Assert.That(textures[index].width, Is.EqualTo(4));
                    Assert.That(textures[index].height, Is.EqualTo(2));
                }
                Assert.That(textures[0].wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(textures[1].wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
                Assert.That(textures[0], Is.Not.SameAs(textures[1]));
                Assert.That(textures[0], Is.Not.SameAs(textures[2]));
                Assert.That(textures[0], Is.SameAs(textures[3]));
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
                instance = null;
                Assert.That(textures.All(texture => texture == null), Is.True);
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance.gameObject);
                QualitySettings.skinWeights = previous;
            }
        }

        [UnityTest]
        public IEnumerator AuthoredGpuMipBytesPreserveBothExplicitColorPoliciesWithoutRegeneration()
        {
            if (!SystemInfo.supportsAsyncGPUReadback)
                Assert.Ignore("GPU mip byte verification requires AsyncGPUReadback support");
            SkinWeights previous = QualitySettings.skinWeights;
            SceneInstance instance = null;
            try
            {
                QualitySettings.skinWeights = SkinWeights.FourBones;
                byte[] geometry = SceneFixtures.Bytes(), linear = Authored(), srgb = Authored(false);
                SceneDocument scene = SceneFixtures.Document(geometry);
                scene.Version = 3;
                scene.Resources = new[] { scene.Resources[0],
                    new ContentResource { Id = "linear", Path = "linear.rgba", MediaType = RgbaMipPayload.MediaType, Sha256 = Hash(linear) },
                    new ContentResource { Id = "srgb", Path = "srgb.rgba", MediaType = RgbaMipPayload.MediaType, Sha256 = Hash(srgb) } };
                scene.Materials = new SurfaceMaterial[2];
                scene.Meshes = new MeshInstance[2];
                for (int index = 0; index < 2; index++)
                {
                    scene.Materials[index] = new SurfaceMaterial { BaseColor = new[] { 1f, 1f, 1f, 1f }, BaseColorTexture = index == 0 ? "linear" : "srgb" };
                    scene.Meshes[index] = new MeshInstance { Node = 0, Geometry = "geometry", RootBone = 1,
                        Bones = new[] { 1, 2, 3, 4 }, Materials = new[] { index }, MorphWeights = new[] { .5f } };
                }
                instance = SceneContentLoader.Load(scene, new AuthoredSource { Geometry = geometry, Linear = linear, Srgb = srgb }, Shader.Find("Universal Render Pipeline/Unlit"));
                for (int policy = 0; policy < 2; policy++)
                {
                    Texture texture = instance.FindMesh(policy).sharedMaterial.GetTexture("_BaseMap");
                    int offset = 80;
                    for (int level = 0; level < 3; level++)
                    {
                        var request = AsyncGPUReadback.Request(texture, level);
                        System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
                        while (!request.done && timer.Elapsed.TotalSeconds < 5)
                            yield return null;
                        Assert.That(request.done, Is.True, "GPU mip readback timed out: policy " + policy + " level " + level);
                        Assert.That(request.hasError, Is.False, "GPU mip readback failed: policy " + policy + " level " + level);
                        var data = request.GetData<byte>();
                        int size = Math.Max(1, 4 >> level) * Math.Max(1, 2 >> level) * 4;
                        Assert.That(data.Length, Is.EqualTo(size));
                        byte[] actual = new byte[data.Length];
                        for (int index = 0; index < actual.Length; index++) actual[index] = data[index];
                        byte[] expected = (policy == 0 ? linear : srgb).Skip(offset).Take(size).ToArray();
                        Assert.That(actual, Is.EqualTo(expected), "Authored GPU bytes differ: policy " + policy + " level " + level);
                        offset += size;
                    }
                    Assert.That(offset - 80, Is.EqualTo(44));
                }
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance.gameObject);
                QualitySettings.skinWeights = previous;
            }
        }

        private sealed class AuthoredSource : IContentSource
        {
            public byte[] Geometry, Linear, Srgb;
            public byte[] Read(ContentResource resource) { return resource.Id == "geometry" ? Geometry : resource.Id == "linear" ? Linear : Srgb; }
        }
    }
}
