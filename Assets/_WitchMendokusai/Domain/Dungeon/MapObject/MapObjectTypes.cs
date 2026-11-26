using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 맵 오브젝트 분류. 정본 memo wm/design/map-objects-20261119 분류 표. 숫자는 자산 직렬화 값이라 고정
	public enum MapObjectCategory
	{
		Chest = 0,
		Challenge = 1,
		HoldZone = 2,
		Exchange = 3,
		Luck = 4,
		HealSpring = 5,
		Info = 6,
		Breakable = 7,
		Merchant = 8,
		Travel = 9,
		// 갇힌 사람. 예산 밖, 마을이 기다리는 사람이 있을 때만 판에 하나 (MonsterSpawner.Rescue)
		Rescue = 10,
	}

	// 상호작용 비용 종류. 지원 여부는 MapObjectRules.IsCostSupported
	public enum MapObjectCostType
	{
		None = 0,
		RiskAccept = 1,
		// 최대 체력의 % 를 현재 체력에서 냄. 체력 1 은 남김
		HealthPercent = 2,
		MaxHealthPercent = 3,
		// 이번 판에 주운 냥. 들고 온 냥은 못 씀 (DungeonManager.RunNyang)
		RunNyang = 4,
		Card = 5,
	}

	// 상호작용 효과. 새 종류는 값 하나와 MonsterSpawner.MapObjectEffects 의 처리 하나
	public enum MapObjectEffectType
	{
		SpawnElite = 0,
		SpawnChest = 1,
		// Value: 최대 체력의 % 회복
		Heal = 2,
		// Value: 반경 (m). 0 이면 판 전체. 다른 맵 오브젝트를 작은 지도와 화면 밖 화살표에 드러냄
		RevealMapObjects = 3,
		// 갇힌 사람을 구함. 열쇠는 DungeonManager.RescueWanted, 알림은 CaptiveRescuedEvent
		Rescue = 4,
	}

	// 효과가 나가는 때. 머무름 (HoldSeconds > 0) 이 없는 오브젝트는 OnActivate 만
	public enum MapObjectEffectTiming
	{
		OnActivate = 0,
		OnHoldComplete = 1,
	}

	// 머무름 구역 밖에 있을 때 진행
	public enum MapObjectHoldOutside
	{
		Pause = 0,
		Decay = 1,
	}

	// 머무름 한 틱의 결과
	public enum MapObjectHoldResult
	{
		Running = 0,
		Completed = 1,
		Failed = 2,
	}

	// 바닥 선 표식 모양 (MapObjectData.ShowMarker). 외형 (Model) 과 따로
	public enum MapObjectMarkerShape
	{
		Diamond = 0,
		Square = 1,
		Ring = 2,
	}

	// 작은 지도와 화면 밖 화살표 범주. None 이면 표시 안 함. 색은 데이터의 MarkerColor
	public enum MapObjectSignal
	{
		None = 0,
		Objective = 1,
		Reward = 2,
		Danger = 3,
		Utility = 4,
	}

	// 수락 전 보여 줄 보상 범주. 글자와 색은 MapObjectSettings.RewardPreviews
	public enum MapObjectRewardPreview
	{
		None = 0,
		Chest = 1,
		Heal = 2,
		Luck = 3,
		Info = 4,
	}

	[Serializable]
	public class MapObjectEffect
	{
		[field: SerializeField] public MapObjectEffectType Type { get; private set; }
		[field: SerializeField] public MapObjectEffectTiming Timing { get; private set; }
		[field: SerializeField, Tooltip("SpawnElite 대상. 비우면 사건표의 확정 상자 정예")] public Monster Monster { get; private set; }
		[field: SerializeField, Tooltip("SpawnElite 전용. 처치 시 상자 확정")] public bool GuaranteedChest { get; private set; } = true;
		[field: SerializeField, Min(1)] public int Count { get; private set; } = 1;
		[field: SerializeField, Tooltip("Heal: 최대 체력 %. RevealMapObjects: 반경 m, 0 이면 판 전체"), Min(0f)] public float Value { get; private set; }
		[field: SerializeField, Tooltip("꽝 확률 %. 0 이면 항상 나감. 운 성소"), Range(0f, 100f)] public float FailPercent { get; private set; }

		public MapObjectEffect()
		{
		}

		public MapObjectEffect(MapObjectEffectType type, Monster monster, bool guaranteedChest, int count)
		{
			Type = type;
			Monster = monster;
			GuaranteedChest = guaranteedChest;
			Count = count;
		}
	}

	// 오브젝트 SO 를 돌리는 쪽이 지원하는 범위와 엔진 없는 계산. 데이터 검사, 런타임, 시험이 같은 함수를 씀
	public static class MapObjectRules
	{
		// 부동소수 곱 오차로 정수 비용이 한 칸 올라가는 것 방지 (10 x 1.0 이 10.0000001 이 되는 경우)
		private const float ROUNDING_TOLERANCE = 0.0001f;

		public static bool IsCostSupported(MapObjectCostType costType) => costType switch
		{
			MapObjectCostType.None => true,
			MapObjectCostType.RiskAccept => true,
			MapObjectCostType.HealthPercent => true,
			MapObjectCostType.RunNyang => true,
			_ => false,
		};

		public static bool IsEffectSupported(MapObjectEffectType effectType) => effectType switch
		{
			MapObjectEffectType.SpawnElite => true,
			MapObjectEffectType.SpawnChest => true,
			MapObjectEffectType.Heal => true,
			MapObjectEffectType.RevealMapObjects => true,
			MapObjectEffectType.Rescue => true,
			_ => false,
		};

		// 재사용 배율. 비용 종류와 무관하게 이 한 곳
		public static float CostMultiplier(float growth, int usedCount) => Mathf.Pow(growth, usedCount);

		// 값에 배율을 곱해 올림, 값이 있으면 최소 1. 냥은 값 그대로, 체력은 최대 체력 % 를 값으로
		public static int ScaledCost(float baseValue, float multiplier)
		{
			if (baseValue <= 0f)
				return 0;
			return Mathf.Max(1, Mathf.CeilToInt(baseValue * multiplier - ROUNDING_TOLERANCE));
		}

		// 체력 비용. 최대 체력 % 를 올림. 내고 나서 체력 1 이상 남아야 낼 수 있음
		public static int HealthCost(int maxHp, float percent, float multiplier) => ScaledCost(maxHp * percent / 100f, multiplier);

		public static bool CanPayHealth(int currentHp, int cost) => currentHp - cost >= 1;

		// 최대 체력의 % (반올림, 최소 1). 회복 샘 회복량, 경계 밖 피해
		public static int PercentOfMaxHealth(int maxHp, float percent) => Mathf.Max(1, Mathf.RoundToInt(maxHp * percent / 100f));

		// 꽝 굴림. roll 은 [0, 1)
		public static bool RollSucceeds(float failPercent, double roll) => roll * 100.0 >= failPercent;

		// 머무름 한 틱. 안이면 진행, 밖이면 멈춤 또는 감소. 제한 시간이 있으면 경과 시각으로 실패
		public static MapObjectHoldResult StepHold(ref float progress, ref float age, float deltaSeconds, bool inside, float holdSeconds, MapObjectHoldOutside outside, float decayPerSecond, float timeLimit)
		{
			age += deltaSeconds;
			if (inside)
				progress += deltaSeconds;
			else if (outside == MapObjectHoldOutside.Decay)
				progress = Mathf.Max(0f, progress - decayPerSecond * deltaSeconds);
			if (progress >= holdSeconds)
			{
				progress = holdSeconds;
				return MapObjectHoldResult.Completed;
			}
			if (timeLimit > 0f && age >= timeLimit)
				return MapObjectHoldResult.Failed;
			return MapObjectHoldResult.Running;
		}
	}
}
