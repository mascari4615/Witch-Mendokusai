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
		[field: SerializeField, Tooltip("비우면 선 표식")] public GameObject Prefab { get; private set; }
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

		[field: Header("배치")]
		[field: SerializeField, Tooltip("배치 예산에서 가격"), Min(0)] public int PlacementCost { get; private set; } = 10;
		[field: SerializeField, Tooltip("예산 뽑기 가중치. 0 이면 최소 개수만"), Min(0f)] public float Weight { get; private set; } = 1f;
		[field: SerializeField, Tooltip("판당 개수 (최소, 최대). 최소는 예산이 되면 먼저 채움")] public Vector2Int CountRange { get; private set; } = new(0, 1);
		[field: SerializeField, Tooltip("등장 시각 범위 (판 경과 초). 둘 다 0 이면 판 시작")] public Vector2 SpawnTime { get; private set; }
		[field: SerializeField, Tooltip("주인공과 거리 범위 (m)")] public Vector2 PlayerDistance { get; private set; } = new(11f, 11f);
		[field: SerializeField, Tooltip("다른 맵 오브젝트와 최소 간격 (m)"), Min(0f)] public float MinSpacing { get; private set; } = 6f;
		[field: SerializeField, Tooltip("주인공 자리에서 걸어갈 수 있는 자리만")] public bool RequireRoute { get; private set; } = true;

		public float ResolveMarkerRadius(float interactionDistance) => MarkerRadius > 0f ? MarkerRadius : interactionDistance;

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
			}
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
