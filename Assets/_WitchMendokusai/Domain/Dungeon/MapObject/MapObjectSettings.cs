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

		[field: Header("등장 빈도")]
		[field: SerializeField, Tooltip("앞 오브젝트와 등장 시각 최소 간격 (판 경과 초). 판 시작에 몰려 보이지 않게"), Min(0f)] public float SpawnGapSeconds { get; private set; } = 75f;
		[field: SerializeField, Tooltip("같은 종류 오브젝트끼리 최소 거리 (m). 이미 쓴 것도 셈 (같은 자리 근처 반복 방지)"), Min(0f)] public float SameKindSpacing { get; private set; } = 40f;
		[field: SerializeField, Tooltip("예산 뽑기에서 바로 앞과 같은 종류를 피함")] public bool AvoidSameKindInRow { get; private set; } = true;

		public MapObjectPacing Pacing => new(SpawnGapSeconds, AvoidSameKindInRow);

		[field: Header("걸어갈 길 검사")]
		[field: SerializeField, Tooltip("경로 표본 간격 (m)"), Min(0.01f)] public float RouteSampleSpacing { get; private set; } = 0.2f;
		[field: SerializeField, Tooltip("경로 표본 상한"), Min(1)] public int RouteMaxSamples { get; private set; } = 128;
		[field: SerializeField, Tooltip("경로 지면 높이 허용 차이 (m)"), Min(0f)] public float RouteGroundDeviation { get; private set; } = 0.2f;

		[field: Header("표식")]
		[field: SerializeField, Tooltip("선 표식 공유 재질. 외형의 빛 부품도 이 재질 (정점 색)")] public Material MarkerMaterial { get; private set; }
		[field: SerializeField, Tooltip("외형의 돌 부품 재질. 지면 바위와 같은 텍스처")] public Material StoneMaterial { get; private set; }
		[field: SerializeField, Tooltip("외형 텍스처 밀도. 1m 당 텍스처 반복 수"), Min(0.01f)] public float ModelUvPerMeter { get; private set; } = 0.5f;
		[field: SerializeField, Tooltip("빛 부품 면마다 밝기 차이 (0 이면 한 색). 표식 재질은 조명이 없어 각진 결을 정점 색으로"), Range(0f, 1f)] public float ModelGlowFacetShade { get; private set; } = 0.35f;
		[field: SerializeField, Tooltip("원 표식 꼭짓점 수"), Min(8)] public int RingSegments { get; private set; } = 32;

		[field: Header("머무름 진행 호")]
		[field: SerializeField, Tooltip("표식 반경 대비 진행 호 반경"), Range(0.1f, 1f)] public float HoldProgressRadiusRatio { get; private set; } = 0.82f;
		[field: SerializeField, Tooltip("표식 선 폭 대비 진행 호 폭"), Min(0.1f)] public float HoldProgressWidthRatio { get; private set; } = 1.6f;
		[field: SerializeField] public Color HoldProgressColor { get; private set; } = Color.white;

		[field: Header("발견 신호 (작은 지도, 화면 밖 화살표)")]
		[field: SerializeField, Tooltip("이 거리 안에 들어온 오브젝트는 발견됨 (m). 스캐너는 거리 무관"), Min(0f)] public float DiscoverDistance { get; private set; } = 10f;
		[field: SerializeField, Tooltip("작은 지도 점 크기 (px)"), Min(1f)] public float MinimapDotSize { get; private set; } = 9f;
		[field: SerializeField, Tooltip("화면 밖 화살표 크기 배율"), Min(0.1f)] public float OffscreenScale { get; private set; } = 0.95f;

		[field: Header("상호작용 표시 (보상 예고, 비용)")]
		[field: SerializeField, Tooltip("보상 범주별 아이콘, 글자, 색. None 은 표시 안 함")] public List<MapObjectRewardPreviewStyle> RewardPreviews { get; private set; } = new();
		[field: SerializeField, Tooltip("{0} = 체력 비용")] public string HealthCostFormat { get; private set; } = "체력 {0}";
		[field: SerializeField, Tooltip("{0} = 냥 비용, {1} = 판 안 냥")] public string NyangCostFormat { get; private set; } = "{0}냥 (보유 {1})";
		[field: SerializeField] public string RiskCostText { get; private set; } = "위험 수락";
		[field: SerializeField, Tooltip("{0} = 진행 초, {1} = 필요 초")] public string HoldProgressFormat { get; private set; } = "머무름 {0}/{1}초";
		[field: SerializeField, Tooltip("작은 지도 아래 이번 판 냥 줄. {0} = 판 안 냥")] public string RunNyangHudFormat { get; private set; } = "이번 판 {0}냥";
		[field: SerializeField] public Color CostAffordableColor { get; private set; } = new(1f, 0.92f, 0.62f, 1f);
		[field: SerializeField] public Color CostShortColor { get; private set; } = new(1f, 0.42f, 0.42f, 1f);

		[field: Header("반응 글자")]
		[field: SerializeField, Tooltip("오브젝트 위 반응 글자 높이 (m)"), Min(0f)] public float TextHeight { get; private set; } = 1.6f;
		[field: SerializeField] public string ShortNyangText { get; private set; } = "냥이 모자람";
		[field: SerializeField] public string ShortHealthText { get; private set; } = "체력이 모자람";
		[field: SerializeField] public string HealthFullText { get; private set; } = "체력이 가득";
		[field: SerializeField] public string BusyText { get; private set; } = "지금은 못 씀";
		[field: SerializeField] public string LuckFailText { get; private set; } = "꽝";
		[field: SerializeField] public string LuckWinText { get; private set; } = "당첨";
		[field: SerializeField] public string HoldStartText { get; private set; } = "원 안에 머물기";
		[field: SerializeField] public string HoldCompleteText { get; private set; } = "완료";
		[field: SerializeField, Tooltip("갇힌 사람을 구했을 때")] public string RescueText { get; private set; } = "마을로 보냈습니다";
		[field: SerializeField] public string HoldFailText { get; private set; } = "시간 초과";
		[field: SerializeField, Tooltip("{0} = 드러난 수")] public string RevealFormat { get; private set; } = "맵 오브젝트 {0}개 표시";

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

		public bool TryGetRewardPreview(MapObjectRewardPreview preview, out MapObjectRewardPreviewStyle style)
		{
			foreach (MapObjectRewardPreviewStyle entry in RewardPreviews)
			{
				if (entry.Preview == preview)
				{
					style = entry;
					return true;
				}
			}
			style = null;
			return false;
		}

		// Resources/MapObjects 의 모든 오브젝트 자산. 개발자 창 목록과 데이터 검사
		public static MapObjectData[] LoadAllData() => Resources.LoadAll<MapObjectData>(RESOURCE_FOLDER);

		public void Validate(List<string> errors)
		{
			if (MarkerMaterial == null)
				errors.Add($"{name}: {nameof(MarkerMaterial)} 미연결");
			if (StoneMaterial == null)
				errors.Add($"{name}: {nameof(StoneMaterial)} 미연결");
			if (GroundMask.value == 0)
				errors.Add($"{name}: {nameof(GroundMask)} 비어 있음");
			if (ObstacleMask.value == 0)
				errors.Add($"{name}: {nameof(ObstacleMask)} 비어 있음");
			if (FallbackEliteMonster == null)
				errors.Add($"{name}: {nameof(FallbackEliteMonster)} 미연결");
			foreach (MapObjectRewardPreview preview in System.Enum.GetValues(typeof(MapObjectRewardPreview)))
			{
				if (preview == MapObjectRewardPreview.None)
					continue;
				if (TryGetRewardPreview(preview, out MapObjectRewardPreviewStyle style) == false || string.IsNullOrWhiteSpace(style.Label))
					errors.Add($"{name}: 보상 예고 {preview} 글자 없음");
				else if (style.Color.a <= 0f)
					errors.Add($"{name}: 보상 예고 {preview} 색이 투명 (목록에 새로 넣은 칸은 알파 0)");
				else if (style.Icon == null)
					errors.Add($"{name}: 보상 예고 {preview} 아이콘 없음");
			}
			// 자리 표시 개수가 틀리면 상호작용 표시가 매 프레임 예외. 입장 전에 잡음
			CheckFormat(errors, nameof(HealthCostFormat), HealthCostFormat, 1);
			CheckFormat(errors, nameof(NyangCostFormat), NyangCostFormat, 2);
			CheckFormat(errors, nameof(HoldProgressFormat), HoldProgressFormat, 2);
			CheckFormat(errors, nameof(RevealFormat), RevealFormat, 1);
			CheckFormat(errors, nameof(RunNyangHudFormat), RunNyangHudFormat, 1);
			if (DefaultBudget == null)
				errors.Add($"{name}: {nameof(DefaultBudget)} 미연결 (예산 없는 던전의 굴림 시드)");
		}

		// 판당 둘 이상 나오는 종류는 주인공이 제자리에 있어도 둘째 자리가 있어야 함 (거리 범위 지름보다 같은 종류 거리가 짧게)
		public void ValidateBudget(MapObjectBudget budget, List<string> errors)
		{
			foreach (MapObjectData candidate in budget.Candidates)
			{
				if (candidate == null || candidate.CountRange.y < 2)
					continue;
				if (SameKindSpacing >= candidate.PlayerDistance.y * 2f)
					errors.Add($"{name}: {nameof(SameKindSpacing)} {SameKindSpacing} 이 {candidate.name} 주인공 거리 지름 {candidate.PlayerDistance.y * 2f} 이상이라 둘째가 못 놓일 수 있음");
			}
		}

		private void CheckFormat(List<string> errors, string fieldName, string format, int argumentCount)
		{
			try
			{
				string.Format(format, new object[argumentCount]);
			}
			catch (System.FormatException)
			{
				errors.Add($"{name}: {fieldName} '{format}' 자리 표시는 {argumentCount}개까지");
			}
		}
	}

	[System.Serializable]
	public class MapObjectRewardPreviewStyle
	{
		[field: SerializeField] public MapObjectRewardPreview Preview { get; private set; }
		[field: SerializeField] public string Label { get; private set; } = string.Empty;
		[field: SerializeField, Tooltip("글자 앞 아이콘. 공개 라이선스 세트 (game-icons.net) 에서")] public Sprite Icon { get; private set; }
		[field: SerializeField] public Color Color { get; private set; } = Color.white;
	}
}
