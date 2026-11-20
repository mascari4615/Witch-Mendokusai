using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 모든 맵 오브젝트 공용 배치와 표식 설정. Resources 의 MapObjectSettings 하나
	[CreateAssetMenu(fileName = nameof(MapObjectSettings), menuName = "WM/Variable/" + nameof(MapObjectSettings))]
	public class MapObjectSettings : ScriptableObject
	{
		public const string RESOURCE_FOLDER = "MapObjects";

		[field: Header("지면과 장애물")]
		[field: SerializeField] public LayerMask GroundMask { get; private set; } = 1;
		[field: SerializeField] public LayerMask ObstacleMask { get; private set; } = 1;
		[field: SerializeField, Tooltip("지면 탐색 광선 시작 높이 (m)"), Min(0.01f)] public float GroundProbeHeight { get; private set; } = 15f;
		[field: SerializeField, Tooltip("표식을 지면 위로 띄우는 높이 (m)"), Min(0f)] public float GroundLift { get; private set; } = 0.02f;
		[field: SerializeField, Tooltip("자리 하나에 시도할 후보 수"), Min(1)] public int PlacementAttempts { get; private set; } = 8;
		[field: SerializeField, Tooltip("배치 실패 시 다시 시도할 판 틱 수 (0.1초 간격). 다 쓰면 그 오브젝트 포기"), Min(1)] public int PlacementRounds { get; private set; } = 50;

		[field: Header("걸어갈 길 검사")]
		[field: SerializeField, Tooltip("경로 표본 간격 (m)"), Min(0.01f)] public float RouteSampleSpacing { get; private set; } = 0.2f;
		[field: SerializeField, Tooltip("경로 표본 상한"), Min(1)] public int RouteMaxSamples { get; private set; } = 128;
		[field: SerializeField, Tooltip("경로 지면 높이 허용 차이 (m)"), Min(0f)] public float RouteGroundDeviation { get; private set; } = 0.2f;

		[field: Header("표식")]
		[field: SerializeField, Tooltip("선 표식 공유 재질")] public Material MarkerMaterial { get; private set; }
		[field: SerializeField, Tooltip("원 표식 꼭짓점 수"), Min(8)] public int RingSegments { get; private set; } = 32;

		[field: Header("효과")]
		[field: SerializeField, Tooltip("사건표가 없는 던전 (진열장) 의 정예 설정")] public EliteProfile FallbackElite { get; private set; } = new();
		[field: SerializeField, Tooltip("효과에 몬스터가 비어 있고 사건표에 확정 상자 정예도 없을 때 쓸 정예")] public Monster FallbackEliteMonster { get; private set; }

		[field: Header("개발 도구")]
		[field: SerializeField, Tooltip("mapobj near 배치 거리 (m)"), Min(0f)] public float NearDistance { get; private set; } = 3f;
		[field: SerializeField, Tooltip("판 밖에서 mapobj budget 이 쓸 예산")] public MapObjectBudget DefaultBudget { get; private set; }

		private static MapObjectSettings loaded;

		public static MapObjectSettings Load()
		{
			if (loaded == null)
				loaded = Resources.Load<MapObjectSettings>(nameof(MapObjectSettings));
			if (loaded == null)
				throw new System.InvalidOperationException($"Resources/{nameof(MapObjectSettings)} 없음");
			return loaded;
		}

		// Resources/MapObjects 의 모든 오브젝트 자산. 개발자 창 목록과 데이터 검사
		public static MapObjectData[] LoadAllData() => Resources.LoadAll<MapObjectData>(RESOURCE_FOLDER);

		public void Validate(List<string> errors)
		{
			if (MarkerMaterial == null)
				errors.Add($"{name}: {nameof(MarkerMaterial)} 미연결");
			if (GroundMask.value == 0)
				errors.Add($"{name}: {nameof(GroundMask)} 비어 있음");
			if (ObstacleMask.value == 0)
				errors.Add($"{name}: {nameof(ObstacleMask)} 비어 있음");
			if (FallbackEliteMonster == null)
				errors.Add($"{name}: {nameof(FallbackEliteMonster)} 미연결");
		}
	}
}
