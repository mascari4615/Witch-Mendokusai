using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;

namespace WitchMendokusai.Tests
{
    public sealed class BootRootWiringTests
    {
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
