using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class StatusDetonationRulesTests
	{
		private static readonly UnitStatusType[] PRIORITY =
		{
			UnitStatusType.Burn, UnitStatusType.Freeze, UnitStatusType.Poison, UnitStatusType.Stun,
		};

		private static int Mask(params UnitStatusType[] types) => StatusDetonationRules.Mask(types);

		[Test]
		public void PicksHighestPriorityOnlyOne()
		{
			int active = Mask(UnitStatusType.Poison, UnitStatusType.Freeze, UnitStatusType.Burn);
			int detonates = Mask(UnitStatusType.Poison, UnitStatusType.Freeze, UnitStatusType.Burn);
			StatusDetonation result = StatusDetonationRules.Resolve(10, false, PRIORITY, active, detonates, 5f, 1f);
			Assert.IsTrue(result.Triggered);
			Assert.AreEqual(UnitStatusType.Burn, result.Status);
			Assert.AreEqual(50, result.Damage);
		}

		[Test]
		public void WeaponOnlyDetonatesItsOwnStatuses()
		{
			int active = Mask(UnitStatusType.Burn, UnitStatusType.Poison);
			StatusDetonation result = StatusDetonationRules.Resolve(10, false, PRIORITY, active, Mask(UnitStatusType.Poison), 5f, 1f);
			Assert.AreEqual(UnitStatusType.Poison, result.Status);
		}

		[Test]
		public void NoMatchingStatusKeepsDamage()
		{
			StatusDetonation result = StatusDetonationRules.Resolve(10, false, PRIORITY, Mask(UnitStatusType.Burn), Mask(UnitStatusType.Freeze), 5f, 1f);
			Assert.IsFalse(result.Triggered);
			Assert.AreEqual(10, result.Damage);
		}

		[Test]
		public void StatusOutsidePriorityNeverDetonates()
		{
			int slow = Mask(UnitStatusType.Slow);
			Assert.IsFalse(StatusDetonationRules.Resolve(10, false, PRIORITY, slow, slow, 5f, 1f).Triggered);
		}

		[Test]
		public void CriticalOverlapMultiplies()
		{
			Assert.AreEqual(10f, StatusDetonationRules.Multiplier(5f, 2f, true), 0.0001f);
			Assert.AreEqual(5f, StatusDetonationRules.Multiplier(5f, 2f, false), 0.0001f);
			StatusDetonation result = StatusDetonationRules.Resolve(7, true, PRIORITY, Mask(UnitStatusType.Burn), Mask(UnitStatusType.Burn), 5f, 2f);
			Assert.AreEqual(70, result.Damage);
		}

		[Test]
		public void ScaleTruncatesAndClamps()
		{
			Assert.AreEqual(7, StatusDetonationRules.Scale(5, 1.5f));
			Assert.AreEqual(int.MaxValue, StatusDetonationRules.Scale(int.MaxValue, 5f));
			Assert.AreEqual(0, StatusDetonationRules.Scale(0, 5f));
		}

		[Test]
		public void ZeroDamageDoesNotDetonate()
		{
			Assert.IsFalse(StatusDetonationRules.Resolve(0, false, PRIORITY, Mask(UnitStatusType.Burn), Mask(UnitStatusType.Burn), 5f, 1f).Triggered);
		}

		[Test]
		public void MaskHelpers()
		{
			int mask = Mask(UnitStatusType.Burn, UnitStatusType.Silence);
			Assert.IsTrue(StatusDetonationRules.Has(mask, UnitStatusType.Silence));
			Assert.AreEqual(Mask(UnitStatusType.Silence), StatusDetonationRules.Remove(mask, UnitStatusType.Burn));
		}
	}
}
