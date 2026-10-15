using System;
using System.Collections.Generic;
using NUnit.Framework;
using SceneContent.Serialization;
using SceneContent.Unity;
using UnityEngine;

namespace SceneContent.Tests
{
    public sealed class SceneComponentTests
    {
        [Test]
        public void CameraLightAndSurfaceValuesRoundTripWithDeepOwnership()
        {
            SceneDocument document = SceneFixtures.Document(SceneFixtures.Bytes());
            document.Cameras = new[] { new SceneCamera { Node = 1, FieldOfView = 37 } };
            document.Lights = new[] { new SceneLight { Node = 2, Kind = "point", Intensity = 3 } };
            document.Materials[0].LightingModel = "lit";
            document.Materials[0].Metallic = .4f;
            SceneDocument copy = SceneCloning.Copy(document);
            copy.Cameras[0].ClearColor[0] = .7f;
            copy.Materials[0].BaseColor[0] = .1f;
            copy.Resources[0].Path = "other.bin";
            Assert.That(document.Cameras[0].ClearColor[0], Is.EqualTo(.12f));
            Assert.That(document.Materials[0].BaseColor[0], Is.EqualTo(1));
            Assert.That(document.Resources[0].Path, Is.EqualTo("mesh.bin"));
            SceneDocument loaded = SceneJson.Read(SceneJson.Write(document));
            Assert.That(loaded.Cameras[0].FieldOfView, Is.EqualTo(37));
            Assert.That(loaded.Lights[0].Intensity, Is.EqualTo(3));
            Assert.That(loaded.Materials[0].Metallic, Is.EqualTo(.4f));
            loaded.Cameras[0].Far = loaded.Cameras[0].Near;
            Assert.Throws<ArgumentException>(() => SceneValidation.Ensure(loaded));
        }

        [Test]
        public void PruningRetainsCameraAndLightAnchorsAndRemapsTheirBindings()
        {
            SceneDocument document = SceneFixtures.Document(SceneFixtures.Bytes());
            List<SceneNode> nodes = new List<SceneNode>(document.Nodes);
            for (int index = 0; index < 3; index++)
            {
                nodes.Add(new SceneNode { Id = "extra-" + index, Parent = 0, Position = new float[3], Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } });
            }
            document.Nodes = nodes.ToArray();
            document.Cameras = new[] { new SceneCamera { Node = 6 } };
            document.Lights = new[] { new SceneLight { Node = 7 } };
            SceneDocument compact = ScenePruning.Snapshot(document, new string[0]);
            Assert.That(compact.Nodes.Length, Is.EqualTo(7));
            Assert.That(compact.Cameras[0].Node, Is.EqualTo(5));
            Assert.That(compact.Lights[0].Node, Is.EqualTo(6));
        }

        [Test]
        public void UnityComponentsRespectHostCameraPolicyAndCaptureCurrentLens()
        {
            SkinWeights previous = QualitySettings.skinWeights;
            SceneInstance instance = null;
            try
            {
                QualitySettings.skinWeights = SkinWeights.FourBones;
                byte[] bytes = SceneFixtures.Bytes();
                SceneDocument document = SceneFixtures.Document(bytes);
                document.Cameras = new[] { new SceneCamera { Node = 1, FieldOfView = 37 } };
                document.Lights = new[] { new SceneLight { Node = 2, Kind = "point", Intensity = 3 } };
                document.Materials[0].LightingModel = "lit";
                instance = SceneContentLoader.LoadWithSurfaces(document, new MemorySource { Bytes = bytes },
                    new StandardSurfaceFactory(Shader.Find("Universal Render Pipeline/Unlit"), Shader.Find("Universal Render Pipeline/Lit")), activateCameras: false);
                Camera camera = instance.FindNode("node-1").GetComponent<Camera>();
                Assert.That(camera.enabled, Is.False);
                Assert.That(instance.FindNode("node-2").GetComponent<Light>().type, Is.EqualTo(LightType.Point));
                Assert.That(instance.GetComponentInChildren<Renderer>().sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                camera.fieldOfView = 61;
                SceneDocument snapshot = SceneSnapshot.Capture(document, instance);
                Assert.That(snapshot.Cameras[0].FieldOfView, Is.EqualTo(61));
                Assert.That(snapshot.Cameras[0].Enabled, Is.True);
                Assert.That(document.Cameras[0].FieldOfView, Is.EqualTo(37));
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance.gameObject);
                }
                QualitySettings.skinWeights = previous;
            }
        }
    }
}
