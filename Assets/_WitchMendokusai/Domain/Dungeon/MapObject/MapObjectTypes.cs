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
	}

	// 상호작용 비용 종류. 지원 여부는 MapObjectRules.IsCostSupported
	public enum MapObjectCostType
	{
		None = 0,
		RiskAccept = 1,
		HealthPercent = 2,
		MaxHealthPercent = 3,
		RunNyang = 4,
		Card = 5,
	}

	// 상호작용 효과. 새 종류는 값 하나와 MonsterSpawner.MapObjectEffects 의 처리 하나
	public enum MapObjectEffectType
	{
		SpawnElite = 0,
		SpawnChest = 1,
	}

	// 월드 표식 모양. Prefab 이 있으면 표식 대신 그 모양
	public enum MapObjectMarkerShape
	{
		Diamond = 0,
		Square = 1,
		Ring = 2,
	}

	// 작은 지도와 화면 밖 화살표 범주. 연결은 2단계 (mo7, mo8)
	public enum MapObjectSignal
	{
		None = 0,
		Objective = 1,
		Reward = 2,
		Danger = 3,
		Utility = 4,
	}

	// 수락 전 보여 줄 보상 범주. 표시는 2단계 (mo8)
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
		[field: SerializeField, Tooltip("SpawnElite 대상. 비우면 사건표의 확정 상자 정예")] public Monster Monster { get; private set; }
		[field: SerializeField, Tooltip("SpawnElite 전용. 처치 시 상자 확정")] public bool GuaranteedChest { get; private set; } = true;
		[field: SerializeField, Min(1)] public int Count { get; private set; } = 1;

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

	// 오브젝트 SO 를 돌리는 쪽이 지원하는 범위. 데이터 검사와 런타임이 같은 표를 씀
	public static class MapObjectRules
	{
		public static bool IsCostSupported(MapObjectCostType costType) => costType == MapObjectCostType.None || costType == MapObjectCostType.RiskAccept;

		public static bool IsEffectSupported(MapObjectEffectType effectType) => effectType == MapObjectEffectType.SpawnElite || effectType == MapObjectEffectType.SpawnChest;
	}
}
