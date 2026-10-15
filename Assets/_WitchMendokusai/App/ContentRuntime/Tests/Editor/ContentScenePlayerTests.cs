using System;
using System.Collections.Generic;
using Motion;
using NUnit.Framework;
using SceneContent;
using SceneContent.Unity;
using UnityEngine;

namespace WitchMendokusai.ContentRuntime.Tests
{
    public sealed class ContentScenePlayerTests
    {
        [Test]
        public void LoadsSeeksAndUnloadsOwnedScene()
        {
            GameObject host = new GameObject("content-player-test");
            try
            {
                ContentScenePlayer player = host.AddComponent<ContentScenePlayer>();
                player.Initialize(Document(), new EmptySource(), Surfaces(), new[] { Clip("node") });
                player.Seek(.5f);
                Assert.That(player.Instance.FindNode("node").localPosition.x, Is.EqualTo(.5f).Within(.00001));
                player.Unload();
                Assert.That(player.IsLoaded, Is.False);
                Assert.That(host.transform.childCount, Is.EqualTo(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void OverlappingClipsAreRejectedBeforeAllocatingScene()
        {
            GameObject host = new GameObject("content-player-test");
            try
            {
                ContentScenePlayer player = host.AddComponent<ContentScenePlayer>();
                Assert.Throws<ArgumentException>(() => player.Initialize(Document(), new EmptySource(), Surfaces(), new[] { Clip("node"), Clip("node") }));
                Assert.That(host.transform.childCount, Is.EqualTo(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void MissingTargetDisposesStagedSceneAndKeepsPlayerUnloaded()
        {
            GameObject host = new GameObject("content-player-test");
            try
            {
                ContentScenePlayer player = host.AddComponent<ContentScenePlayer>();
                Assert.Throws<KeyNotFoundException>(() => player.Initialize(Document(), new EmptySource(), Surfaces(), new[] { Clip("missing") }));
                Assert.That(player.IsLoaded, Is.False);
                Assert.That(host.transform.childCount, Is.EqualTo(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static SceneDocument Document()
        {
            return new SceneDocument
            {
                Id = "test", Nodes = new[] { new SceneNode { Id = "node", Position = new float[3], Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } } },
                Meshes = new MeshInstance[0], Materials = new SurfaceMaterial[0], Resources = new ContentResource[0]
            };
        }

        private static PoseClip Clip(string node)
        {
            PoseClip clip = new PoseClip { Id = "clip", Duration = 1, Tracks = new PoseTrack[0] };
            clip = PoseClipEditing.Record(clip, node, 0, new PoseValue(System.Numerics.Vector3.Zero, System.Numerics.Quaternion.Identity, System.Numerics.Vector3.One));
            return PoseClipEditing.Record(clip, node, 1, new PoseValue(System.Numerics.Vector3.UnitX, System.Numerics.Quaternion.Identity, System.Numerics.Vector3.One));
        }

        private static ISurfaceFactory Surfaces()
        {
            return new SingleShaderSurfaceFactory(Shader.Find("Universal Render Pipeline/Unlit"));
        }

        private sealed class EmptySource : IContentSource
        {
            public byte[] Read(ContentResource resource) { throw new InvalidOperationException("No resources expected"); }
        }
    }
}
