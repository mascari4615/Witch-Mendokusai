using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class DungeonThreatRulesTests
	{
		private static List<DungeonThreatLevel> Table() => new List<DungeonThreatLevel>
		{
			new DungeonThreatLevel { EnemyHealth = 1.5f, RewardMultiplier = 1.2f },
			new DungeonThreatLevel { EnemyDamage = 1.5f, EliteExtraPerGap = 1, RewardMultiplier = 1.5f },
			new DungeonThreatLevel { EnemySpeed = 1.2f, WaveRate = 1.3f, RewardMultiplier = 2f },
		};

		[Test]
		public void LevelZeroLeavesEveryValueUnchanged()
		{
			DungeonThreatEffect effect = DungeonThreatRules.Resolve(Table(), 0);

			Assert.AreEqual(0, effect.Level);
			Assert.AreEqual(0, effect.EliteExtraPerGap);
			Assert.AreEqual(1f, effect.RewardMultiplier);
			foreach (int value in new[] { 0, 1, 2, 3, 7, 24, 30, 500, 12000 })
			{
				Assert.AreEqual(value, effect.ScaleEnemyHealth(value));
				Assert.AreEqual(value, effect.UnscaleEnemyHealth(value));
				Assert.AreEqual(value, effect.ScaleEnemyDamage(value));
				Assert.AreEqual(value, effect.ScaleEnemySpeed(value));
				Assert.AreEqual(value, effect.ScaleSwarmCount(value));
				Assert.AreEqual(0, DungeonThreatRules.BonusNyang(value, effect.RewardMultiplier));
			}
			Assert.AreEqual(0.5f, effect.ScaleSpawnDelay(0.5f));
		}

		[Test]
		public void EmptyOrMissingTableResolvesToIdentity()
		{
			Assert.AreEqual(0, DungeonThreatRules.MaxLevel(null));
			Assert.AreEqual(0, DungeonThreatRules.Resolve(null, 3).Level);
			Assert.AreEqual(0, DungeonThreatRules.Resolve(new List<DungeonThreatLevel>(), 3).Level);
		}

		[Test]
		public void ConstraintsAccumulateAndRewardBelongsToTheLevel()
		{
			DungeonThreatEffect first = DungeonThreatRules.Resolve(Table(), 1);
			Assert.AreEqual(1.5f, first.EnemyHealth);
			Assert.AreEqual(1f, first.EnemyDamage);
			Assert.AreEqual(1.2f, first.RewardMultiplier);

			DungeonThreatEffect second = DungeonThreatRules.Resolve(Table(), 2);
			Assert.AreEqual(1.5f, second.EnemyHealth);
			Assert.AreEqual(1.5f, second.EnemyDamage);
			Assert.AreEqual(1, second.EliteExtraPerGap);
			Assert.AreEqual(1f, second.EnemySpeed);
			Assert.AreEqual(1.5f, second.RewardMultiplier);

			DungeonThreatEffect third = DungeonThreatRules.Resolve(Table(), 3);
			Assert.AreEqual(1.5f, third.EnemyHealth);
			Assert.AreEqual(1.5f, third.EnemyDamage);
			Assert.AreEqual(1, third.EliteExtraPerGap);
			Assert.AreEqual(1.2f, third.EnemySpeed);
			Assert.AreEqual(1.3f, third.WaveRate);
			Assert.AreEqual(2f, third.RewardMultiplier);
		}

		[Test]
		public void LevelOutsideTableIsClamped()
		{
			Assert.AreEqual(3, DungeonThreatRules.Resolve(Table(), 99).Level);
			Assert.AreEqual(0, DungeonThreatRules.Resolve(Table(), -1).Level);
		}

		[Test]
		public void NonPositiveMultiplierInDataCountsAsOne()
		{
			List<DungeonThreatLevel> table = new List<DungeonThreatLevel>
			{
				new DungeonThreatLevel { EnemyHealth = 0f, WaveRate = -1f, RewardMultiplier = 0f },
			};

			DungeonThreatEffect effect = DungeonThreatRules.Resolve(table, 1);

			Assert.AreEqual(1f, effect.EnemyHealth);
			Assert.AreEqual(1f, effect.WaveRate);
			Assert.AreEqual(1f, effect.RewardMultiplier);
		}

		[Test]
		public void ScalingAvoidsFloatErrorOnExactProducts()
		{
			DungeonThreatEffect effect = new DungeonThreatEffect(1, 1.2f, 1.5f, 0, 1.2f, 1.3f, 1f);

			Assert.AreEqual(12, effect.ScaleEnemyHealth(10));
			Assert.AreEqual(3, effect.ScaleEnemyDamage(2));
			Assert.AreEqual(18, effect.ScaleEnemySpeed(15));
			Assert.AreEqual(0, effect.ScaleEnemySpeed(0));
			Assert.AreEqual(32, effect.ScaleSwarmCount(24));
		}

		[Test]
		public void UnscaleRestoresOriginalHealthForExperienceTier()
		{
			foreach (float multiplier in new[] { 1f, 1.2f, 1.5f, 1.95f, 2.25f, 3.1f })
			{
				DungeonThreatEffect effect = new DungeonThreatEffect(1, multiplier, 1f, 0, 1f, 1f, 1f);
				for (int health = 0; health <= 3000; health++)
					Assert.AreEqual(health, effect.UnscaleEnemyHealth(effect.ScaleEnemyHealth(health)), $"multiplier {multiplier}, health {health}");
			}
		}

		[Test]
		public void SpawnDelayShrinksWithWaveRate()
		{
			DungeonThreatEffect effect = new DungeonThreatEffect(1, 1f, 1f, 0, 1f, 2f, 1f);

			Assert.AreEqual(0.25f, effect.ScaleSpawnDelay(0.5f));
		}

		[Test]
		public void UnlockFollowsPreviousLevelClear()
		{
			Assert.AreEqual(0, DungeonThreatRules.HighestUnlocked(0, 3));
			Assert.AreEqual(1, DungeonThreatRules.HighestUnlocked(1, 3));
			Assert.AreEqual(3, DungeonThreatRules.HighestUnlocked(4, 3));
			Assert.AreEqual(0, DungeonThreatRules.HighestUnlocked(-5, 3));
			Assert.AreEqual(0, DungeonThreatRules.HighestUnlocked(7, 0));
		}

		[Test]
		public void ClearOnlyRaisesRecord()
		{
			Assert.AreEqual(1, DungeonThreatRules.AfterClear(0, 0));
			Assert.AreEqual(3, DungeonThreatRules.AfterClear(3, 0));
			Assert.AreEqual(3, DungeonThreatRules.AfterClear(1, 2));
			Assert.AreEqual(1, DungeonThreatRules.AfterClear(-4, 0));
		}

		[Test]
		public void SelectionStaysInsideUnlockedRange()
		{
			Assert.AreEqual(0, DungeonThreatRules.StepSelection(0, -1, 2, 3));
			Assert.AreEqual(1, DungeonThreatRules.StepSelection(0, 1, 2, 3));
			Assert.AreEqual(2, DungeonThreatRules.StepSelection(2, 1, 2, 3));
			Assert.AreEqual(3, DungeonThreatRules.StepSelection(3, 1, 9, 3));
		}

		[Test]
		public void BonusNyangMatchesProvisionalMultipliers()
		{
			Assert.AreEqual(0, DungeonThreatRules.BonusNyang(500, 1f));
			Assert.AreEqual(100, DungeonThreatRules.BonusNyang(500, 1.2f));
			Assert.AreEqual(250, DungeonThreatRules.BonusNyang(500, 1.5f));
			Assert.AreEqual(500, DungeonThreatRules.BonusNyang(500, 2f));
			Assert.AreEqual(0, DungeonThreatRules.BonusNyang(0, 2f));
			Assert.AreEqual(0, DungeonThreatRules.BonusNyang(-10, 2f));
			Assert.AreEqual(0, DungeonThreatRules.BonusNyang(500, 0.5f));
		}

		[Test]
		public void ExtraElitesFillGapsBetweenNeighbours()
		{
			List<ExtraEliteSlot> slots = DungeonThreatRules.ExtraEliteSlots(new[] { 120f, 360f, 480f }, 1);

			Assert.AreEqual(2, slots.Count);
			Assert.AreEqual(240f, slots[0].Time);
			Assert.AreEqual(0, slots[0].SourceIndex);
			Assert.AreEqual(420f, slots[1].Time);
			Assert.AreEqual(1, slots[1].SourceIndex);
		}

		[Test]
		public void ExtraElitesSplitEvenlyAndCanBeAbsent()
		{
			List<ExtraEliteSlot> slots = DungeonThreatRules.ExtraEliteSlots(new[] { 100f, 400f }, 2);

			Assert.AreEqual(2, slots.Count);
			Assert.AreEqual(200f, slots[0].Time);
			Assert.AreEqual(300f, slots[1].Time);
			Assert.AreEqual(0, DungeonThreatRules.ExtraEliteSlots(new[] { 100f, 400f }, 0).Count);
			Assert.AreEqual(0, DungeonThreatRules.ExtraEliteSlots(new[] { 100f }, 3).Count);
			Assert.AreEqual(0, DungeonThreatRules.ExtraEliteSlots(new float[0], 3).Count);
		}
	}
}
