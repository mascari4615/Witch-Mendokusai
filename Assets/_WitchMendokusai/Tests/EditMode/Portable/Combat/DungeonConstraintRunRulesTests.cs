using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class DungeonConstraintRunRulesTests
	{
		private static DungeonConstraintRunEffect Resolve(params (DungeonConstraintEffectKind Kind, int Value)[] effects)
		{
			List<DungeonConstraintRunRules.Entry> entries = new List<DungeonConstraintRunRules.Entry>();
			foreach ((DungeonConstraintEffectKind kind, int value) in effects)
				entries.Add(new DungeonConstraintRunRules.Entry(kind, value));
			return DungeonConstraintRunRules.Resolve(entries);
		}

		[Test]
		public void NoEffectLeavesRunUnchanged()
		{
			DungeonConstraintRunEffect effect = Resolve((DungeonConstraintEffectKind.Stat, 50));
			Assert.AreEqual(1f, effect.WaveMultiplier);
			Assert.AreEqual(10, effect.ScaleHeal(10));
			Assert.AreEqual(3, effect.ChoiceCount(3));
			Assert.AreEqual(100, effect.StartHealth(100));
			Assert.AreEqual(180d, effect.ScaleDifficultyStep(180d));
		}

		[Test]
		public void SameKindsAdd()
		{
			DungeonConstraintRunEffect effect = Resolve((DungeonConstraintEffectKind.WaveRate, 20), (DungeonConstraintEffectKind.WaveRate, 40));
			Assert.AreEqual(1.6f, effect.WaveMultiplier, 0.0001f);
		}

		[Test]
		public void HealCanDropToZeroButChoiceAndHealthKeepOne()
		{
			Assert.AreEqual(0, Resolve((DungeonConstraintEffectKind.HealRate, -100)).ScaleHeal(10));
			Assert.AreEqual(5, Resolve((DungeonConstraintEffectKind.HealRate, -50)).ScaleHeal(10));
			Assert.AreEqual(1, Resolve((DungeonConstraintEffectKind.ChoiceCount, -5)).ChoiceCount(3));
			Assert.AreEqual(1, Resolve((DungeonConstraintEffectKind.StartHealth, -100)).StartHealth(100));
			Assert.AreEqual(80, Resolve((DungeonConstraintEffectKind.StartHealth, -20)).StartHealth(100));
		}

		[Test]
		public void ThreatGetsWaveAndEliteOnTopKeepingLevelAndReward()
		{
			DungeonThreatEffect threat = new DungeonThreatEffect(2, 1.5f, 1.5f, 1, 1f, 1.3f, 1.5f);
			DungeonThreatEffect applied = Resolve((DungeonConstraintEffectKind.WaveRate, 20), (DungeonConstraintEffectKind.EliteExtra, 2)).ApplyTo(threat);
			Assert.AreEqual(2, applied.Level);
			Assert.AreEqual(1.5f, applied.RewardMultiplier);
			Assert.AreEqual(3, applied.EliteExtraPerGap);
			Assert.AreEqual(1.56f, applied.WaveRate, 0.0001f);
		}

		[Test]
		public void DifficultyPaceShortensStep()
		{
			Assert.AreEqual(144d, Resolve((DungeonConstraintEffectKind.DifficultyPace, -20)).ScaleDifficultyStep(180d), 0.001d);
		}
	}
}
