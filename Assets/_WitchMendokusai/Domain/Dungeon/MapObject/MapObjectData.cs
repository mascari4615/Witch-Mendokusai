using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 생존 판 맵 오브젝트 하나. 자산 하나로 추가, 자산 삭제로 제거. 배치는 MapObjectBudget, 동작은 Effects
	// 정본 memo wm/design/map-objects-20261119 SO 필드표
	[CreateAssetMenu(fileName = nameof(MapObjectData), menuName = "WM/Variable/" + nameof(MapObjectData))]
	public class MapObjectData : ScriptableObject
	{
		[field: Header("안내")]
		[field: SerializeField, Tooltip("상호작용 표시 이름")] public string DisplayName { get; private set; } = string.Empty;
		[field: SerializeField, Tooltip("상호작용 표시 이름 옆 흐린 설명")] public string Role { get; private set; } = string.Empty;
		[field: SerializeField] public MapObjectCategory Category { get; private set; }

		[field: Header("표시")]
		[field: SerializeField, Tooltip("저폴리 외형 (제단, 기둥, 샘). 비우면 바닥 선 표식만")] public MapObjectModel Model { get; private set; }
		[field: SerializeField, Tooltip("바닥 선 표식. 외형이 있으면 범위 표시 (머무름 원) 로만 켬")] public bool ShowMarker { get; private set; } = true;
		[field: SerializeField] public MapObjectMarkerShape MarkerShape { get; private set; }
		[field: SerializeField] public Color MarkerColor { get; private set; } = Color.yellow;
		[field: SerializeField, Tooltip("표식 반경 (m). 0 이면 상호작용 거리"), Min(0f)] public float MarkerRadius { get; private set; }
		[field: SerializeField, Tooltip("표식 선 폭 (m)"), Min(0.01f)] public float MarkerWidth { get; private set; } = 0.2f;
		[field: SerializeField] public MapObjectSignal Signal { get; private set; }
		[field: SerializeField] public MapObjectRewardPreview RewardPreview { get; private set; }

		[field: Header("상호작용")]
		[field: SerializeField] public MapObjectCostType CostType { get; private set; }
		[field: SerializeField, Tooltip("비용 값. 체력 % 는 0~100, 재화는 개수"), Min(0f)] public float CostValue { get; private set; }
		[field: SerializeField, Tooltip("같은 오브젝트 재사용마다 비용 배율"), Min(1f)] public float CostGrowth { get; private set; } = 1f;
		[field: SerializeField, Tooltip("사용 횟수. 0 이면 무한"), Min(0)] public int Uses { get; private set; } = 1;
		[field: SerializeField] public List<MapObjectEffect> Effects { get; private set; } = new();

		[field: Header("머무름 (구역은 표식 원)")]
		[field: SerializeField, Tooltip("표식 반경 안에 머물 시간 (초). 0 이면 머무름 없음, 효과는 상호작용 즉시"), Min(0f)] public float HoldSeconds { get; private set; }
		[field: SerializeField, Tooltip("구역 밖일 때 진행")] public MapObjectHoldOutside HoldOutside { get; private set; }
		[field: SerializeField, Tooltip("Decay 일 때 밖에서 초당 잃는 진행 (초)"), Min(0f)] public float HoldDecayPerSecond { get; private set; } = 1f;
		[field: SerializeField, Tooltip("시작부터 실패까지 (초). 0 이면 제한 없음"), Min(0f)] public float HoldTimeLimit { get; private set; }

		[field: Header("배치")]
		[field: SerializeField, Tooltip("배치 예산에서 가격"), Min(0)] public int PlacementCost { get; private set; } = 10;
		[field: SerializeField, Tooltip("예산 뽑기 가중치. 0 이면 최소 개수만"), Min(0f)] public float Weight { get; private set; } = 1f;
		[field: SerializeField, Tooltip("판당 개수 (최소, 최대). 최소는 예산이 되면 먼저 채움")] public Vector2Int CountRange { get; private set; } = new(0, 1);
		[field: SerializeField, Tooltip("등장 시각 범위 (판 경과 초). 둘 다 0 이면 판 시작")] public Vector2 SpawnTime { get; private set; }
		[field: SerializeField, Tooltip("주인공과 거리 범위 (m)")] public Vector2 PlayerDistance { get; private set; } = new(11f, 11f);
		[field: SerializeField, Tooltip("다른 맵 오브젝트와 최소 간격 (m)"), Min(0f)] public float MinSpacing { get; private set; } = 6f;
		[field: SerializeField, Tooltip("주인공 자리에서 걸어갈 수 있는 자리만")] public bool RequireRoute { get; private set; } = true;

		public float ResolveMarkerRadius(float interactionDistance) => MarkerRadius > 0f ? MarkerRadius : interactionDistance;

		public bool HasHold => HoldSeconds > 0f;

		// 판 시작 전에 부르는 데이터 검사. 오류 문구를 모아 돌려줌
		public void Validate(List<string> errors)
		{
			if (string.IsNullOrWhiteSpace(DisplayName))
				errors.Add($"{name}: {nameof(DisplayName)} 비어 있음");
			if (MapObjectRules.IsCostSupported(CostType) == false)
				errors.Add($"{name}: 비용 {CostType} 미지원");
			if (Effects.Count == 0)
				errors.Add($"{name}: 효과 없음");
			foreach (MapObjectEffect effect in Effects)
			{
				if (effect == null)
				{
					errors.Add($"{name}: 빈 효과 칸");
					continue;
				}
				if (MapObjectRules.IsEffectSupported(effect.Type) == false)
					errors.Add($"{name}: 효과 {effect.Type} 미지원");
				if (effect.Count < 1)
					errors.Add($"{name}: 효과 {effect.Type} 개수 {effect.Count}");
				if (effect.Type == MapObjectEffectType.Heal && effect.Value <= 0f)
					errors.Add($"{name}: 회복 효과 값 {effect.Value}");
				if (effect.FailPercent < 0f || effect.FailPercent >= 100f)
					errors.Add($"{name}: 효과 {effect.Type} 꽝 확률 {effect.FailPercent}");
				if (effect.Timing == MapObjectEffectTiming.OnHoldComplete && HasHold == false)
					errors.Add($"{name}: 효과 {effect.Type} 이 머무름 완료 시점인데 {nameof(HoldSeconds)} 0");
			}
			if ((CostType == MapObjectCostType.HealthPercent || CostType == MapObjectCostType.RunNyang) && CostValue <= 0f)
				errors.Add($"{name}: 비용 {CostType} 값 {CostValue}");
			if (CostType == MapObjectCostType.HealthPercent && CostValue >= 100f)
				errors.Add($"{name}: 체력 비용 {CostValue}% 는 낼 수 없음");
			if (HasHold && HoldTimeLimit > 0f && HoldTimeLimit <= HoldSeconds)
				errors.Add($"{name}: {nameof(HoldTimeLimit)} {HoldTimeLimit} 이 {nameof(HoldSeconds)} {HoldSeconds} 이하");
			if (HasHold && Effects.Exists(effect => effect != null && effect.Timing == MapObjectEffectTiming.OnHoldComplete) == false)
				errors.Add($"{name}: 머무름이 있는데 완료 효과 없음");
			if (CostType == MapObjectCostType.HealthPercent && CostGrowth > 1f)
			{
				// 마지막 사용 비용까지 100% 미만이어야 낼 수 있음. 무한 사용이면 언젠가 넘음
				if (Uses == 0)
					errors.Add($"{name}: 체력 비용 배율 {CostGrowth} 에 무한 사용");
				else if (CostValue * MapObjectRules.CostMultiplier(CostGrowth, Uses - 1) >= 100f)
					errors.Add($"{name}: 체력 비용이 {Uses} 번째 사용에서 100% 이상");
			}
			if (HasHold && ShowMarker == false)
				errors.Add($"{name}: 머무름 진행 호는 선 표식에 그려서 {nameof(ShowMarker)} 를 켜야 함");
			if (Model == null && ShowMarker == false)
				errors.Add($"{name}: 외형도 선 표식도 없음");
			if (Model != null)
				Model.Validate(errors);
			if (CountRange.x < 0 || CountRange.y < 1 || CountRange.x > CountRange.y)
				errors.Add($"{name}: {nameof(CountRange)} 범위 오류 {CountRange}");
			if (SpawnTime.x < 0f || SpawnTime.y < SpawnTime.x)
				errors.Add($"{name}: {nameof(SpawnTime)} 범위 오류 {SpawnTime}");
			if (PlayerDistance.x <= 0f || PlayerDistance.y < PlayerDistance.x)
				errors.Add($"{name}: {nameof(PlayerDistance)} 범위 오류 {PlayerDistance}");
			if (Weight <= 0f && CountRange.x == 0)
				errors.Add($"{name}: 가중치 0, 최소 개수 0 이라 나올 수 없음");
		}
	}
}
