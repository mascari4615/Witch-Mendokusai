using System;
using System.IO;
using System.Security.Cryptography;
using Geometry;
using Geometry.Serialization;
using NUnit.Framework;
using SceneContent;
using SceneContent.Serialization;
using SceneContent.Storage;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring.Tests
{
    public sealed partial class AuthoringWindowTests
    {
        private sealed class WorkspaceFixture : IDisposable
        {
            public readonly string Root = Path.Combine(Path.GetTempPath(), "authoring-wardrobe-" + Guid.NewGuid().ToString("N"));
            public readonly string ActorFile;
            public readonly string OtherFile;
            public readonly string ActorRig;
            public readonly string OutfitFile;
            public readonly string ActorOriginal;
            public readonly string RigOriginal = "\n{\"Format\":\"rig-definition\",\"Version\":1,\"Id\":\"test-rig\",\"Joints\":[{\"Role\":\"root\",\"Node\":\"root\"}],\"Limbs\":[]}\n";
            private readonly string libraryKey = "WM.ContentLibrary.Directory";
            private readonly string lastSceneKey = Application.dataPath + "/ContentWorkspace/LastScene";
            private readonly bool hadLibrary;
            private readonly bool hadLastScene;
            private readonly string oldLibrary;
            private readonly string oldLastScene;
            private readonly string recoveryFile;
            private readonly byte[] recovery;
            private ContentAuthoringWindow window;

            public WorkspaceFixture()
            {
                hadLibrary = EditorPrefs.HasKey(libraryKey); oldLibrary = EditorPrefs.GetString(libraryKey);
                hadLastScene = EditorPrefs.HasKey(lastSceneKey); oldLastScene = EditorPrefs.GetString(lastSceneKey);
                recoveryFile = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/ContentAuthoring/recovery.json"));
                if (File.Exists(recoveryFile)) recovery = File.ReadAllBytes(recoveryFile);
                ActorFile = CreateScene("actor"); OtherFile = CreateScene("other");
                ActorRig = RigFiles.Find(ActorFile);
                ActorOriginal = File.ReadAllText(ActorFile);
                EditorPrefs.SetString(libraryKey, Path.Combine(Root, "library"));
                SceneDocument part = Document("part", "part-root", new ContentResource[0]);
                OutfitFile = Path.Combine(Root, "outfit.json");
                File.WriteAllText(OutfitFile, "{\"Format\":\"wardrobe\",\"Version\":1,\"Id\":\"test-outfit\",\"Slots\":[{\"Id\":\"accessory\"}],\"Parts\":[{\"Id\":\"part\",\"Slot\":\"accessory\",\"Anchor\":\"root\",\"Scene\":" + SceneJson.Write(part) + "}],\"Outfits\":[{\"Id\":\"outfit\",\"Picks\":[{\"Slot\":\"accessory\",\"Part\":\"part\"}]}]}");
            }

            public ContentAuthoringWindow OpenWindow()
            {
                window = ScriptableObject.CreateInstance<ContentAuthoringWindow>();
                window.LoadFile(ActorFile);
                return window;
            }

            public void CheckResource(ContentWorkspace workspace, string expected)
            {
                byte[] bytes = new DirectoryContentSource(Path.GetDirectoryName(workspace.FilePath)).Read(workspace.Document.Resources[0]);
                float coordinate = expected == "actor" ? 0 : 1;
                Assert.That(GeometryBinary.Read(bytes).Positions[0], Is.EqualTo(coordinate));
                Assert.That(workspace.Instance.FindMesh(0).GetComponent<MeshFilter>().sharedMesh.vertices[0].x, Is.EqualTo(coordinate));
            }

            private string CreateScene(string name)
            {
                string directory = Path.Combine(Root, name);
                Directory.CreateDirectory(directory);
                float coordinate = name == "actor" ? 0 : 1;
                byte[] bytes = GeometryBinary.Write(new MeshGeometry
                {
                    Positions = new[] { coordinate, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f }, Normals = new float[0], Uv = new float[0],
                    BoneIndices = new int[0], BoneWeights = new float[0], BindPoses = new float[0], Submeshes = new[] { new[] { 0, 1, 2 } }
                });
                File.WriteAllBytes(Path.Combine(directory, "shared.bin"), bytes);
                string hash;
                using (SHA256 algorithm = SHA256.Create()) hash = BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                string file = Path.Combine(directory, "actor.scene.json");
                File.WriteAllText(file, SceneJson.Write(Document(name, "root", new[] { new ContentResource { Id = "resource", Path = "shared.bin", MediaType = "application/vnd.native.geometry", Sha256 = hash } })));
                File.WriteAllText(Path.Combine(directory, "actor.scene.rig.json"), RigOriginal);
                return file;
            }

            private static SceneDocument Document(string id, string node, ContentResource[] resources)
            {
                return new SceneDocument
                {
                    Id = id, Nodes = new[] { new SceneNode { Id = node, Label = "initial", Position = new float[3], Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } } },
                    Meshes = resources.Length == 0 ? new MeshInstance[0] : new[] { new MeshInstance { Node = 0, Geometry = "resource", Bones = new int[0], Materials = new[] { 0 } } },
                    Materials = resources.Length == 0 ? new SurfaceMaterial[0] : new[] { new SurfaceMaterial { BaseColor = new[] { 1f, 1f, 1f, 1f } } }, Resources = resources
                };
            }

            public void Dispose()
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
                if (hadLibrary) EditorPrefs.SetString(libraryKey, oldLibrary); else EditorPrefs.DeleteKey(libraryKey);
                if (hadLastScene) EditorPrefs.SetString(lastSceneKey, oldLastScene); else EditorPrefs.DeleteKey(lastSceneKey);
                if (recovery != null) { Directory.CreateDirectory(Path.GetDirectoryName(recoveryFile)); File.WriteAllBytes(recoveryFile, recovery); }
                else if (File.Exists(recoveryFile)) File.Delete(recoveryFile);
                string target = Path.GetFullPath(Root);
                string boundary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (target.StartsWith(boundary, StringComparison.OrdinalIgnoreCase) == false || Path.GetFileName(target).StartsWith("authoring-wardrobe-", StringComparison.Ordinal) == false) throw new InvalidOperationException("Unexpected fixture cleanup path");
                if (Directory.Exists(target)) Directory.Delete(target, true);
            }
        }
    }
}
