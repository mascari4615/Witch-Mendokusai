using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 품질 단계와 Unity 품질 단계 이름의 대응. PlayerPrefs 와 실제 적용은 건드리지 않는 순수 함수만
	public class QualityPresetTest
	{
		private static readonly string[] NAMES = { "Very Low", "Low", "Medium", "High", "Very High", "Ultra" };

		[Test]
		public void Tiers_map_to_low_medium_high_levels()
		{
			Assert.AreEqual(1, QualityPreset.ResolveLevelIndex(NAMES, QualityPreset.TIER_LOW));
			Assert.AreEqual(2, QualityPreset.ResolveLevelIndex(NAMES, QualityPreset.TIER_MID));
			Assert.AreEqual(3, QualityPreset.ResolveLevelIndex(NAMES, QualityPreset.TIER_HIGH));
		}

		[Test]
		public void Out_of_range_tier_is_clamped()
		{
			Assert.AreEqual(QualityPreset.TIER_LOW, QualityPreset.ClampTier(-5));
			Assert.AreEqual(QualityPreset.TIER_HIGH, QualityPreset.ClampTier(9));
		}

		[Test]
		public void Missing_level_name_gives_minus_one()
			=> Assert.AreEqual(-1, QualityPreset.ResolveLevelIndex(new[] { "Low", "High" }, QualityPreset.TIER_MID));

		[Test]
		public void Project_has_all_three_levels()
		{
			for (int tier = 0; tier < QualityPreset.TIER_COUNT; tier++)
				Assert.GreaterOrEqual(QualityPreset.ResolveLevelIndex(QualitySettings.names, tier), 0, "tier " + tier);
		}
	}
}
