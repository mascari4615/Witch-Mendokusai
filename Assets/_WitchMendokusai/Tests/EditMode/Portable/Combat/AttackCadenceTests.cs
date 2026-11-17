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
		public void WeaponInterval_SharedStacks_AddToWeaponStacks()
		{
			Assert.AreEqual(0.55f, AttackCadence.WeaponInterval(1f, 2, 1, 0.15f, 0.25f, 0), 0.0001f);
			Assert.AreEqual(AttackCadence.Interval(1f, 3, 0.15f, 0.25f, 10), AttackCadence.WeaponInterval(1f, 2, 1, 0.15f, 0.25f, 10), 0.0001f);
		}

		[Test]
		public void WeaponInterval_NegativeStacks_SumThenClamp()
		{
			Assert.AreEqual(1f, AttackCadence.WeaponInterval(1f, 2, -5, 0.15f, 0.25f, 0), 0.0001f);
			Assert.AreEqual(0.85f, AttackCadence.WeaponInterval(1f, 2, -1, 0.15f, 0.25f, 0), 0.0001f);
		}

		[Test]
		public void WeaponInterval_Cooldown_MultipliesAndKeepsFloor()
		{
			Assert.AreEqual(0.63f, AttackCadence.WeaponInterval(1f, 2, 0, 0.15f, 0.25f, 10), 0.0001f);
			Assert.AreEqual(0.25f, AttackCadence.WeaponInterval(1f, 5, 3, 0.15f, 0.25f, 15), 0.0001f);
		}

		// 검은 최소 주기를 초로 가짐. 비율로 바꿔 넣어도 이전 식 max(최소, 기본 * (1 - 스택 * 0.2)) 과 같아야 함
		[TestCase(0, 1.5f)]
		[TestCase(2, 0.9f)]
		[TestCase(4, 0.3f)]
		[TestCase(5, 0.15f)]
		[TestCase(9, 0.15f)]
		public void WeaponInterval_MinimumSecondsAsRatio_MatchesSwordFormula(int stacks, float expected)
		{
			Assert.AreEqual(expected, AttackCadence.WeaponInterval(1.5f, stacks, 0, 0.2f, 0.15f / 1.5f, 0), 0.0001f);
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
