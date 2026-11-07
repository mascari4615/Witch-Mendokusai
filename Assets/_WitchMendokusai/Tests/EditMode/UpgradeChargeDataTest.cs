using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public class UpgradeChargeDataTest
	{
		private const string UPGRADE_FOLDER = "Assets/_WitchMendokusai/Domain/Upgrade/ScriptableObject";
		private const string PANEL_PATH = "Assets/_WitchMendokusai/Domain/Upgrade/UI/Panel/[Panel] Upgrade.prefab";
		private const string THEME_PATH = "Assets/_WitchMendokusai/Domain/UI/Survival/SurvivalUITheme.asset";

		private static readonly UpgradeType[] CHARGE_TYPES = { UpgradeType.RerollCharge, UpgradeType.SkipCharge, UpgradeType.BanCharge };

		private static List<UpgradeData> AllUpgrades() => AssetDatabase.FindAssets("t:" + nameof(UpgradeData), new[] { UPGRADE_FOLDER })
			.Select(guid => AssetDatabase.LoadAssetAtPath<UpgradeData>(AssetDatabase.GUIDToAssetPath(guid)))
			.ToList();

		private static int PolynomialPrice(int level) => 10 * level * level + 30;

		[Test]
		public void Charge_upgrades_use_polynomial_prices_and_add_one_per_level()
		{
			Dictionary<int, UpgradeData> upgrades = AllUpgrades().ToDictionary(upgrade => upgrade.ID);

			foreach (UpgradeType type in CHARGE_TYPES)
			{
				UpgradeData upgrade = upgrades[(int)type];

				Assert.AreEqual(type, upgrade.Type);
				Assert.AreEqual(UnitStatType.NONE, upgrade.UnitStatType);
				Assert.AreEqual(UpgradeCategory.Choice, upgrade.Category);
				for (int level = 1; level <= upgrade.MaxLevel; level++)
				{
					Assert.AreEqual(PolynomialPrice(level), upgrade.PricePerLevel[level - 1], upgrade.name);
					Assert.AreEqual(1f, upgrade.ValuePerLevel[level - 1], upgrade.name);
				}
			}
		}

		[Test]
		public void Upgrade_ids_match_their_type()
		{
			foreach (UpgradeData upgrade in AllUpgrades())
				Assert.AreEqual((int)upgrade.Type, upgrade.ID, upgrade.name);
		}

		[Test]
		public void Panel_has_slot_for_every_upgrade()
		{
			GameObject panel = AssetDatabase.LoadAssetAtPath<GameObject>(PANEL_PATH);

			Assert.GreaterOrEqual(panel.GetComponentsInChildren<UIUpgradeSlot>(true).Length, AllUpgrades().Count);
		}

		[Test]
		public void Theme_has_style_for_every_category()
		{
			SurvivalUITheme theme = AssetDatabase.LoadAssetAtPath<SurvivalUITheme>(THEME_PATH);

			foreach (UpgradeCategory category in System.Enum.GetValues(typeof(UpgradeCategory)))
				Assert.AreEqual(category, theme.Style(category).Category);
		}
	}
}
