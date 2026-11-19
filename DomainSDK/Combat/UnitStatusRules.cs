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
			UnitStatusType.Bleed,
			UnitStatusType.Shock,
			UnitStatusType.Mark,
			UnitStatusType.Fear,
			UnitStatusType.Silence,
		};

		public static int Count => ALL.Count;

		public static UnitStatType StatType(UnitStatusType type) => type switch
		{
			UnitStatusType.Burn => UnitStatType.BURN,
			UnitStatusType.Poison => UnitStatType.POISON,
			UnitStatusType.Freeze => UnitStatType.FREEZE,
			UnitStatusType.Slow => UnitStatType.SLOW,
			UnitStatusType.Stun => UnitStatType.STUN,
			UnitStatusType.Bleed => UnitStatType.BLEED,
			UnitStatusType.Shock => UnitStatType.SHOCK,
			UnitStatusType.Mark => UnitStatType.MARK,
			UnitStatusType.Fear => UnitStatType.FEAR,
			UnitStatusType.Silence => UnitStatType.SILENCE,
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

		// 쌓임 수. 걸려 있던 중이면 하나 더, 아니면 1부터. 상한 1 이하는 쌓이지 않고 항상 1
		public static int NextStacks(int currentStacks, bool wasActive, int maxStacks)
		{
			int limit = Math.Max(1, maxStacks);
			if (wasActive == false)
				return 1;
			return Math.Min(limit, Math.Max(1, currentStacks) + 1);
		}

		// 쌓임 수만큼 곱한 틱 피해. 쌓임 하나는 TickDamage 와 같음
		public static int StackedTickDamage(int maxHealth, float maxHealthPercent, int minDamage, int stacks) => TickDamage(maxHealth, maxHealthPercent, minDamage) * Math.Max(1, stacks);

		// 받는 피해 증가 (표식). 퍼센트 0 이하면 그대로, 올림이라 1 이상 피해는 줄지 않음
		public static int AmplifiedDamage(int damage, float bonusPercent)
		{
			if (damage <= 0 || bonusPercent <= 0f)
				return damage;
			return (int)Math.Ceiling(damage * (1d + bonusPercent / 100d));
		}

		// 감전이 옆 몬스터로 튀는 피해. 틱 피해의 퍼센트, 퍼센트가 있으면 최소 1
		public static int ChainDamage(int tickDamage, float chainPercent)
		{
			if (tickDamage <= 0 || chainPercent <= 0f)
				return 0;
			return Math.Max(1, (int)Math.Round(tickDamage * chainPercent / 100d));
		}

		// 남은 시간 테두리 그림 번호. 0 이 가장 짧음, frameCount - 1 이 가득. 전체 시간이 없으면 가득
		public static int RingFrame(int remainingMilliseconds, int totalMilliseconds, int frameCount)
		{
			if (frameCount <= 0)
				return 0;
			if (totalMilliseconds <= 0)
				return frameCount - 1;
			double ratio = Math.Min(1d, Math.Max(0d, (double)remainingMilliseconds / totalMilliseconds));
			return Math.Min(frameCount - 1, Math.Max(0, (int)Math.Ceiling(ratio * frameCount) - 1));
		}

		// 밀리초 감소. 0 아래로 내려가지 않음
		public static int Elapse(int remainingMilliseconds, int elapsedMilliseconds) => Math.Max(0, remainingMilliseconds - Math.Max(0, elapsedMilliseconds));
	}
}
