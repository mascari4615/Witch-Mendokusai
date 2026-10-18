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
        public void DynamicsWithoutPoseClipsCanPlayAndSnapshotsKeepTheAuthoredRotation()
        {
            SceneDocument document = Document();
            document.Nodes = new[] { document.Nodes[0], new SceneNode { Id = "tip", Parent = 0, Position = new[] { .5f, -.5f, 0f }, Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } } };
            document.Springs = new[] { new SceneSpring { Id = "spring", Joint = "node", Tip = "tip" } };
            GameObject host = new GameObject("spring-player-test");
            try
            {
                ContentScenePlayer player = host.AddComponent<ContentScenePlayer>();
                player.Initialize(document, new EmptySource(), Surfaces(), new PoseClip[0]);
                player.Play();
                Assert.That(player.IsPlaying, Is.True);
                player.Seek(.5f);
                Assert.That(player.Time, Is.EqualTo(.5f));
                Quaternion displayed = player.Instance.FindNode("node").localRotation;
                Assert.That(Quaternion.Angle(displayed, Quaternion.identity), Is.GreaterThan(.1f));
                SceneDocument snapshot = SceneSnapshot.Capture(document, player.Instance);
                Assert.That(snapshot.Nodes[0].Rotation, Is.EqualTo(document.Nodes[0].Rotation));
                Assert.Throws<ArgumentException>(() => player.Seek(1000));
                Assert.That(player.Time, Is.EqualTo(.5f));
                Assert.That(player.Instance.FindNode("node").localRotation, Is.EqualTo(displayed));
                player.Pause();
                Assert.That(player.IsPlaying, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void LoopingClipsStillSupportNegativeAndMultipleCycleSeeks()
        {
            GameObject host = new GameObject("loop-player-test");
            try
            {
                PoseClip clip = Clip("node"); clip.Loop = true;
                ContentScenePlayer player = host.AddComponent<ContentScenePlayer>();
                player.Initialize(Document(), new EmptySource(), Surfaces(), new[] { clip });
                player.Seek(-.25f);
                Assert.That(player.Time, Is.EqualTo(.75f));
                Assert.That(player.Instance.FindNode("node").localPosition.x, Is.EqualTo(.75f).Within(.00001f));
                player.Seek(2.5f);
                Assert.That(player.Time, Is.EqualTo(.5f));
                Assert.That(player.Instance.FindNode("node").localPosition.x, Is.EqualTo(.5f).Within(.00001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void CalibratedParameterValuesUseTheSameSceneCoreBeforeRuntimeLoading()
        {
            SceneDocument source = Document();
            SceneParameterAuthoring.AddScaleControl(source, "node", "size", "Size", -1, .5f, 2);
            SceneDocument changed = SceneParameters.WithValues(source, new Dictionary<string, float> { { "size", 1.3f } });
            GameObject host = new GameObject("parameter-player-test");
            try
            {
                ContentScenePlayer player = host.AddComponent<ContentScenePlayer>();
                player.Initialize(changed, new EmptySource(), Surfaces(), new PoseClip[0]);
                Assert.That(player.Instance.FindNode("node").localScale.x, Is.EqualTo(1.3f));
                Assert.That(source.Nodes[0].Scale[0], Is.EqualTo(1));
                player.Unload();
                Assert.That(host.transform.childCount, Is.EqualTo(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

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
        public void MissingPoseTargetIsRejectedBeforeSceneAllocation()
        {
            GameObject host = new GameObject("content-player-test");
            try
            {
                ContentScenePlayer player = host.AddComponent<ContentScenePlayer>();
                Assert.Throws<ArgumentException>(() => player.Initialize(Document(), new EmptySource(), Surfaces(), new[] { Clip("missing") }));
                Assert.That(player.IsLoaded, Is.False);
                Assert.That(host.transform.childCount, Is.EqualTo(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void MissingPropertyDisposesStagedSceneAndKeepsPlayerUnloaded()
        {
            GameObject host = new GameObject("content-player-test");
            try
            {
                ContentScenePlayer player = host.AddComponent<ContentScenePlayer>();
                PoseClip clip = PoseClipEditing.RecordValue(new PoseClip { Id = "values", Duration = 1, Tracks = new PoseTrack[0] }, "missing", 0, 1);
                Assert.Throws<KeyNotFoundException>(() => player.Initialize(Document(), new EmptySource(), Surfaces(), new[] { clip }));
                Assert.That(player.IsLoaded, Is.False);
                Assert.That(host.transform.childCount, Is.EqualTo(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void FutureOutOfRangeLensKeyRejectsWholeSequenceBeforeActivation()
        {
            GameObject host = new GameObject("content-player-test");
            try
            {
                ContentScenePlayer player = host.AddComponent<ContentScenePlayer>();
                SceneDocument document = Document();
                document.Cameras = new[] { new SceneCamera { Node = 0 } };
                string property = SceneMotionBindings.CameraField("node", "fov");
                PoseClip clip = PoseClipEditing.RecordValue(new PoseClip { Id = "lens", Duration = 1, Tracks = new PoseTrack[0] }, property, 0, 45);
                clip = PoseClipEditing.RecordValue(clip, property, 1, 200);
                Assert.Throws<ArgumentException>(() => player.Initialize(document, new EmptySource(), Surfaces(), new[] { clip }));
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
