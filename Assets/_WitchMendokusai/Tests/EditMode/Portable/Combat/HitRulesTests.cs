using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class HitRulesTests
	{
		private static readonly HitRule PLAIN = new(false, false, 0f);

		[Test]
		public void SameFrameSameTargetHitsOnce()
		{
			HitLedger ledger = new();
			Assert.IsTrue(ledger.TryHit(1, 0f, 10, PLAIN));
			Assert.IsFalse(ledger.TryHit(1, 0f, 10, PLAIN));
			Assert.IsTrue(ledger.TryHit(2, 0f, 10, PLAIN));
			Assert.IsTrue(ledger.TryHit(1, 0.02f, 11, PLAIN));
		}

		[Test]
		public void OncePerTargetNeverHitsAgain()
		{
			HitLedger ledger = new();
			HitRule once = new(true, false, 0f);
			Assert.IsTrue(ledger.TryHit(1, 0f, 1, once));
			Assert.IsFalse(ledger.TryHit(1, 5f, 300, once));
			Assert.IsTrue(ledger.TryHit(2, 5f, 300, once));
		}

		[Test]
		public void ReenterRequiresLeaving()
		{
			HitLedger ledger = new();
			HitRule reenter = new(false, true, 0f);
			Assert.IsTrue(ledger.TryHit(1, 0f, 1, reenter));
			Assert.IsFalse(ledger.TryHit(1, 1f, 60, reenter));
			ledger.Leave(1);
			Assert.IsTrue(ledger.TryHit(1, 2f, 120, reenter));
			Assert.IsFalse(ledger.TryHit(1, 3f, 180, reenter));
		}

		[Test]
		public void LeaveWithoutRecordIsIgnored()
		{
			HitLedger ledger = new();
			ledger.Leave(9);
			Assert.AreEqual(0, ledger.Count);
		}

		[Test]
		public void IntervalBlocksUntilTime()
		{
			HitLedger ledger = new();
			HitRule interval = new(false, false, 0.5f);
			Assert.IsTrue(ledger.TryHit(1, 0f, 1, interval));
			Assert.IsFalse(ledger.TryHit(1, 0.4f, 25, interval));
			Assert.IsTrue(ledger.TryHit(1, 0.5f, 31, interval));
		}

		[Test]
		public void BlockedAttemptDoesNotExtendInterval()
		{
			HitLedger ledger = new();
			HitRule interval = new(false, false, 0.5f);
			ledger.TryHit(1, 0f, 1, interval);
			ledger.TryHit(1, 0.3f, 20, interval);
			Assert.IsTrue(ledger.TryHit(1, 0.5f, 31, interval));
		}

		[Test]
		public void ClearForgetsEverything()
		{
			HitLedger ledger = new();
			HitRule once = new(true, false, 0f);
			ledger.TryHit(1, 0f, 1, once);
			ledger.Clear();
			Assert.IsTrue(ledger.TryHit(1, 0f, 1, once));
		}

		[Test]
		public void NegativeIntervalBecomesZero()
		{
			Assert.AreEqual(0f, new HitRule(false, false, -1f).RehitInterval);
			Assert.AreEqual(0.3f, PLAIN.WithInterval(0.3f).RehitInterval, 0.0001f);
		}

		[Test]
		public void KnockbackDirectionKinds()
		{
			float x;
			float z;
			Assert.IsFalse(KnockbackDirections.TryResolve(KnockbackDirectionKind.AwayFromAttacker, 1, 1, 0, 0, 1, 0, 1, 0, out x, out z));
			Assert.IsTrue(KnockbackDirections.TryResolve(KnockbackDirectionKind.FromHitCenter, 3, 0, 0, 0, 0, 1, 0, 1, out x, out z));
			Assert.AreEqual(1f, x, 0.0001f);
			Assert.AreEqual(0f, z, 0.0001f);
			Assert.IsTrue(KnockbackDirections.TryResolve(KnockbackDirectionKind.HitForward, 3, 0, 0, 0, 0, 2, 5, 0, out x, out z));
			Assert.AreEqual(1f, z, 0.0001f);
			Assert.IsTrue(KnockbackDirections.TryResolve(KnockbackDirectionKind.HitVelocity, 3, 0, 0, 0, 0, 2, -4, 0, out x, out z));
			Assert.AreEqual(-1f, x, 0.0001f);
		}

		[Test]
		public void KnockbackZeroVectorFallsBack()
		{
			Assert.IsFalse(KnockbackDirections.TryResolve(KnockbackDirectionKind.FromHitCenter, 1, 1, 1, 1, 0, 0, 0, 0, out float x, out float z));
			Assert.AreEqual(0f, x);
			Assert.AreEqual(0f, z);
		}
	}
}
