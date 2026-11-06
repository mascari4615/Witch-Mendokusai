using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class AttackCadenceTests
	{
		[Test]
		public void Interval_NoStacks_ReturnsBase()
		{
			Assert.AreEqual(2f, AttackCadence.Interval(2f, 0, 0.15f, 0.25f), 0.0001f);
		}

		[Test]
		public void Interval_Stacks_ReducesLinearly()
		{
			Assert.AreEqual(0.7f, AttackCadence.Interval(1f, 2, 0.15f, 0.25f), 0.0001f);
		}

		[Test]
		public void Interval_ManyStacks_StopsAtFloor()
		{
			Assert.AreEqual(0.25f, AttackCadence.Interval(1f, 100, 0.15f, 0.25f), 0.0001f);
		}

		[Test]
		public void Interval_NegativeStacks_ReturnsBase()
		{
			Assert.AreEqual(1f, AttackCadence.Interval(1f, -3, 0.15f, 0.25f), 0.0001f);
		}

		[Test]
		public void ChargeRatio_ClampsBetweenZeroAndOne()
		{
			Assert.AreEqual(0f, AttackCadence.ChargeRatio(-1f, 2f), 0.0001f);
			Assert.AreEqual(0.5f, AttackCadence.ChargeRatio(1f, 2f), 0.0001f);
			Assert.AreEqual(1f, AttackCadence.ChargeRatio(9f, 2f), 0.0001f);
			Assert.AreEqual(1f, AttackCadence.ChargeRatio(0f, 0f), 0.0001f);
		}
	}
}
