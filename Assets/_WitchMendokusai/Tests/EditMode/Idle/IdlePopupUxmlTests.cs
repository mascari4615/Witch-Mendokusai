using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace WitchMendokusai.Tests.Idle
{
	public sealed class IdlePopupUxmlTests
	{
		private const string ROOT = "Assets/_WitchMendokusai/Idle/UI/";

		[Test]
		public void BattleScreenExposesStaticBindingPoints()
		{
			AssertElements("IdleBattleScreen.uxml", "shell", "battle", "skill-aim", "skill-aim-origin",
				"skill-aim-line", "skill-aim-range", "skill-aim-caption", "wipe-button",
				"side", "tabs", "tab-0", "tab-1", "tab-2", "tab-3",
				"tab-4", "tab-5", "tab-6", "panel-title", "panel-caption", "panel-body",
				"doll-page-host", "item-page-host", "codex-page-host", "shop-page-host", "lab-page-host",
				"dungeon-page-host", "invest-page-host", "map-popup-host", "gear-popup-host", "hero-popup-host",
				"gold-popup-host", "settings-popup-host", "away-popup-host", "tooltip");
		}

		[Test]
		public void FullScreenUsesOneImageCollapseControl()
		{
			VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ROOT + "IdleBattleHud.uxml");
			Assert.NotNull(asset);
			TemplateContainer tree = asset.Instantiate();
			Button split = tree.Q<Button>("split-button");
			Assert.NotNull(split);
			Assert.IsEmpty(split.text);
			Assert.NotNull(split.Q<VisualElement>(className: "idle-split-chevron"));
		}

		[Test]
		public void HudKeepsOnlyGoldSummaryAndSkillAuto()
		{
			VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ROOT + "IdleBattleHud.uxml");
			Assert.NotNull(asset);
			TemplateContainer tree = asset.Instantiate();
			Button gold = tree.Q<Button>("gold-chip");
			Assert.NotNull(gold);
			Assert.NotNull(gold.Q<VisualElement>(className: "idle-gold-icon"));
			Assert.NotNull(gold.Q<Label>("gold-value"));
			Assert.Null(tree.Q("gold-income"));
			Assert.Null(tree.Q("speed-2"));
			Button autoCast = tree.Q<Button>("auto-cast-button");
			Assert.NotNull(autoCast);
			Assert.IsTrue(autoCast.parent.ClassListContains("idle-hand"));
		}

		[Test]
		public void RepeatedTemplatesExposeBindingPoints()
		{
			AssertElements("IdleChoiceCard.uxml", "choice", "choice-icon", "choice-label");
			AssertElements("IdleQueueChip.uxml", "chip");
			AssertElements("IdleRowButton.uxml", "row");
			AssertElements("IdleRowLabel.uxml", "row");
		}

		[Test]
		public void MapPopupExposesBindingPoints()
		{
			AssertElements("IdleMapPopup.uxml", "popup", "map-close", "map-rows");
		}

		[Test]
		public void HeroPopupExposesBindingPoints()
		{
			AssertElements("IdleHeroPopup.uxml", "popup", "hero-close", "hero-grid");
		}

		[Test]
		public void GoldPopupExposesBindingPoints()
		{
			AssertElements("IdleGoldPopup.uxml", "popup", "gold-amount", "gold-income", "gold-close");
		}

		[Test]
		public void SettingsPopupExposesBindingPoints()
		{
			AssertElements("IdleSettingsPopup.uxml", "popup", "settings-close", "speed-0", "speed-1", "speed-2",
				"log-label", "note-label");
		}

		[Test]
		public void AwayPopupExposesBindingPoints()
		{
			AssertElements("IdleAwayPopup.uxml", "popup", "away-span", "gold-value", "kills-value",
				"stages-value", "items-value", "away-warning", "away-close");
		}

		private static void AssertElements(string assetName, params string[] elementNames)
		{
			VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ROOT + assetName);
			Assert.NotNull(asset, assetName);
			TemplateContainer tree = asset.Instantiate();
			foreach (string elementName in elementNames)
			{
				Assert.NotNull(tree.Q(elementName), assetName + " is missing #" + elementName);
			}
		}
	}
}
