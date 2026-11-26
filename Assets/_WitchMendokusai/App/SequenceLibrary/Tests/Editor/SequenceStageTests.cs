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
                SequenceStage stage = host.AddComponent<SequenceStage>();
                typeof(SequenceStage).GetMethod("CaptureScreen", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stage, new object[0]);
                Assert.That(sky.enabled, Is.False);
                stage.Hide();
                Assert.That(sky.enabled, Is.EqualTo(enabled));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                if (sky != null) typeof(SkyDirector).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sky, new object[0]);
                UnityEngine.Object.DestroyImmediate(skyHost);
            }
        }

        [Test]
        public void OnlyOneStageHoldsTheScreenAndReleaseRestoresCameras()
        {
            GameObject cameraHost = new GameObject("sequence-stage-camera-test");
            GameObject first = new GameObject("sequence-stage-first");
            GameObject second = new GameObject("sequence-stage-second");
            try
            {
                Camera camera = cameraHost.AddComponent<Camera>();
                camera.cullingMask = 0b1011;
                SequenceStage firstStage = first.AddComponent<SequenceStage>();
                SequenceStage secondStage = second.AddComponent<SequenceStage>();
                Capture(firstStage);
                Assert.That(camera.enabled, Is.True, "Game code reads Camera.main every frame, so the camera must stay enabled");
                Assert.That(camera.cullingMask, Is.EqualTo(0));
                TargetInvocationException refused = Assert.Throws<TargetInvocationException>(() => Capture(secondStage));
                Assert.That(refused.InnerException, Is.TypeOf<InvalidOperationException>());
                secondStage.Hide();
                Assert.That(camera.cullingMask, Is.EqualTo(0), "A refused stage must not restore what another stage holds");
                firstStage.Hide();
                Assert.That(camera.cullingMask, Is.EqualTo(0b1011));
                Capture(secondStage);
                Assert.That(camera.cullingMask, Is.EqualTo(0));
                secondStage.Hide();
                Assert.That(camera.cullingMask, Is.EqualTo(0b1011));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(cameraHost);
            }
        }

        private static void Capture(SequenceStage stage)
        {
            typeof(SequenceStage).GetMethod("CaptureScreen", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stage, new object[0]);
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
                SequenceStage stage = host.AddComponent<SequenceStage>();
                SceneDocument document = Document();
                document.Lights = new[] { new SceneLight { Node = 0, Enabled = enabled, Intensity = .37f } };
                if (authoredCamera) document.Cameras = new[] { new SceneCamera { Node = 0 } };
                SceneInstance scene = SceneContentLoader.Load(document, new EmptySource(), Shader.Find("Universal Render Pipeline/Unlit"), host.transform);
                Prepare(stage, host, scene);
                Light[] lights = host.GetComponentsInChildren<Light>(true);
                Assert.That(lights.Length, Is.EqualTo(1));
                Assert.That(lights[0].enabled, Is.EqualTo(enabled));
                Assert.That(lights[0].intensity, Is.EqualTo(.37f));
                Assert.That(lights[0].cullingMask, Is.EqualTo(1 << 31));
                Assert.That(host.GetComponentsInChildren<Camera>(true).Length, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SceneWithoutAuthoredLightingGetsOneCameraRelativeFallbackLight(bool authoredCamera)
        {
            GameObject host = new GameObject("sequence-stage-fallback-test");
            try
            {
                SequenceStage stage = host.AddComponent<SequenceStage>();
                SceneDocument document = Document();
                if (authoredCamera) document.Cameras = new[] { new SceneCamera { Node = 0 } };
                SceneInstance scene = SceneContentLoader.Load(document, new EmptySource(), Shader.Find("Universal Render Pipeline/Unlit"), host.transform);
                Prepare(stage, host, scene);
                Light[] lights = host.GetComponentsInChildren<Light>(true);
                Assert.That(lights.Length, Is.EqualTo(1));
                Assert.That(lights[0].cullingMask, Is.EqualTo(1 << 31));
                Assert.That(lights[0].name, Is.EqualTo("Sequence light"));
                Camera camera = host.GetComponentsInChildren<Camera>(true)[0];
                Assert.That(camera.depth, Is.EqualTo(100f), "The content camera must draw over cameras outside the active scene");
                Assert.That(Vector3.Dot(camera.transform.forward, lights[0].transform.forward), Is.GreaterThan(.5f),
                    "The fallback key must illuminate the viewer-facing side with authored or generated cameras");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void Prepare(SequenceStage stage, GameObject host, SceneInstance scene)
        {
            typeof(SequenceStage).GetMethod("PrepareCamera", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stage, new object[] { host, scene });
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
