namespace WitchMendokusai
{
	/// <summary>
	/// 체력이 한 번 움직인 결과 (TASK-WM-215).
	/// 「얼마였다가 얼마가 됐고, 그래서 죽었나」만 담는다 — 연출·소리·화면은 이 값을 받아 쓰는 쪽의 일.
	/// </summary>
	public readonly struct HealthChange
	{
		public readonly int PreviousHp;
		public readonly int NewHp;
		public readonly int MaxHp;

		public HealthChange(int previousHp, int newHp, int maxHp)
		{
			PreviousHp = previousHp;
			NewHp = newHp;
			MaxHp = maxHp;
		}

		/// <summary>실제로 깎인 양(회복이면 0). 과잉 피해는 세지 않는다 — 남은 체력만큼만 깎인다.</summary>
		public int AppliedDamage => PreviousHp > NewHp ? PreviousHp - NewHp : 0;

		/// <summary>실제로 채워진 양(피해면 0). 최대치를 넘는 회복은 세지 않는다.</summary>
		public int AppliedHeal => NewHp > PreviousHp ? NewHp - PreviousHp : 0;

		public bool Died => NewHp <= 0;
	}

	/// <summary>
	/// 체력 판정 — 순수 함수 (TASK-WM-215).
	///
	/// 왜 떼어냈나: 「맞으면 얼마 남나 / 죽었나」는 게임의 규칙이지 화면의 일이 아니다.
	/// 씬 컴포넌트 안에 있으면 헤드리스 서버가 같은 전투를 재현할 수 없다.
	/// 규칙은 여기 한 벌만 두고, 유니티 쪽은 결과를 받아 연출만 한다.
	/// </summary>
	public static class HealthResolution
	{
		/// <summary>체력을 <paramref name="delta"/> 만큼 움직인다(음수 = 피해). 0 과 최대치 사이로 잘린다.</summary>
		public static HealthChange Apply(int currentHp, int maxHp, int delta)
		{
			int target = currentHp + delta;
			int clamped = Numerics.Mathf.Clamp(target, 0, maxHp);
			return new HealthChange(currentHp, clamped, maxHp);
		}

		public static HealthChange ApplyDamage(int currentHp, int maxHp, DamageInfo damageInfo)
		{
			return Apply(currentHp, maxHp, -damageInfo.damage);
		}

		public static HealthChange ApplyHeal(int currentHp, int maxHp, int healAmount)
		{
			return Apply(currentHp, maxHp, healAmount);
		}
	}
}
