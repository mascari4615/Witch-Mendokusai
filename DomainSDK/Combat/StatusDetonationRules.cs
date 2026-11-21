using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 상태 폭발 결과. 한 타격에 상태 하나만 소모
	public readonly struct StatusDetonation
	{
		public readonly bool Triggered;
		public readonly UnitStatusType Status;
		public readonly int Damage;
		public readonly float Multiplier;

		public StatusDetonation(bool triggered, UnitStatusType status, int damage, float multiplier)
		{
			Triggered = triggered;
			Status = status;
			Damage = damage;
			Multiplier = multiplier;
		}

		public static StatusDetonation None(int damage) => new StatusDetonation(false, default, damage, 1f);
	}

	// 상태 폭발 규칙. 엔진 무관
	// - 한 무기가 건 상태를 다른 무기가 소모해 큰 피해
	// - 걸려 있고 무기가 터뜨릴 수 있는 상태 중 우선순위가 가장 높은 하나만 지움. 한 타격당 한 번
	// - 치명타와 겹치면 곱해짐. 치명타 피해는 이미 계산된 값이라 폭발 배율만 곱하면 겹친 효과가 나옴
	public static class StatusDetonationRules
	{
		// 상태 집합은 비트 하나가 상태 하나. 비트 번호는 상태 종류의 정수 값
		public static int Bit(UnitStatusType type) => 1 << (int)type;

		public static int Mask(params UnitStatusType[] types)
		{
			int mask = 0;
			if (types == null)
				return mask;
			foreach (UnitStatusType type in types)
				mask |= Bit(type);
			return mask;
		}

		public static bool Has(int mask, UnitStatusType type) => (mask & Bit(type)) != 0;

		public static int Remove(int mask, UnitStatusType type) => mask & ~Bit(type);

		// 우선순위 순서의 첫 일치. 걸려 있고 터뜨릴 수 있는 것만
		public static bool TryPick(IReadOnlyList<UnitStatusType> priority, int activeMask, int detonateMask, out UnitStatusType picked)
		{
			picked = default;
			if (priority == null)
				return false;
			int candidates = activeMask & detonateMask;
			if (candidates == 0)
				return false;
			for (int i = 0; i < priority.Count; i++)
			{
				if (Has(candidates, priority[i]) == false)
					continue;
				picked = priority[i];
				return true;
			}
			return false;
		}

		// 최종 배율. 치명타 배율은 1 이면 곱하지 않음
		public static float Multiplier(float detonationMultiplier, float criticalMultiplier, bool isCritical)
		{
			float multiplier = Math.Max(1f, detonationMultiplier);
			if (isCritical)
				multiplier *= Math.Max(1f, criticalMultiplier);
			return multiplier;
		}

		// 정수 곱셈. 소수는 버림, 정수 범위를 넘으면 최댓값
		public static int Scale(int damage, float multiplier)
		{
			if (damage <= 0)
				return damage;
			double scaled = Math.Floor(damage * (double)Math.Max(1f, multiplier));
			return scaled >= int.MaxValue ? int.MaxValue : (int)scaled;
		}

		public static StatusDetonation Resolve(
			int damage,
			bool isCritical,
			IReadOnlyList<UnitStatusType> priority,
			int activeMask,
			int detonateMask,
			float detonationMultiplier,
			float criticalMultiplier)
		{
			if (damage <= 0 || TryPick(priority, activeMask, detonateMask, out UnitStatusType picked) == false)
				return StatusDetonation.None(damage);
			float multiplier = Multiplier(detonationMultiplier, criticalMultiplier, isCritical);
			return new StatusDetonation(true, picked, Scale(damage, multiplier), multiplier);
		}
	}
}
