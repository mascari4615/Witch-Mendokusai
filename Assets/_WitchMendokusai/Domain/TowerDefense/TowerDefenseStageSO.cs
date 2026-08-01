using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) 한 스테이지의 콘텐츠 + 수치 전량 — 규칙(TowerDefenseRules) + 유닛 구성(코어/적/타워/채집건물)
	/// + 전술 프로그램 + 건설 비용 + 배치 레이아웃을 SO 인스펙터로 노출(수치 노출 룰: 하드코딩 0).
	/// TowerDefenseMatch(셸)가 이 SO 하나만 보고 스테이지를 셋업 — ArenaMatchConfig 와 동형(맵/모드/로스터가
	/// 여기선 지형/코어·적·타워·채집건물/전술·비용 3축으로 대응).
	/// </summary>
	[CreateAssetMenu(fileName = nameof(TowerDefenseStageSO), menuName = "WM/TowerDefense/TowerDefenseStageSO")]
	public class TowerDefenseStageSO : DataSO
	{
		[field: Header("_" + nameof(TowerDefenseStageSO))]
		[field: Tooltip("진행·경제 규칙 수치(웨이브 수/준비시간/자원/수입 등).")]
		[field: SerializeField] public TowerDefenseRules Rules { get; private set; }

		[field: Header("유닛 구성")]
		[field: Tooltip("수비 코어(기지) 유닛 데이터 — 파괴되면 즉시 패배.")]
		[field: SerializeField] public Unit CoreUnit { get; private set; }

		[field: Tooltip("웨이브로 스폰되는 적 유닛 데이터.")]
		[field: SerializeField] public Unit EnemyUnit { get; private set; }

		[field: Tooltip("건설 페이즈에 배치 가능한 타워 유닛 데이터.")]
		[field: SerializeField] public Unit TowerUnit { get; private set; }

		[field: Tooltip("건설 페이즈에 배치 가능한 채집건물 유닛 데이터.")]
		[field: SerializeField] public Unit HarvesterUnit { get; private set; }

		[field: Header("전술 프로그램")]
		[field: Tooltip("적 유닛의 전술(우선순위 룰 리스트) — 코어를 향해 전진·교전.")]
		[field: SerializeField] public TacticProgram EnemyTactic { get; private set; }

		[field: Tooltip("타워 유닛의 전술(우선순위 룰 리스트) — 사거리 내 적 요격.")]
		[field: SerializeField] public TacticProgram TowerTactic { get; private set; }

		[field: Header("건설 비용")]
		[field: Tooltip("타워 1기 배치 비용.")]
		[field: SerializeField, Min(0)] public int TowerCost { get; private set; }

		[field: Tooltip("채집건물 1기 배치 비용.")]
		[field: SerializeField, Min(0)] public int HarvesterCost { get; private set; }

		[field: Header("가독성 (읽히는 화면 — 수치·색 전부 노출)")]
		// 역할별 색 — 팀 2색만으로는 "이 슬라임이 무슨 건물인지" 를 못 알려준다(사용자 실증 2회).
		// 지금 단계에선 아트가 아니라 **색이 곧 정체**이므로 4역할을 서로 확실히 멀게 잡는다.
		// HUD 범례가 이 색을 그대로 읽어 화면에 설명을 띄운다(색↔이름 단일 소스).
		[field: Tooltip("코어(기지) 색 — 지켜야 할 것.")]
		[field: SerializeField] public Color CoreTint { get; private set; } = new Color(1f, 0.93f, 0.45f, 1f);

		[field: Tooltip("포탑 인형 색 — 싸우는 것.")]
		[field: SerializeField] public Color TowerTint { get; private set; } = new Color(0.45f, 0.72f, 1f, 1f);

		[field: Tooltip("채집 인형 색 — 버는 것.")]
		[field: SerializeField] public Color HarvesterTint { get; private set; } = new Color(0.42f, 0.92f, 0.68f, 1f);

		[field: Tooltip("마수(적) 색.")]
		[field: SerializeField] public Color EnemyTint { get; private set; } = new Color(1f, 0.38f, 0.36f, 1f);

		// 크기 = 배치 격자 한 칸 기준. 칸보다 크면 서로 밀치고 어디에 속한 유닛인지도 안 읽힌다
		// (사용자 실증: "칸보다 슬라임이 더 커서 슬라임끼리 밀려"). 기본값 1 = 정확히 한 칸.
		[field: Tooltip("코어 크기(칸 단위). 1 = 한 칸.")]
		[field: SerializeField, Min(0.1f)] public float CoreScale { get; private set; } = 1f;

		[field: Tooltip("포탑 인형 크기(칸 단위).")]
		[field: SerializeField, Min(0.1f)] public float TowerScale { get; private set; } = 1f;

		[field: Tooltip("채집 인형 크기(칸 단위).")]
		[field: SerializeField, Min(0.1f)] public float HarvesterScale { get; private set; } = 1f;

		[field: Tooltip("마수 크기(칸 단위).")]
		[field: SerializeField, Min(0.1f)] public float EnemyScale { get; private set; } = 1f;

		[field: Tooltip("바닥 격자 한 칸 크기 — 배치 스냅 격자와 같아야 눈으로 칸을 셀 수 있다.")]
		[field: SerializeField, Min(0.25f)] public float GroundCellSize { get; private set; } = 1f;

		[field: Header("레이아웃")]
		[field: Tooltip("코어(기지) 배치 위치 — 스테이지 root 로컬 좌표.")]
		[field: SerializeField] public Vector3 CorePosition { get; private set; }

		[field: Tooltip("적 웨이브 스폰 지점(로컬 좌표) — 웨이브 적을 이 지점들에 고르게 분산 스폰.")]
		[field: SerializeField] public Vector3[] EnemySpawnPoints { get; private set; }

		[field: Tooltip("자원 채집 노드 위치(로컬 좌표) — 채집건물은 이 지점 중 하나를 점유해야만 가동(개척 리스크 = 설계 긴장).")]
		[field: SerializeField] public Vector3[] ResourceNodePositions { get; private set; }

		[field: Tooltip("채집건물 배치가 노드를 점유로 인정하는 최대 거리 — 이 반경 밖 배치는 거절(노드 스냅 X).")]
		[field: SerializeField, Min(0.5f)] public float NodeCaptureRadius { get; private set; } = 2f;

		[field: Tooltip("지면 X 축 폭(월드 단위).")]
		[field: SerializeField, Min(2f)] public float GroundWidth { get; private set; } = 24f;

		[field: Tooltip("지면 Z 축 길이(월드 단위).")]
		[field: SerializeField, Min(2f)] public float GroundLength { get; private set; } = 24f;
	}
}
