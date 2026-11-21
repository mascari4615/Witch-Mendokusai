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
			double carry = 0d;
			Assert.AreEqual(10, effect.ScaleHeal(10, ref carry));
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
		public void HealCanDropToZeroAndChoiceKeepsOne()
		{
			double carry = 0d;
			Assert.AreEqual(0, Resolve((DungeonConstraintEffectKind.HealRate, -100)).ScaleHeal(10, ref carry));
			carry = 0d;
			Assert.AreEqual(5, Resolve((DungeonConstraintEffectKind.HealRate, -50)).ScaleHeal(10, ref carry));
			Assert.AreEqual(1, Resolve((DungeonConstraintEffectKind.ChoiceCount, -5)).ChoiceCount(3));
			Assert.AreEqual(80, Resolve((DungeonConstraintEffectKind.StartHealth, -20)).StartHealth(100));
		}

		[Test]
		public void SmallHealsAccumulateInsteadOfVanishing()
		{
			// 재생 1 에 회복 -25%: 네 번에 세 번 회복
			DungeonConstraintRunEffect effect = Resolve((DungeonConstraintEffectKind.HealRate, -25));
			double carry = 0d;
			int total = 0;
			for (int i = 0; i < 4; i++)
				total += effect.ScaleHeal(1, ref carry);
			Assert.AreEqual(3, total);
		}

		[Test]
		public void InvalidSumsFailFast()
		{
			Assert.Throws<System.ArgumentOutOfRangeException>(() => Resolve((DungeonConstraintEffectKind.StartHealth, -100)));
			Assert.Throws<System.ArgumentOutOfRangeException>(() => Resolve((DungeonConstraintEffectKind.StartHealth, 10)));
			Assert.Throws<System.ArgumentOutOfRangeException>(() => Resolve((DungeonConstraintEffectKind.HealRate, -150)));
			Assert.Throws<System.ArgumentOutOfRangeException>(() => Resolve((DungeonConstraintEffectKind.EliteExtra, -1)));
			Assert.Throws<System.ArgumentOutOfRangeException>(() => Resolve((DungeonConstraintEffectKind.WaveRate, -100)));
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

		private static DungeonConstraintSideEffect Side(params (DungeonConstraintEffectKind Kind, int Value)[] effects)
		{
			List<DungeonConstraintRunRules.Entry> entries = new List<DungeonConstraintRunRules.Entry>();
			foreach ((DungeonConstraintEffectKind kind, int value) in effects)
				entries.Add(new DungeonConstraintRunRules.Entry(kind, value));
			return DungeonConstraintRunRules.ResolveSide(entries);
		}

		[Test]
		public void SideRulesIgnoreRunKindsAndTakeShortestChestGap()
		{
			DungeonConstraintSideEffect side = Side((DungeonConstraintEffectKind.WaveRate, 20), (DungeonConstraintEffectKind.KillChest, 300), (DungeonConstraintEffectKind.KillChest, 200));
			Assert.AreEqual(200, side.KillChestEvery);
			Assert.IsFalse(side.IsKillChestDue(199, 0));
			Assert.IsTrue(side.IsKillChestDue(200, 0));
			Assert.IsFalse(side.IsKillChestDue(399, 1));
			Assert.IsTrue(side.IsKillChestDue(400, 1));
		}

		[Test]
		public void HealLossNyangCarriesFraction()
		{
			DungeonConstraintSideEffect side = Side((DungeonConstraintEffectKind.HealLossToNyang, 50));
			double carry = 0d;
			Assert.AreEqual(0, side.HealLossNyang(1, ref carry));
			Assert.AreEqual(1, side.HealLossNyang(1, ref carry));
		}

		[Test]
		public void InvalidSideValuesFailFast()
		{
			Assert.Throws<System.ArgumentOutOfRangeException>(() => Side((DungeonConstraintEffectKind.KillChest, 0)));
			Assert.Throws<System.ArgumentOutOfRangeException>(() => Side((DungeonConstraintEffectKind.EliteChest, -10)));
		}
	}
}
