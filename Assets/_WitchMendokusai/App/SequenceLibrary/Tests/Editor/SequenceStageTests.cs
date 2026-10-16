using System;
using System.Reflection;
using NUnit.Framework;
using SceneContent;
using SceneContent.Unity;
using UnityEngine;

namespace WitchMendokusai.SequenceLibrary.Tests
{
    public sealed class SequenceStageTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void LobbySkyRetainsItsPreviousEnabledStateAfterViewing(bool enabled)
        {
            GameObject skyHost = new GameObject("sequence-stage-sky-test");
            GameObject host = new GameObject("sequence-stage-menu-test");
            SkyDirector sky = null;
            try
            {
                sky = skyHost.AddComponent<SkyDirector>();
                typeof(SkyDirector).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sky, new object[0]);
                Assert.That(SkyDirector.Instance, Is.SameAs(sky));
                sky.enabled = enabled;
                SequenceLibraryMenu menu = host.AddComponent<SequenceLibraryMenu>();
                typeof(SequenceLibraryMenu).GetMethod("CaptureLobby", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, new object[0]);
                Assert.That(sky.enabled, Is.False);
                typeof(SequenceLibraryMenu).GetMethod("StopSequence", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, new object[0]);
                Assert.That(sky.enabled, Is.EqualTo(enabled));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                if (sky != null) typeof(SkyDirector).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sky, new object[0]);
                UnityEngine.Object.DestroyImmediate(skyHost);
            }
        }

        [TestCase(false, true)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(true, false)]
        public void AuthoredLightsKeepTheirStateAndStayInsideTheContentLayer(bool authoredCamera, bool enabled)
        {
            GameObject host = new GameObject("sequence-stage-test");
            try
            {
                SequenceLibraryMenu menu = host.AddComponent<SequenceLibraryMenu>();
                SceneDocument document = Document();
                document.Lights = new[] { new SceneLight { Node = 0, Enabled = enabled, Intensity = .37f } };
                if (authoredCamera) document.Cameras = new[] { new SceneCamera { Node = 0 } };
                SceneInstance scene = SceneContentLoader.Load(document, new EmptySource(), Shader.Find("Universal Render Pipeline/Unlit"), host.transform);
                Prepare(menu, host, scene);
                Light[] lights = host.GetComponentsInChildren<Light>(true);
                Assert.That(lights.Length, Is.EqualTo(1));
                Assert.That(lights[0].enabled, Is.EqualTo(enabled));
                Assert.That(lights[0].intensity, Is.EqualTo(.37f));
                Assert.That(lights[0].cullingMask, Is.EqualTo(1 << 31));
                Assert.That(host.GetComponentsInChildren<Camera>(true).Length, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void SceneWithoutAuthoredLightingGetsOneFallbackLight()
        {
            GameObject host = new GameObject("sequence-stage-fallback-test");
            try
            {
                SequenceLibraryMenu menu = host.AddComponent<SequenceLibraryMenu>();
                SceneInstance scene = SceneContentLoader.Load(Document(), new EmptySource(), Shader.Find("Universal Render Pipeline/Unlit"), host.transform);
                Prepare(menu, host, scene);
                Light[] lights = host.GetComponentsInChildren<Light>(true);
                Assert.That(lights.Length, Is.EqualTo(1));
                Assert.That(lights[0].cullingMask, Is.EqualTo(1 << 31));
                Assert.That(lights[0].name, Is.EqualTo("Sequence light"));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void Prepare(SequenceLibraryMenu menu, GameObject host, SceneInstance scene)
        {
            typeof(SequenceLibraryMenu).GetMethod("PrepareCamera", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, new object[] { host, scene });
        }

        private static SceneDocument Document()
        {
            return new SceneDocument
            {
                Id = "lighting-test", Nodes = new[] { new SceneNode { Id = "node", Position = new float[3], Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } } },
                Meshes = new MeshInstance[0], Materials = new SurfaceMaterial[0], Resources = new ContentResource[0]
            };
        }

        private sealed class EmptySource : IContentSource
        {
            public byte[] Read(ContentResource resource) { throw new InvalidOperationException("No resources expected"); }
        }
    }
}
