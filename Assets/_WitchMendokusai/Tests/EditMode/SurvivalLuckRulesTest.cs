using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 행운 계수, 화면 밖 갱신 단계, 상자 개수 가중치 (mc16, mc18, mc36)
	public class SurvivalLuckRulesTest
	{
		private SurvivalRunRules rules;

		[SetUp]
		public void SetUp() => rules = ScriptableObject.CreateInstance<SurvivalRunRules>();

		[TearDown]
		public void TearDown() => Object.DestroyImmediate(rules);

		[Test]
		public void Rarity_multiplier_adds_permanent_luck_and_equipment_level()
		{
			rules.RarityLuckScale = 1f;
			rules.EquipmentLevelLuck = 0.1f;

			Assert.AreEqual(1f, rules.RarityLuckMultiplier(0f, 0), 1e-5f);
			Assert.AreEqual(1.5f, rules.RarityLuckMultiplier(0.2f, 3), 1e-5f);
		}

		[Test]
		public void Rarity_scale_zero_ignores_luck()
		{
			rules.RarityLuckScale = 0f;

			Assert.AreEqual(1f, rules.RarityLuckMultiplier(1f, 9), 1e-5f);
		}

		[Test]
		public void Floor_item_multiplier_uses_its_own_scale()
		{
			rules.FloorItemLuckScale = 2f;

			Assert.AreEqual(1.6f, rules.FloorItemLuckMultiplier(0.3f), 1e-5f);
		}

		[TestCase(5f, 0.1f)]
		[TestCase(19.9f, 0.1f)]
		[TestCase(20f, 0.2f)]
		[TestCase(34.9f, 0.2f)]
		[TestCase(35f, 0.4f)]
		[TestCase(200f, 0.4f)]
		public void Offscreen_interval_steps_by_distance(float distance, float expected)
		{
			Assert.AreEqual(expected, rules.OffscreenUpdateInterval(distance), 1e-5f);
		}

		[Test]
		public void Choice_count_does_not_consume_roll_when_chance_zero()
		{
			rules.ExtraChoiceChancePerLuck = 0f;
			int rolls = 0;

			int count = rules.ChoiceCount(3, 0.5f, () => { rolls++; return 0f; });

			Assert.AreEqual(3, count);
			Assert.AreEqual(0, rolls);
		}

		[Test]
		public void Choice_count_adds_extra_when_roll_under_chance()
		{
			rules.ExtraChoiceChancePerLuck = 0.5f;
			rules.ExtraChoiceCount = 1;

			Assert.AreEqual(4, rules.ChoiceCount(3, 1f, () => 0.4f));
			Assert.AreEqual(3, rules.ChoiceCount(3, 1f, () => 0.6f));
		}

		[Test]
		public void Chest_reward_count_without_luck_weight_ignores_luck()
		{
			rules.ChestRewardCounts = new()
			{
				new ChestRewardCountWeight { Count = 1, Weight = 70f },
				new ChestRewardCountWeight { Count = 5, Weight = 30f },
			};

			Assert.AreEqual(1, rules.PickChestRewardCount(5f, 0.69f));
			Assert.AreEqual(5, rules.PickChestRewardCount(5f, 0.71f));
		}

		[Test]
		public void Chest_reward_count_luck_weight_shifts_odds()
		{
			rules.ChestRewardCounts = new()
			{
				new ChestRewardCountWeight { Count = 1, Weight = 50f },
				new ChestRewardCountWeight { Count = 5, Weight = 50f, LuckWeight = 50f },
			};

			Assert.AreEqual(1, rules.PickChestRewardCount(0f, 0.4f));
			Assert.AreEqual(5, rules.PickChestRewardCount(1f, 0.4f));
		}
	}
}
