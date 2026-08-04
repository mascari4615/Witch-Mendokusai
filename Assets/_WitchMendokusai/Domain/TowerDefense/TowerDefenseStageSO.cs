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

		[field: Tooltip("첫 웨이브는 사람이 부를 때까지 오지 않는다 — 판이 매번 새로 생성되므로 시작하자마자 시계가 돌면 지형을 볼 시간이 없다.")]
		[field: SerializeField] public bool ManualFirstWave { get; private set; } = true;

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

		[field: Header("전초기지 — 넓히면 지킬 곳이 는다")]
		[field: Tooltip("전초기지 1기에 드는 정수. 비싸야 「넓힐까 말까」가 진짜 결정이 된다.")]
		[field: SerializeField, Min(0)] public int OutpostEssenceCost { get; private set; } = 10;

		[field: Tooltip("전초기지가 밝히는 시야 반경.")]
		[field: SerializeField, Min(0f)] public float OutpostVisionRadius { get; private set; } = 10f;

		[field: Tooltip("전초기지 색.")]
		[field: SerializeField] public Color OutpostTint { get; private set; } = new Color(1f, 0.9f, 0.55f, 1f);

		[field: Header("보급선 — 이어져야 들어온다")]
		[field: Tooltip("건물 하나가 다음 건물까지 보급을 잇는 거리(칸). 짧을수록 촘촘히 이어야 하고 그만큼 지킬 게 는다.")]
		// 7 은 44칸 판에서 너무 짧았다 — 바깥 노드가 *어떤 사슬로도* 안 닿아 정수가 영영 0 이 됐다.
		// 판이 200칸이 되면서 12 도 같은 병에 걸렸다: 코어 주변 6% 만 덮어 **가장 가까운 자원조차** 못 잡고,
		// 채집이 0기인 채로 판이 돌았다(라이브 실측 — 거절 사유가 전부 「보급이 닿는 곳에만」이었다).
		// 24 = 코어에서 가까운 광맥 몇 곳이 처음부터 손에 닿는 거리. 넓히려면 여전히 이어야 한다.
		[field: SerializeField, Min(1f)] public float SupplyReach { get; private set; } = 24f;

		[field: Tooltip("판 짧은 변의 이 비율만큼도 보급이 닿는다(0.12 = 12%). 위 절대값과 둘 중 큰 쪽을 쓴다 — 판을 키워도 반경이 저절로 따라온다.")]
		[field: SerializeField, Range(0f, 0.5f)] public float SupplyReachRatio { get; private set; } = 0.12f;

		[field: Header("정수 — 강화 전용 재화(바깥 노드에서만)")]
		[field: Tooltip("연구 인형 1기에 드는 정수.")]
		[field: SerializeField, Min(0)] public int LabEssenceCost { get; private set; } = 6;

		[field: Tooltip("승급 기준 정수(실제 값은 단계에 비례).")]
		[field: SerializeField, Min(1)] public int UpgradeEssenceCost { get; private set; } = 8;

		[field: Tooltip("승급 값이 단계마다 붙는 비율 — 1단계 올릴 때마다 이만큼씩 비싸진다. " +
			"0.5 = 2단계 1.5배 · 3단계 2배. 0 이면 몇 단계를 올려도 값이 같다(무한 승급이 공짜가 된다).")]
		[field: SerializeField, Min(0f)] public float UpgradeCostGrowth { get; private set; } = 0.5f;

		[field: Tooltip("정수 색 — 자원과 확실히 달라야 두 통장이 갈린다.")]
		[field: SerializeField] public Color EssenceTint { get; private set; } = new Color(0.7f, 0.6f, 1f, 1f);

		[field: Header("적응 — 한 수단에만 기대면 통하지 않게 된다")]
		[field: Tooltip("적응 민감도(0 = 안 씀). 클수록 편중된 전략에 저항이 빨리 붙는다. 저항은 절대 절반을 넘지 않는다.")]
		[field: SerializeField, Min(0f)] public float AdaptationSensitivity { get; private set; } = 1f;

		[field: Header("이벤트 웨이브 — 성격이 변한다")]
		[field: Tooltip("몇 파마다 성격이 붙나(0 = 안 씀). 3 이면 3·6·9파가 떼거리→정예→돌진→어스름 순환.")]
		[field: SerializeField, Min(0)] public int WaveEventEvery { get; private set; } = 3;

		[field: Header("잔해 — 전투가 판을 바꾼다")]
		[field: Tooltip("마수가 죽은 자리에 남는 잔해 지속(초). 0 이면 잔해 없음.")]
		[field: SerializeField, Min(0f)] public float DebrisSeconds { get; private set; } = 6f;

		[field: Tooltip("잔해를 밟은 마수의 속도 배수(0.6 = 40% 느려짐).")]
		[field: SerializeField, Range(0.1f, 1f)] public float DebrisSlowFactor { get; private set; } = 0.6f;

		[field: Tooltip("잔해 색.")]
		[field: SerializeField] public Color DebrisTint { get; private set; } = new Color(0.45f, 0.4f, 0.38f, 1f);

		[field: Header("함정 — 바닥에 까는 것")]
		[field: Tooltip("함정 1개 비용. 포탑이 「어디를 쏘나」라면 함정은 「어디를 지나가나」 — 길목과 직결된다.")]
		[field: SerializeField, Min(0)] public int TrapCost { get; private set; } = 25;

		[field: Tooltip("밟은 마수가 받는 피해.")]
		[field: SerializeField, Min(1)] public int TrapDamage { get; private set; } = 14;

		[field: Tooltip("한 함정이 견디는 발동 횟수. 다 쓰면 사라진다 — 소모품이라 「어디에 깔까」가 매번 판단이 된다.")]
		[field: SerializeField, Min(1)] public int TrapCharges { get; private set; } = 6;

		[field: Tooltip("발동 반경(칸). 밟는 순간 주변까지 함께 때린다.")]
		[field: SerializeField, Min(0.2f)] public float TrapRadius { get; private set; } = 1.1f;

		[field: Tooltip("함정 색.")]
		[field: SerializeField] public Color TrapTint { get; private set; } = new Color(1f, 0.42f, 0.3f, 1f);

		[field: Header("벽 — 길을 내가 그린다")]
		[field: Tooltip("벽 1칸 비용. 싸야 「길을 그린다」가 성립하고, 공짜면 판을 통째로 미로로 만들어 버린다.")]
		[field: SerializeField, Min(0)] public int WallCost { get; private set; } = 12;

		[field: Tooltip("벽 색 — 생성된 암반과 구분돼야 「내가 세운 것」이 읽힌다.")]
		[field: SerializeField] public Color WallTint { get; private set; } = new Color(0.72f, 0.68f, 0.55f, 1f);

		[field: Header("유출 — 새면 잃는다")]
		[field: Tooltip("마수가 코어에서 이만큼 안으로 들어오면 「샜다」로 본다(칸). 코어를 때리게 두지 않는다.")]
		[field: SerializeField, Min(0.5f)] public float LeakRadius { get; private set; } = 1.6f;

		[field: Tooltip("마수가 멈춰 서는 거리(전술 사거리)에 이만큼 더한 것도 「샜다」로 본다 — 안 그러면 마수가 코어 앞에서 멈춰 웨이브가 영영 안 끝난다.")]
		[field: SerializeField, Min(0f)] public float LeakRangeMargin { get; private set; } = 0.5f;

		[field: Header("판매 — 되돌릴 수 있는 실수")]
		[field: Tooltip("팔 때 돌려받는 비율(0.6 = 60%). 100%면 배치가 무료 실험이 되고, 0%면 아무도 안 판다.")]
		[field: SerializeField, Range(0f, 1f)] public float SellRefundRatio { get; private set; } = 0.6f;

		[field: Header("연구 — 코어를 골라서 하는 판 안 강화")]
		[field: Tooltip("연구 1단계당 모든 포탑 피해 증가 비율(0.15 = +15%). 이미 세워둔 포탑에도 즉시 적용된다.")]
		[field: SerializeField, Min(0f)] public float LabDamageBonus { get; private set; } = 0.2f;

		[field: Tooltip("연구를 나타내는 색 — 범례가 이 색으로 「코어 연구」 줄을 그린다.")]
		[field: SerializeField] public Color LabTint { get; private set; } = new Color(0.86f, 0.62f, 1f, 1f);

		[field: Tooltip("안개 판이 땅에서 뜨는 높이 — 인형보다 낮아야 한다(안 그러면 인형을 덮는다).")]
		[field: SerializeField, Min(0f)] public float FogHeight { get; private set; } = 0.06f;

		[field: Header("마수 이동 — 천천히, 촘촘히")]
		// ★ 사용자 직접 플레이: "게임이 너무 빠름. They are billions 같이 적들이 천천히 몰려오는 느낌으로."
		//   + "몬스터들이 복셀 블럭 단위로 뚝뚝 끊겨움직이는 것처럼 보이지 않았으면 함."
		[field: Tooltip("모든 마수의 이동 속도 배수 — 1 보다 작으면 판 전체가 느려진다.")]
		[field: SerializeField, Min(0.1f)] public float EnemyMoveSpeedMultiplier { get; private set; } = 1f;

		[field: Tooltip("모서리를 얼마나 둥글게 도나(0 = 칸 중심을 딱딱 밟는다, 1 = 다음 칸 너머를 본다).")]
		[field: SerializeField, Range(0f, 1f)] public float EnemyCornerSmoothing { get; private set; } = 0.6f;

		[field: Header("연구 해금 — 처음엔 거의 아무것도 못 한다")]
		// ★ 사용자 지시(2026-08-04 직접 플레이): "처음엔 자원 건물이랑 연구만. 첫 테크 트리로 공성 건물.
		//   고급 테크 가야 좀 복잡해지는 것." 처음부터 다 열려 있으면 무엇을 할지가 아니라 *무엇부터 볼지*가
		//   숙제가 된다 — 판을 여는 순간 손이 멎는다.
		// 값 = 그 종류를 쓰려면 필요한 연구 단계. 0 이면 처음부터. 채집은 0(먹고사는 길은 늘 열려 있다).
		[field: Tooltip("공성(포탑)을 여는 연구 단계 — 첫 테크.")]
		[field: SerializeField, Min(0)] public int TowerUnlockLevel { get; private set; } = 1;

		[field: Tooltip("벽을 여는 연구 단계.")]
		[field: SerializeField, Min(0)] public int WallUnlockLevel { get; private set; } = 2;

		[field: Tooltip("함정을 여는 연구 단계.")]
		[field: SerializeField, Min(0)] public int TrapUnlockLevel { get; private set; } = 3;

		[field: Tooltip("발전 인형을 여는 연구 단계 — 전기가 필요해지는 시점.")]
		[field: SerializeField, Min(0)] public int GeneratorUnlockLevel { get; private set; } = 4;

		[field: Tooltip("전초기지를 여는 연구 단계 — 여기부터가 고급 테크(정수로 산다).")]
		[field: SerializeField, Min(0)] public int OutpostUnlockLevel { get; private set; } = 5;

		[field: Tooltip("포탑 종류가 하나 더 열리는 데 필요한 추가 연구 단계 — 첫 종류 이후 이 값마다 하나씩.")]
		[field: SerializeField, Min(1)] public int TowerVariantUnlockStep { get; private set; } = 2;

		[field: Tooltip("이 단계 *미만*의 연구는 일반 자원으로 산다. 이상부터는 정수(바깥 노드) — 고급 테크.")]
		[field: SerializeField, Min(0)] public int ResearchEssenceFromLevel { get; private set; } = 4;

		[field: Tooltip("자원으로 사는 연구 1단계 값 — 단계마다 이 값의 배수로 오른다.")]
		[field: SerializeField, Min(1)] public int LabResourceCost { get; private set; } = 60;

		[field: Header("판 밖에 남는 것 — 유물·뽑기")]
		[field: Tooltip("처음부터 쓸 수 있는 포탑 수(앞에서부터). 나머지는 유물로 뽑아야 나온다.")]
		[field: SerializeField, Min(1)] public int DefaultUnlockedTowerCount { get; private set; } = 2;

		[field: Tooltip("1분 버틸 때마다 받는 유물 — 실시간 판의 점수는 시간이다.")]
		[field: SerializeField, Min(0)] public int RelicsPerMinute { get; private set; } = 2;

		[field: Tooltip("둥지 하나를 부술 때마다 받는 유물 — 「밀어냈다」는 「버텼다」와 다른 잘함이다.")]
		[field: SerializeField, Min(0)] public int RelicsPerNest { get; private set; } = 6;

		[field: Tooltip("점수 환산 — 둥지 하나가 몇 초어치인가. 부수는 쪽이 버티기만 하는 것보다 값지게.")]
		[field: SerializeField, Min(0)] public int ScoreSecondsPerNest { get; private set; } = 90;

		[field: Tooltip("웨이브 하나 버틸 때마다 받는 유물.")]
		[field: SerializeField, Min(0)] public int RelicsPerWave { get; private set; } = 3;

		[field: Tooltip("판을 한 번 끝내면 무조건 받는 유물 — 0파에 져도 빈손은 아니다.")]
		[field: SerializeField, Min(0)] public int RelicsBaseReward { get; private set; } = 2;

		[field: Tooltip("인형 하나 뽑는 데 드는 유물.")]
		[field: SerializeField, Min(1)] public int PullCost { get; private set; } = 12;

		[field: Header("웨이브 사이 회복 — 버틴 인형은 숨을 돌린다")]
		[field: Tooltip("웨이브를 넘길 때마다 내 편이 최대 체력의 이 비율만큼 회복(0.25 = 25%). 1이면 완전 회복이라 소모전이 사라진다.")]
		[field: SerializeField, Range(0f, 1f)] public float DefenderHealPerWave { get; private set; } = 0.25f;

		[field: Header("무한 맵 — 창이 자란다")]
		[field: Tooltip("내 건물이 창 가장자리에서 이 칸 안으로 들어오면 판을 넓힌다(0 = 안 넓힘 = 고정 판).")]
		[field: SerializeField, Min(0)] public int WindowGrowMargin { get; private set; } = 24;

		[field: Tooltip("한 번에 넓히는 칸 수. 크면 덜 자주 넓히지만 한 번의 재계산이 무거워진다.")]
		[field: SerializeField, Min(4)] public int WindowGrowStep { get; private set; } = 60;

		[field: Header("건물 성장 — 자란 아이가 생긴다")]
		[field: Tooltip("마수 하나를 사거리 안에서 잡을 때 포탑이 받는 경험치(0 = 성장 끔).")]
		[field: SerializeField, Min(0)] public int KillExperience { get; private set; } = 3;

		[field: Tooltip("정산마다 채집 인형이 받는 경험치.")]
		[field: SerializeField, Min(0)] public int HarvestExperience { get; private set; } = 4;

		[field: Tooltip("레벨업 선택지 한 단계가 올리는 비율(0.2 = +20%).")]
		[field: SerializeField, Min(0f)] public float PerkStep { get; private set; } = 0.2f;

		[field: Header("코어·전초기지 방어 — 마지막 보루도 반격은 한다")]
		[field: Tooltip("코어의 자체 무기(비어 있으면 코어는 무방비). 포탑과 같은 표를 쓴다 — 두 곳이 갈라지지 않게.")]
		[field: SerializeField] public TowerDefenseTowerArchetype CoreWeapon { get; private set; }
			= new TowerDefenseTowerArchetype("코어 방어", "마지막 보루도 반격한다", 4f, 9, 0.7f, 2, new Color(1f, 0.93f, 0.45f, 1f));

		[field: Tooltip("전초기지의 자체 무기(비어 있으면 보급·목표 역할만). 넓힌 곳이 스스로 조금은 버텨야 「넓혔다」가 성립한다.")]
		[field: SerializeField] public TowerDefenseTowerArchetype OutpostWeapon { get; private set; }
			= new TowerDefenseTowerArchetype("전초기지 방어", "넓힌 곳도 스스로 버틴다", 3.2f, 6, 0.9f, 1, new Color(1f, 0.9f, 0.55f, 1f));

		[field: Header("전기 — 받아야 돈다")]
		[field: Tooltip("코어가 처음부터 내주는 전기량(0 = 전기 규칙 끔). 초반엔 이것만으로 몇 기를 돌린다.")]
		[field: SerializeField, Min(0)] public int CorePowerCapacity { get; private set; } = 6;

		[field: Tooltip("코어가 전기를 보내는 반경(칸).")]
		[field: SerializeField, Min(0f)] public float CorePowerRadius { get; private set; } = 14f;

		[field: Tooltip("발전 인형 1기 비용.")]
		[field: SerializeField, Min(0)] public int GeneratorCost { get; private set; } = 55;

		[field: Tooltip("발전 인형 1기가 내주는 전기량.")]
		[field: SerializeField, Min(0)] public int GeneratorCapacity { get; private set; } = 5;

		[field: Tooltip("발전 인형이 전기를 보내는 반경(칸).")]
		[field: SerializeField, Min(0f)] public float GeneratorRadius { get; private set; } = 11f;

		[field: Tooltip("포탑 1기가 먹는 전기.")]
		[field: SerializeField, Min(0)] public int TowerPowerDemand { get; private set; } = 1;

		[field: Tooltip("채집 인형 1기가 먹는 전기.")]
		[field: SerializeField, Min(0)] public int HarvesterPowerDemand { get; private set; } = 1;

		[field: Tooltip("발전 인형이 밝히는 시야 반경.")]
		[field: SerializeField, Min(0f)] public float GeneratorVisionRadius { get; private set; } = 5f;

		[field: Tooltip("발전 인형 색.")]
		[field: SerializeField] public Color GeneratorTint { get; private set; } = new Color(1f, 0.82f, 0.3f, 1f);

		[field: Header("마수 둥지 — 부수면 그 출구가 닫힌다")]
		[field: Tooltip("둥지 체력 = 마수 체력 × 이 값(0 = 둥지 없음, 옛 방식). 크면 「밀어낸다」가 장기 목표가 된다.")]
		[field: SerializeField, Min(0f)] public float NestHealthMultiplier { get; private set; } = 14f;

		[field: Tooltip("둥지 크기(칸 단위) — 마수보다 확실히 커야 「부술 것」으로 읽힌다.")]
		[field: SerializeField, Min(0.1f)] public float NestScale { get; private set; } = 3.2f;

		[field: Tooltip("둥지 하나를 부술 때 받는 정수 — 정수가 바깥 채집 하나에만 묶이면 그 길이 막힐 때 강화가 통째로 잠긴다.")]
		[field: SerializeField, Min(0)] public int NestEssenceReward { get; private set; } = 8;

		[field: Tooltip("둥지 색.")]
		[field: SerializeField] public Color NestTint { get; private set; } = new Color(0.66f, 0.16f, 0.3f, 1f);

		[field: Header("굳은 마수 감시 — 한 마리가 굳으면 판이 안 끝난다")]
		[field: Tooltip("이만큼 제자리에 붙어 있으면 「굳었다」로 보고 길 위로 옮긴다(0 = 감시 끔).")]
		[field: SerializeField, Min(0f)] public float StuckRelocateSeconds { get; private set; } = 4f;

		[field: Tooltip("이 거리보다 적게 움직였으면 안 움직인 것으로 친다 — 물리 떨림을 「이동」으로 세지 않기 위해.")]
		[field: SerializeField, Min(0f)] public float StuckMoveEpsilon { get; private set; } = 0.15f;

		[field: Tooltip("굳은 자리 주변 몇 칸까지 뒤져 길을 찾을지.")]
		[field: SerializeField, Min(1)] public int StuckSearchRadius { get; private set; } = 6;

		[field: Header("웨이브 사이 드래프트 — 매 웨이브 강제 선택")]
		[field: Tooltip("웨이브를 넘길 때마다 내놓는 카드 수(0 = 드래프트 없음). 3 이 표준 — 2 는 고민이 얕고, 5 는 읽는 데 지친다.")]
		[field: SerializeField] public TowerDefenseDraftRules DraftRules { get; private set; } = new TowerDefenseDraftRules
		{
			OfferCount = 3,
			FirepowerBonus = 0.18f,
			IncomeBonus = 0.2f,
			BountyBonus = 0.25f,
			LivesBonus = 1f,
			EssenceBonus = 6f,
			WindfallResource = 70f,
			RateBonus = 0.22f,
			DiscountBonus = 0.15f,
			ReachBonus = 0.2f,
			PowerBonus = 3f,
			SlowBonus = 0.12f,
			RepairRatio = 0.35f,
		};

		[field: Header("영웅 인형 — 내가 뛰어가 메운다")]
		[field: Tooltip("영웅 인형 유닛 데이터 — 비어 있으면 영웅 없이 진행(기존 판과 동일).")]
		[field: SerializeField] public Unit HeroUnit { get; private set; }

		[field: Tooltip("영웅의 전투 수치 — 포탑과 같은 표를 쓴다(사거리·연사·피해·관통·광역·둔화). 다른 표를 두면 두 곳이 갈라진다.")]
		[field: SerializeField] public TowerDefenseTowerArchetype HeroArchetype { get; private set; }

		[field: Tooltip("영웅 이동 속도(초당 월드 단위). 포탑과 다른 점은 단 하나 — 움직인다는 것.")]
		[field: SerializeField, Min(0.5f)] public float HeroMoveSpeed { get; private set; } = 6f;

		[field: Tooltip("영웅이 밝히는 시야 반경 — 움직이는 시야라 정찰 수단이 된다.")]
		[field: SerializeField, Min(0f)] public float HeroVisionRadius { get; private set; } = 6f;

		[field: Tooltip("쓰러진 영웅이 다시 일어나기까지(초). 0 이면 영영 못 일어난다 — 돌아올 방법이 없는 건 무게가 아니라 벽이다.")]
		[field: SerializeField, Min(0f)] public float HeroRespawnSeconds { get; private set; } = 25f;

		[field: Tooltip("영웅 색 — 내가 조종하는 것이라 무엇보다 눈에 띄어야 한다.")]
		[field: SerializeField] public Color HeroTint { get; private set; } = new Color(1f, 0.62f, 0.9f, 1f);

		[field: Tooltip("영웅 크기(칸 단위).")]
		[field: SerializeField, Min(0.1f)] public float HeroScale { get; private set; } = 1.1f;

		[field: Header("시야")]
		[field: Tooltip("코어가 밝히는 반경(칸). 시작 시 보이는 범위 = 여기서 정해진다.")]
		[field: SerializeField, Min(0f)] public float CoreVisionRadius { get; private set; } = 6f;

		[field: Tooltip("채집 인형이 밝히는 반경(칸) — 먼 노드로 나가는 것이 곧 시야 확장이 된다.")]
		[field: SerializeField, Min(0f)] public float HarvesterVisionRadius { get; private set; } = 4.5f;

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

		[field: Tooltip("마수를 하나씩 내보내는 간격(초). 0이면 한 프레임에 몰아 내보내 서로의 몸에 끼어 못 나온다. 「웨이브가 밀려온다」는 감각도 여기서 나온다.")]
		[field: SerializeField, Min(0f)] public float EnemySpawnInterval { get; private set; } = 0.35f;

		[field: Tooltip("같은 출현 지점에 여러 마수가 나올 때 서로 벌리는 간격 — 0이면 정확히 겹쳐 스폰돼 물리가 서로를 튕겨낸다(맵 밖으로 날아감).")]
		[field: SerializeField, Min(0f)] public float EnemySpawnSpread { get; private set; } = 1.2f;

		[field: Tooltip("무대 밖 판정 여유 — 지면 경계에서 이만큼 더 나가면 이탈로 본다.")]
		[field: SerializeField, Min(1f)] public float StageBoundsMargin { get; private set; } = 12f;

		[field: Tooltip("이 깊이보다 아래로 떨어지면 이탈로 본다(지면 통과·추락).")]
		[field: SerializeField] public float StageFloorDepth { get; private set; } = -5f;

		[field: Tooltip("채집 인형이 한 번에 처리하는 반경(칸) — 광맥 덩어리 한가운데 세우면 여러 자리를 한꺼번에 문다.")]
		[field: SerializeField, Min(0.5f)] public float HarvesterWorkRadius { get; private set; } = 2.5f;

		[field: Tooltip("채집건물 배치가 노드를 점유로 인정하는 최대 거리 — 이 반경 밖 배치는 거절(노드 스냅 X).")]
		[field: SerializeField, Min(0.5f)] public float NodeCaptureRadius { get; private set; } = 2f;

		[field: Tooltip("지면 X 축 폭(월드 단위).")]
		[field: SerializeField, Min(2f)] public float GroundWidth { get; private set; } = 24f;

		[field: Tooltip("지면 Z 축 길이(월드 단위).")]
		[field: SerializeField, Min(2f)] public float GroundLength { get; private set; } = 24f;
	}
}
