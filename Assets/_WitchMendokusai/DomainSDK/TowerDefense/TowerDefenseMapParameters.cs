using System;
using WitchMendokusai.Numerics;
#if UNITY_5_3_OR_NEWER
// 인스펙터 속성만 진짜 Unity 것으로 되돌린다 (디자이너 UX 보존, TASK-WM-214).
using Header = UnityEngine.HeaderAttribute;
using Tooltip = UnityEngine.TooltipAttribute;
#endif

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) 절차적 맵 생성 파라미터 — 수치 전량 노출(하드코딩 0, WM 수치 노출 룰).
	/// 같은 파라미터(Seed 포함) → 항상 같은 <see cref="TowerDefenseMapLayout"/> (결정론).
	///
	/// 셀 단위 좌표계: (0,0) ~ (Width-1, Length-1). 월드는 원점 중심 XZ 평면으로 환산되고
	/// 셀 중심이 곧 배치 좌표 — TowerDefensePlacement 의 스냅 규약(Floor(x/cell)*cell + half)과 동일.
	/// </summary>
	[Serializable]
	public struct TowerDefenseMapParameters
	{
		[Header("판 크기")]
		public int Seed;
		public int Width;      // 셀 개수 (X)
		public int Length;     // 셀 개수 (Z)
		public float CellSize; // 셀 1칸의 월드 크기 — 배치 그리드와 반드시 일치.

		[Header("적 스폰")]
		public int SpawnPointCount;  // 외곽 링에 각도 균등 분산되는 스폰 지점 수.
		public float SpawnRingInset; // 맵 반경 대비 안쪽으로 당길 셀 수 — 0 이면 딱 가장자리.
		public float SpawnAngleJitter; // 균등 각도에서 흔들 최대 각(도). 0 = 완전 대칭(지루함).

		[Header("자원 노드 = 개척 대상")]
		public int ResourceNodeCount;
		public float NodeMinSpacing;      // 노드끼리 최소 거리(셀). 뭉치면 방어선 한 곳으로 퉁쳐져 개척 긴장 0.
		public float NodeMinCoreDistance; // 코어에서 최소 이만큼 떨어짐 — 코어 옆 스택 = 개척 아님.
		public float NodeEdgeMargin;      // 판 가장자리에서 최소 이만큼 안쪽(셀). 구석 노드는 방어선이 맵 밖에 걸려 지킬 각이 안 나온다.
		public float NodeAngularSpread;   // 코어 기준 노드 사이 최소 각도(도). 한쪽으로 쏠리면 "어느 방향으로 넓힐까" 선택이 죽는다.
		public float NearIncomeMultiplier; // 코어에 가장 가까운 노드의 수입 배수.
		public float FarIncomeMultiplier;  // 코어에서 가장 먼 노드의 수입 배수 — 멀수록 벌이가 커야 나갈 이유가 생김.
		public float InnerTierRatio;       // 정규화 거리 이 값 이하 = Inner 티어(안전권).

		[Header("암반 능선 = 초크포인트")]
		// Voronoi *경계*(두 최근접 site 등거리 지대)를 암반으로 굳혀 능선망을 만든다. 덩어리가 아니라
		// 능선이라야 자연스러운 통로·길목이 생기고, 그 길목이 곧 타워 놓을 자리 = TD 의 재미(VoronoiNode 개념 확장).
		public int RockSiteCount;   // Voronoi site 수 — 적을수록 구획이 크고 능선이 굵게 갈린다.
		public float RidgeWidth;    // 경계로 인정할 등거리 허용폭(셀). 클수록 벽이 두꺼움.
		public float ObstacleDensity; // 능선 후보 중 실제로 암반이 되는 비율(0~1) — 낮추면 벽이 숭숭 뚫림.

		[Header("정리 반경 — 여긴 무조건 비운다")]
		public float CoreClearRadius;
		public float SpawnClearRadius;
		public float NodeClearRadius;

		/// <summary> 파라미터 기본값 — 40x40 판 기준의 "말이 되는" 시작점. </summary>
		public static TowerDefenseMapParameters Default => new TowerDefenseMapParameters
		{
			Seed = 0,
			Width = 40,
			Length = 40,
			CellSize = 1f,
			SpawnPointCount = 3,
			SpawnRingInset = 1f,
			SpawnAngleJitter = 18f,
			ResourceNodeCount = 4,
			NodeMinSpacing = 6f,
			NodeMinCoreDistance = 7f,
			NodeEdgeMargin = 3f,
			NodeAngularSpread = 50f,
			NearIncomeMultiplier = 1f,
			FarIncomeMultiplier = 2.5f,
			InnerTierRatio = 0.55f,
			RockSiteCount = 10,
			RidgeWidth = 1.4f,
			ObstacleDensity = 0.75f,
			CoreClearRadius = 4f,
			SpawnClearRadius = 2.5f,
			NodeClearRadius = 2f,
		};

		/// <summary>
		/// 생성기가 실제로 쓸 수 있는 범위로 조인 사본. 잘못된 입력에 대해 예외 대신 안전한 판을 낸다
		/// (맵 생성은 런타임 루프 한가운데라 FastFail 이 곧 매치 붕괴 — 여기선 clamp 가 근본).
		/// </summary>
		public TowerDefenseMapParameters Normalized()
		{
			TowerDefenseMapParameters normalized = this;

			normalized.Width = Mathf.Max(MIN_SIDE, Width);
			normalized.Length = Mathf.Max(MIN_SIDE, Length);
			normalized.CellSize = Mathf.Max(MIN_CELL_SIZE, CellSize);

			normalized.SpawnPointCount = Mathf.Max(1, SpawnPointCount);
			normalized.SpawnRingInset = Mathf.Max(0f, SpawnRingInset);
			normalized.SpawnAngleJitter = Mathf.Clamp(SpawnAngleJitter, 0f, MAX_ANGLE_JITTER);

			normalized.ResourceNodeCount = Mathf.Max(0, ResourceNodeCount);
			normalized.NodeMinSpacing = Mathf.Max(1f, NodeMinSpacing);
			normalized.NodeMinCoreDistance = Mathf.Max(1f, NodeMinCoreDistance);
			normalized.NodeEdgeMargin = Mathf.Max(0f, NodeEdgeMargin);
			normalized.NodeAngularSpread = Mathf.Clamp(NodeAngularSpread, 0f, MAX_ANGLE_JITTER);
			normalized.NearIncomeMultiplier = Mathf.Max(0f, NearIncomeMultiplier);
			normalized.FarIncomeMultiplier = Mathf.Max(normalized.NearIncomeMultiplier, FarIncomeMultiplier);
			normalized.InnerTierRatio = Mathf.Clamp01(InnerTierRatio);

			normalized.RockSiteCount = Mathf.Max(0, RockSiteCount);
			normalized.RidgeWidth = Mathf.Max(0f, RidgeWidth);
			normalized.ObstacleDensity = Mathf.Clamp01(ObstacleDensity);

			normalized.CoreClearRadius = Mathf.Max(0f, CoreClearRadius);
			normalized.SpawnClearRadius = Mathf.Max(0f, SpawnClearRadius);
			normalized.NodeClearRadius = Mathf.Max(0f, NodeClearRadius);

			return normalized;
		}

		private const int MIN_SIDE = 8;
		private const float MIN_CELL_SIZE = 0.1f;
		private const float MAX_ANGLE_JITTER = 180f;
	}
}
