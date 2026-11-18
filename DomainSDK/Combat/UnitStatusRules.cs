using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 상태이상 판정의 정본. 엔진 무관
	// - 남은 시간은 UnitStat 의 상태이상 칸에 밀리초로 보관
	public static class UnitStatusRules
	{
		// 칸 번호가 곧 (int) 종류. 표시와 틱 배열이 이 번호로 찾음
		public static readonly IReadOnlyList<UnitStatusType> ALL = new[]
		{
			UnitStatusType.Burn,
			UnitStatusType.Poison,
			UnitStatusType.Freeze,
			UnitStatusType.Slow,
			UnitStatusType.Stun,
		};

		public static int Count => ALL.Count;

		public static UnitStatType StatType(UnitStatusType type) => type switch
		{
			UnitStatusType.Burn => UnitStatType.BURN,
			UnitStatusType.Poison => UnitStatType.POISON,
			UnitStatusType.Freeze => UnitStatType.FREEZE,
			UnitStatusType.Slow => UnitStatType.SLOW,
			UnitStatusType.Stun => UnitStatType.STUN,
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, "상태이상 칸 없음"),
		};

		public static int Milliseconds(float seconds) => seconds <= 0f ? 0 : (int)Math.Ceiling(seconds * 1000d);

		// 지속 중 다시 걸리면 긴 쪽으로. 겹쳐 쌓지 않음
		public static int Refresh(int currentMilliseconds, int addedMilliseconds) => Math.Max(Math.Max(0, currentMilliseconds), addedMilliseconds);

		// 보스 지속 시간. 비율 0이면 보스 면역
		public static float BossSeconds(float seconds, float bossRatio) => Math.Max(0f, seconds) * Math.Min(1f, Math.Max(0f, bossRatio));

		// 한 번 틱 피해. 최대 체력 비율과 최소값 중 큰 쪽
		public static int TickDamage(int maxHealth, float maxHealthPercent, int minDamage)
		{
			int byPercent = (int)Math.Ceiling(Math.Max(0, maxHealth) * Math.Max(0f, maxHealthPercent) / 100d);
			return Math.Max(Math.Max(0, minDamage), byPercent);
		}

		// 밀리초 감소. 0 아래로 내려가지 않음
		public static int Elapse(int remainingMilliseconds, int elapsedMilliseconds) => Math.Max(0, remainingMilliseconds - Math.Max(0, elapsedMilliseconds));
	}
}
