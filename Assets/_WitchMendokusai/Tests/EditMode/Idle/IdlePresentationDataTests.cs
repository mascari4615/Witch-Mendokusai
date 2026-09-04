using NUnit.Framework;
using UnityEditor;
using WitchMendokusai.Idle;

namespace WitchMendokusai.Tests.Idle
{
    public sealed class IdlePresentationDataTests
    {
        private const string UI_CONTENT_PATH =
            "Assets/_WitchMendokusai/Idle/Data/Assets/UI_0001_Idle.asset";
        private const string HERO_CATALOG_PATH =
            "Assets/_WitchMendokusai/Idle/Data/Assets/HC_0001_Idle.asset";
        private const string GEAR_PRESENTATION_PATH =
            "Assets/_WitchMendokusai/Idle/Data/Assets/GP_0001_Idle.asset";
        private const string BATTLE_PRESENTATION_PATH =
            "Assets/_WitchMendokusai/Idle/Data/Assets/BP_0001_Idle.asset";
        private const string RUNTIME_SETTINGS_PATH =
            "Assets/_WitchMendokusai/Idle/Data/Assets/RT_0001_Idle.asset";

        [Test]
        public void UiContentMatchesScreenContracts()
        {
            UIContentSO content = AssetDatabase.LoadAssetAtPath<UIContentSO>(UI_CONTENT_PATH);
            Assert.NotNull(content);
            Assert.IsTrue(content.TryValidate(7, out string error), error);
            StringAssert.DoesNotContain("단계", content.BagTipText("머리", 1.5d, "없음"));
            StringAssert.DoesNotContain("단계", content.WornGearSummaryText(1.5d));
            StringAssert.DoesNotContain("단계", content.WornTipText("머리", 1.5d));
        }

        [Test]
        public void GearPresentationHasEveryTierSlotSprite()
        {
            GearPresentationSO presentation =
                AssetDatabase.LoadAssetAtPath<GearPresentationSO>(GEAR_PRESENTATION_PATH);
            Assert.NotNull(presentation);
            Assert.IsTrue(presentation.TryValidate(out string error), error);
        }

        [Test]
        public void HeroCatalogHasEveryPortrait()
        {
            HeroCatalogSO catalog = AssetDatabase.LoadAssetAtPath<HeroCatalogSO>(HERO_CATALOG_PATH);
            Assert.NotNull(catalog);
            Assert.IsTrue(catalog.TryValidate(out string error), error);
        }

        [Test]
        public void BattlePresentationHasValidVisualTuning()
        {
            BattlePresentationSO presentation =
                AssetDatabase.LoadAssetAtPath<BattlePresentationSO>(BATTLE_PRESENTATION_PATH);
            Assert.NotNull(presentation);
            Assert.IsTrue(presentation.TryValidate(out string error), error);
        }

        [Test]
        public void RuntimeSettingsHaveValidCadence()
        {
            RuntimeSettingsSO settings = AssetDatabase.LoadAssetAtPath<RuntimeSettingsSO>(RUNTIME_SETTINGS_PATH);
            Assert.NotNull(settings);
            Assert.IsTrue(settings.TryValidate(out string error), error);
        }
    }
}
