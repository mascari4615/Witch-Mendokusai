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
				"skill-aim-line", "skill-aim-range", "skill-aim-caption", "floating-tabs", "wipe-button",
				"side", "tabs", "side-close", "panel-title", "panel-caption", "panel-body", "tooltip");
		}

		[Test]
		public void RepeatedTemplatesExposeBindingPoints()
		{
			AssertElements("IdleChoiceCard.uxml", "choice", "choice-icon", "choice-label");
			AssertElements("IdleQueueChip.uxml", "chip");
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
