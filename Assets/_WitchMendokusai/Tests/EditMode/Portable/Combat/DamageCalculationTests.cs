using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 피해량 계산이 <b>엔진 없이도, 그리고 되풀이해도 같은 답</b>을 낸다 (TASK-WM-215).
	///
	/// 옛 구현은 유니티 전역 난수를 안에서 굴려 같은 상황을 재현할 수 없었다.
	/// 이제 굴린 값을 받으므로 시험이 치명타를 <b>지정</b>할 수 있고, 서버도 씨앗을 고정할 수 있다.
	/// </summary>
	public sealed class DamageCalculationTests
	{
		[Test]
		public void 보너스가_없으면_기본_피해_그대로()
		{
			DamageOutcome outcome = DamageCalculation.Resolve(100, 0, 0, 0, 0, roll: 50);

			Assert.AreEqual(100, outcome.Damage);
			Assert.IsFalse(outcome.IsCritical);
		}

		[Test]
		public void 고정_보너스는_먼저_더해진다()
		{
			DamageOutcome outcome = DamageCalculation.Resolve(100, 20, 0, 0, 0, roll: 99);

			Assert.AreEqual(120, outcome.Damage);
		}

		[Test]
		public void 퍼센트_보너스는_더한_뒤에_곱해진다()
		{
			DamageOutcome outcome = DamageCalculation.Resolve(100, 20, 50, 0, 0, roll: 99);

			Assert.AreEqual(180, outcome.Damage, "(100+20) * 1.5");
		}

		[Test]
		public void 주사위가_확률보다_작으면_치명타()
		{
			DamageOutcome outcome = DamageCalculation.Resolve(100, 0, 0, criticalChancePercent: 30, criticalDamagePercent: 50, roll: 29);

			Assert.IsTrue(outcome.IsCritical);
			Assert.AreEqual(150, outcome.Damage);
		}

		[Test]
		public void 주사위가_확률과_같으면_치명타가_아니다()
		{
			DamageOutcome outcome = DamageCalculation.Resolve(100, 0, 0, criticalChancePercent: 30, criticalDamagePercent: 50, roll: 30);

			Assert.IsFalse(outcome.IsCritical, "경계값 — 옛 구현의 `<` 비교 그대로");
			Assert.AreEqual(100, outcome.Damage);
		}

		[Test]
		public void 치명타_확률이_0_이면_어떤_주사위여도_평타()
		{
			DamageOutcome outcome = DamageCalculation.Resolve(100, 0, 0, 0, 500, roll: 0);

			Assert.IsFalse(outcome.IsCritical);
			Assert.AreEqual(100, outcome.Damage);
		}

		[Test]
		public void 같은_주사위면_몇_번을_돌려도_같은_값()
		{
			DamageOutcome first = DamageCalculation.Resolve(77, 3, 25, 40, 60, roll: 12);
			DamageOutcome second = DamageCalculation.Resolve(77, 3, 25, 40, 60, roll: 12);

			Assert.AreEqual(first.Damage, second.Damage, "리플레이·서버 검증이 성립하는 근거");
			Assert.AreEqual(first.IsCritical, second.IsCritical);
		}

		[Test]
		public void 능력치로_부른_결과가_숫자로_부른_것과_같다()
		{
			UnitStat stat = new UnitStat();
			stat[UnitStatType.DAMAGE_BONUS] = 25;
			stat[UnitStatType.CRITICAL_CHANCE] = 40;
			stat[UnitStatType.CRITICAL_DAMAGE] = 60;

			DamageOutcome viaStat = DamageCalculation.Resolve(77, 3, stat, roll: 12);
			DamageOutcome viaNumbers = DamageCalculation.Resolve(77, 3, 25, 40, 60, roll: 12);

			Assert.AreEqual(viaNumbers.Damage, viaStat.Damage);
			Assert.AreEqual(viaNumbers.IsCritical, viaStat.IsCritical);
		}
	}
}
