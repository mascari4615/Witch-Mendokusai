namespace WitchMendokusai
{
	/// <summary>한 방의 결과 — 얼마나 아팠고 치명타였나 (TASK-WM-215).</summary>
	public readonly struct DamageOutcome
	{
		public readonly int Damage;
		public readonly bool IsCritical;

		public DamageOutcome(int damage, bool isCritical)
		{
			Damage = damage;
			IsCritical = isCritical;
		}
	}

	/// <summary>
	/// 피해량 계산 — 순수 함수 (TASK-WM-215).
	///
	/// ★ 주사위를 <b>안에서 굴리지 않는다.</b> 굴린 값을 받는다.
	///   원래 이 계산은 유니티 전역 난수를 직접 썼고, 그래서 같은 상황을 다시 재현할 수 없었다
	///   (아레나 리플레이·서버 검증이 불가능한 이유). 굴리는 쪽을 밖으로 빼면
	///   게임은 지금처럼 전역 난수를, 서버는 판(match)마다 씨앗 고정 난수를 쓸 수 있다.
	///
	/// 계산 순서와 자르는 자리(정수 변환)는 옛 구현 그대로다 — 손맛이 바뀌면 안 된다.
	/// </summary>
	public static class DamageCalculation
	{
		/// <summary>치명타 판정에 쓰는 주사위 면 수 — 0 이상 이 값 미만을 굴려 넣는다.</summary>
		public const int ROLL_RANGE = 100;

		/// <param name="roll">0 이상 <see cref="ROLL_RANGE"/> 미만으로 굴린 값. 치명타 확률보다 작으면 치명타.</param>
		public static DamageOutcome Resolve(
			int baseDamage,
			int damageBonus,
			int damageBonusPercent,
			int criticalChancePercent,
			int criticalDamagePercent,
			int roll)
		{
			int damage = baseDamage + damageBonus;
			damage = (int)(damage * (1 + (damageBonusPercent / 100f)));

			bool isCritical = criticalChancePercent > 0 && roll < criticalChancePercent;
			if (isCritical)
			{
				damage = (int)(damage * (1 + (criticalDamagePercent / 100f)));
			}

			return new DamageOutcome(damage, isCritical);
		}

		/// <summary>능력치에서 곧바로 — 부르는 쪽이 칸 이름을 몰라도 되게.</summary>
		public static DamageOutcome Resolve(int baseDamage, int damageBonus, UnitStat attackerStat, int roll)
		{
			return Resolve(
				baseDamage,
				damageBonus,
				attackerStat[UnitStatType.DAMAGE_BONUS],
				attackerStat[UnitStatType.CRITICAL_CHANCE],
				attackerStat[UnitStatType.CRITICAL_DAMAGE],
				roll);
		}
	}
}
