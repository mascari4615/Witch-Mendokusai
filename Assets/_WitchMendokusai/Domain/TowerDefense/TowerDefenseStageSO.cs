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

		[field: Tooltip("웨이브 자동 진행 기본값 — 켜짐이면 건설 시간이 다하면 알아서 몰려온다. 플레이 중 화면에서 바꿀 수 있다.")]
		[field: SerializeField] public bool AutoAdvanceWavesDefault { get; private set; } = true;

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
		[field: Tooltip("포탑 종류 — 비었으면 TowerTactic/TowerCost 를 쓰는 기존 단일 포탑. 종류가 있어야 예고→배치 고리가 닫힌다.")]
		[field: SerializeField] public TowerDefenseTowerArchetype[] TowerArchetypes { get; private set; }

		[field: Tooltip("마수 종류 — 비었으면 EnemyUnit 을 그대로 한 종류로 쓴다. 종류가 섞여야 웨이브마다 판단이 달라진다.")]
		[field: SerializeField] public TowerDefenseEnemyArchetype[] EnemyArchetypes { get; private set; }

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

		[field: Header("카메라 조작")]
		[field: Tooltip("개척 카메라 이동 속도(WASD) — CityViewCameraController 와 같은 CameraMove 축을 쓴다.")]
		[field: SerializeField, Min(1f)] public float CameraPanSpeed { get; private set; } = 24f;

		[field: Tooltip("카메라가 스테이지 중심에서 벗어날 수 있는 최대 거리 — 개척지를 잃어버리지 않게 가둔다.")]
		[field: SerializeField, Min(1f)] public float CameraPanLimit { get; private set; } = 26f;

		[field: Tooltip("좌우 회전 속도(Q/E) — 도시 부감 카메라와 같은 축.")]
		[field: SerializeField, Min(0f)] public float CameraYawSpeed { get; private set; } = 90f;

		[field: Tooltip("내려다보는 고정 각도(deg, + = 아래).")]
		[field: SerializeField, Range(20f, 89f)] public float CameraPitch { get; private set; } = 60f;

		[field: Tooltip("휠 줌 하한 높이 — 가까이 = 확대.")]
		[field: SerializeField, Min(1f)] public float CameraMinHeight { get; private set; } = 10f;

		[field: Tooltip("휠 줌 상한 높이 — 멀리 = 축소.")]
		[field: SerializeField, Min(1f)] public float CameraMaxHeight { get; private set; } = 55f;

		[field: Tooltip("진입·재시작 시 되돌아갈 시작 높이.")]
		[field: SerializeField, Min(1f)] public float CameraInitialHeight { get; private set; } = 30f;

		[field: Tooltip("휠 **한 칸당** 높이 변화량. 플랫폼별 raw 델타 차이는 리그가 흡수한다.")]
		[field: SerializeField, Min(0.1f)] public float CameraZoomSpeed { get; private set; } = 6f;

		[field: Header("판 생성 — 매 판 다른 지형")]
		[field: Tooltip("켜면 암반 능선·자원 노드·스폰 지점을 매치마다 새로 만든다. 끄면 아래 고정 레이아웃을 쓴다.")]
		[field: SerializeField] public bool UseProceduralMap { get; private set; } = true;

		[field: Tooltip("켜면 매치마다 씨앗을 새로 뽑는다(매 판 다른 판). 끄면 아래 파라미터의 Seed 로 늘 같은 판.")]
		[field: SerializeField] public bool RandomizeSeedEachMatch { get; private set; } = true;

		[field: Tooltip("판 생성 파라미터 — 크기·암반 능선 밀도·노드 배치 규칙·수입 배수 전량.")]
		[field: SerializeField] public TowerDefenseMapParameters MapParameters { get; private set; } = TowerDefenseMapParameters.Default;

		[field: Header("레이아웃 (고정 판 — UseProceduralMap 끌 때만 쓰임)")]
		[field: Tooltip("코어(기지) 배치 위치 — 스테이지 root 로컬 좌표.")]
		[field: SerializeField] public Vector3 CorePosition { get; private set; }

		[field: Tooltip("적 웨이브 스폰 지점(로컬 좌표) — 웨이브 적을 이 지점들에 고르게 분산 스폰.")]
		[field: SerializeField] public Vector3[] EnemySpawnPoints { get; private set; }

		[field: Tooltip("자원 채집 노드 위치(로컬 좌표) — 채집건물은 이 지점 중 하나를 점유해야만 가동(개척 리스크 = 설계 긴장).")]
		[field: SerializeField] public Vector3[] ResourceNodePositions { get; private set; }

		[field: Tooltip("같은 출현 지점에 여러 마수가 나올 때 서로 벌리는 간격 — 0이면 정확히 겹쳐 스폰돼 물리가 서로를 튕겨낸다(맵 밖으로 날아감).")]
		[field: SerializeField, Min(0f)] public float EnemySpawnSpread { get; private set; } = 1.2f;

		[field: Tooltip("무대 밖 판정 여유 — 지면 경계에서 이만큼 더 나가면 이탈로 본다.")]
		[field: SerializeField, Min(1f)] public float StageBoundsMargin { get; private set; } = 12f;

		[field: Tooltip("이 깊이보다 아래로 떨어지면 이탈로 본다(지면 통과·추락).")]
		[field: SerializeField] public float StageFloorDepth { get; private set; } = -5f;

		[field: Tooltip("채집건물 배치가 노드를 점유로 인정하는 최대 거리 — 이 반경 밖 배치는 거절(노드 스냅 X).")]
		[field: SerializeField, Min(0.5f)] public float NodeCaptureRadius { get; private set; } = 2f;

		[field: Tooltip("지면 X 축 폭(월드 단위).")]
		[field: SerializeField, Min(2f)] public float GroundWidth { get; private set; } = 24f;

		[field: Tooltip("지면 Z 축 길이(월드 단위).")]
		[field: SerializeField, Min(2f)] public float GroundLength { get; private set; } = 24f;
	}
}
