using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;

namespace WitchMendokusai.Tests
{
    public sealed class BootRootWiringTests
    {
        // 이전 실행의 정지 배율로 새 로딩이 멈춘 실제 재현. 기존 코드의 Awake에서는 배율 유지
        [TestCase(0f)]
        [TestCase(float.Epsilon)]
        [TestCase(0.2f)]
        public void NewTimeManagerRestoresClockAndRetainsPauseOwnership(float previousScale)
        {
            Assert.That(TimeManager.Instance == null, Is.True, "Editor test requires no running world");
            float originalScale = Time.timeScale;
            GameObject host = new GameObject("ClockBootTest");
            host.SetActive(false);
            GameObject pauser = new GameObject("ClockBootPauser");
            TimeManager timer = host.AddComponent<TimeManager>();
            try
            {
                Time.timeScale = previousScale;
                typeof(TimeManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(timer, null);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(timer.IsPaused, Is.False);
                timer.Pause(pauser);
                Assert.That(Time.timeScale, Is.EqualTo(float.Epsilon));
                timer.Resume(pauser);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally
            {
                typeof(TimeManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(timer, null);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(pauser);
                Time.timeScale = originalScale;
            }
        }

        [Test]
        public void EditorAndBootConfigUseTheSameRootWithACompleteCatalog()
        {
            VContainerSettings settings = AssetDatabase.LoadAssetAtPath<VContainerSettings>("Assets/_WitchMendokusai/Core/VContainer/VContainerSettings.asset");
            BootConfig config = AssetDatabase.LoadAssetAtPath<BootConfig>("Assets/_WitchMendokusai/Domain/Application/Assets/BootConfig.asset");
            Assert.That(settings, Is.Not.Null);
            Assert.That(config, Is.Not.Null);
            Assert.That(config.RootScopePrefab, Is.Not.Null);
            Assert.That(settings.RootLifetimeScope, Is.SameAs(config.RootScopePrefab));
            Assert.That(AssetDatabase.GetAssetPath(settings.RootLifetimeScope), Does.Not.Contain("/Resources/"));
            SerializedObject root = new SerializedObject(settings.RootLifetimeScope);
            SingletonCatalog catalog = root.FindProperty("catalog").objectReferenceValue as SingletonCatalog;
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.SOManager, Is.Not.Null);
            foreach (GameObject prefab in catalog.Prefabs) Assert.That(prefab, Is.Not.Null, "Catalog contains a missing prefab reference");
            Assert.That(catalog.Get<UIRoot>(), Is.Not.Null);
            Assert.That(catalog.Get<InputManager>(), Is.Not.Null);
            Assert.That(catalog.Get<DataLoader>(), Is.Not.Null);
        }
    }
}
