using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 스킬창 장비 칸의 재사용 대기 표시. 사용자 2026-11-21 플레이 피드백 (장비 칸 쿨타임 표시 없음)
	// 남은 비율 1 은 막 씀, 0 은 사용 가능. 남은 초가 음수면 시간이 아니라 충전으로 차는 장비 (움직이거나 멈춰 서서 참)
	public readonly struct CooldownState
	{
		public readonly float RemainingRatio;
		public readonly float RemainingSeconds;
		public readonly float Interval;

		public CooldownState(float remainingRatio, float remainingSeconds, float interval)
		{
			RemainingRatio = Mathf.Clamp01(remainingRatio);
			RemainingSeconds = remainingSeconds;
			Interval = interval;
		}

		public bool IsCharge => RemainingSeconds < 0f;

		// 다음 사용 시각 (Time.time 기준) 과 그 주기로 만듦. WaitForSeconds 와 같은 게임 시간이라 일시정지 중에는 멈춤
		public static CooldownState FromReadyTime(float readyTime, float interval)
		{
			float remaining = Mathf.Max(0f, readyTime - Time.time);
			return new CooldownState(interval > 0f ? remaining / interval : 0f, remaining, interval);
		}

		// 충전형. 다 찬 비율 (0~1). 다 차면 사용 가능
		public static CooldownState FromCharge(float charged) => new(1f - charged, -1f, 0f);
	}

	// 재사용 대기나 충전이 있는 무기 부품이 구현. 상시로 붙어 있는 장비 (상시 오라, 훌라후프, 자석, 능력치만 주는 도구) 는 등록 안 함
	public interface IEquipmentCooldown
	{
		CooldownState Cooldown { get; }
	}

	// 장비 번호에서 그 장비 무기 부품의 대기 상태를 찾음. 무기가 켜질 때 등록, 꺼질 때 해제
	// 등록 없는 장비는 스킬창이 상시 장비로 보임
	public static class EquipmentCooldowns
	{
		private static readonly Dictionary<int, IEquipmentCooldown> sources = new();

		// 장비 무기 부품만 부름. 장비 문맥이 비면 등록 누락이라 바로 실패 (NullReferenceException)
		public static void Register(IItemData equipment, IEquipmentCooldown source) => sources[equipment.ID] = source;

		public static void Unregister(IItemData equipment, IEquipmentCooldown source)
		{
			if (sources.TryGetValue(equipment.ID, out IEquipmentCooldown registered) && ReferenceEquals(registered, source))
				sources.Remove(equipment.ID);
		}

		public static bool TryGet(IItemData equipment, out CooldownState state)
		{
			if (sources.TryGetValue(equipment.ID, out IEquipmentCooldown source))
			{
				state = source.Cooldown;
				return true;
			}
			state = default;
			return false;
		}

		// 도메인 재로드 없이 Play 를 다시 켜도 지난 판 등록이 남지 않게
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetOnPlay() => sources.Clear();
	}
}
