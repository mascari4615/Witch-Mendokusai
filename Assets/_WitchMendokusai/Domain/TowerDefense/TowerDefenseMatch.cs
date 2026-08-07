using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) 매치 오케스트레이터 — ArenaMatch 와 동형 셸(맵 생성 → 유닛 스폰(기존 풀, 자동 DI)
	/// → MatchCombatant/TacticDriver 부착 → TargetingSystem 등록 → TimeManager 틱으로 TowerDefenseCore 폴).
	/// 규칙 판단은 전부 순수 코어(TowerDefenseCore)에 있고 본 셸은 그 신호(TowerDefenseSignal)를 받아
	/// 스폰/자원차감/정리 같은 actuation 만 수행 — Arena 아키텍처의 "코어=브레인, 셸=손발" 원칙 그대로 재사용.
	/// 배치 UI/입력전략/게임모드진입/카메라는 별도 증분(본 셸은 매치 진행 자체만 담당).
	/// </summary>
	public class TowerDefenseMatch : MonoBehaviour
	{
		private const int DEFENDER_TEAM = 0; // 코어/타워/채집건물 소속 팀.
		private const int ATTACKER_TEAM = 1; // 웨이브 적 소속 팀.

		[field: Header("_" + nameof(TowerDefenseMatch))]
		[SerializeField] private TowerDefenseStageSO stage;
		[SerializeField] private Transform stageRoot;

		private ObjectPoolManager pool;
		private TimeManager timeManager;
		private TargetingSystem targeting;
		private TowerDefenseCore core;
		private MatchCombatant coreCombatant;
		private int nextCombatantId;
		private bool started;
		private bool ticking;
		private bool matchEndedFired;

		// 생명주기 정리(재매치 누수 방지, ArenaMatch 와 동형) — 스폰 유닛/등록 참가자/구동 드라이버 추적 →
		// Dispose 에서 despawn/unregister/정지.
		private readonly List<GameObject> spawnedUnits = new();

		// ★ 몇 번째 판인가. 인형을 세우는 일은 한 프레임 쉬었다 이어지는데, 그 사이에 판이 통째로
		//   갈릴 수 있다(다시 시작). 그때 「판이 사라졌나」만 보면 *새 판이 이미 서 있어서* 검사를
		//   통과하고, 지난 판이 부른 인형이 새 판에 세워진다.
		//   실측: 아무것도 안 지은 무방비 판에 지난 판 영웅이 서서 세 웨이브를 막았고, 코어가 안 죽어
		//   승리도 패배도 없는 판이 됐다. 「사라졌나」가 아니라 **「그 판이 맞나」**를 물어야 한다.
		private int matchGeneration;
		private readonly List<ICombatant> registeredCombatants = new();
		private readonly List<TacticDriver> drivers = new();

		// 매 틱 aliveEnemies 카운트용 — 죽거나 풀 반환된(null) 엔트리는 조회 시 제거(멱등 정리).
		// 웨이브마다 SpawnWaveRoutine 시작에서 비움(이전 웨이브 잔여가 다음 웨이브에 누적되는 것 방지).
		private readonly List<MatchCombatant> waveEnemies = new();

		// 자원 노드 점유 — 채집건물은 반드시 미점유 노드를 잡아야 가동(개척 리스크). index = stage.ResourceNodePositions 인덱스.
		private readonly HashSet<int> claimedNodes = new();

		// 격파 보상을 이미 지급한 마수(CombatantId) — 죽은 개체는 여러 틱 동안 목록에 남으므로 중복 지급 차단.
		// 오브젝트 풀이 같은 GameObject 를 되돌려주기 때문에 참조가 아니라 매치 고유 id 로 센다.
		private readonly HashSet<int> bountyPaidEnemyIds = new();

		// 가동 중인 채집 인형 위치 — 정산 때 각자 머리 위에 벌어들인 액수를 띄운다.
		// 숫자가 *어디서* 나오는지 안 보이면 채집 인형은 그냥 서 있는 장식으로 읽힌다.
		private readonly List<Transform> harvesterTransforms = new();
		// 이 채집 인형이 바깥 노드에 섰나 — 정수/자원 중 무엇을 내는지 결정.
		// ★ 열쇠는 반드시 *그 인형 자신*이다. 좌표를 열쇠로 쓰면 유닛이 바닥에 앉으며 소수점이 미세하게
		//   달라지는 순간 조회가 통째로 빗나가 **바깥 노드가 영원히 안쪽으로 취급된다** = 정수 수입 0
		//   (사용자 실증: "전초지기랑 연구소 설치 안됨" — 정수로만 사는 것들이 통째로 잠겨 있었다).
		private readonly Dictionary<Transform, bool> harvesterIsOuter = new();
		// 보급 — 코어에서 내 건물을 징검다리로 이어지는 사슬. 끊기면 그 너머의 채집은 수입이 0.
		private readonly TowerDefenseSupplyChain supplyChain = new();
		// 판을 *그리는* 층 — 바닥·암반·길 표시·표식. 규칙과 그림을 갈라둔다.
		private readonly TowerDefenseTerrainView terrainView = new();

		// 신호장 그림 — 덮인 땅의 테두리와 퍼져 나가는 파동. 무대가 서는 순간 만들어진다.
		private TowerDefenseSignalView signalView;
		// 전초기지 — 마수가 향하는 *또 하나의 목표*이자 보급의 새 원점.
		private readonly List<Transform> outposts = new();
		private readonly List<Vector2Int> pathGoals = new();

		// 고른 카드가 쌓이는 곳 — 코어 레벨업 선택이 여기로 들어온다.
		// 카드가 걸려 있는 동안 진행이 멈춘다 = 「강제 선택」의 실체.
		private readonly TowerDefenseBoonState boons = new();

		// ★ 코어도 자란다(사용자 지시: "코어 건물 자체의 레벨도 있어서 그것도 선택지 있으면 좋을듯").
		//   새 선택지 체계를 하나 더 만들지 않고 *이미 있는 드래프트 카드*를 코어 레벨업에 붙였다 —
		//   웨이브가 부르던 카드를 코어 성장이 부르게 바꾼 것뿐이다. 체계가 둘로 갈리면 같은 선택이
		//   두 곳에서 다른 규칙으로 살게 된다.
		//   성장 곡선은 스테이지가 정한다(Begin 에서 세운다) — 코드에 박아 두면 스테이지에서 아무리
		//   만져도 코어만 옛 속도로 자란다.
		private TowerDefenseBuildingProgress coreProgress;

		// 영웅 인형 — 유일하게 *움직이는* 내 편. 포탑과 같은 전투 표를 쓰되 자리를 내가 옮긴다.
		private Transform heroTransform;
		// 영웅을 실제로 걷게 하는 부품 — 좌표를 직접 옮기지 않는 이유는 세우는 곳의 ★ 주석 참고.
		private UnitMovement heroMovement;
		private MatchCombatant heroCombatant;
		private Vector3 heroTargetPosition;
		private bool heroActive;

		// 세워둔 것들의 사거리 원 — 기본은 전부 꺼져 있고, 묻는 순간(마우스 얹기)에만 하나가 켜진다.
		private readonly List<TowerDefenseRing> rangeRings = new();

		// 보급 원점(코어·전초기지)의 원 — 보급 거리 연구가 오르면 이것도 같이 자라야 한다.
		private readonly List<TowerDefenseRing> supplyRings = new();
		private TowerDefenseRing highlightedRing;
		private bool showAllRanges;

		// 이름 붙은 인형들 — 화면이 이름표를 띄우는 데 필요한 최소 정보.
		private readonly List<TowerDefenseDollLabel> dollLabels = new();
		private int nextDollOrdinal;

		// 이번 매치의 판 — 절차 생성이면 layout 이 정본, 끄면 null 이고 스테이지 SO 의 고정 레이아웃을 쓴다.
		// 아래 active* 목록이 *둘을 하나로 합친 단일 출처* — 매치 본문은 어느 쪽인지 신경 쓰지 않는다.
		// 시야 — 내 건물이 밝힌 만큼만 보인다. 건물은 안 움직이므로 *지어질 때만* 다시 계산한다.
		private TowerDefenseVision vision;
		private TowerDefenseFogView fogView;
		private readonly List<TowerDefenseVision.Source> visionSources = new();
		private readonly List<TowerDefenseVision.Source> scaledVisionSources = new();

		// 내가 세운 벽. 암반(생성된 지형)과 합쳐 「통행 불가」 하나로 본다 —
		// 길찾기·표시·배치가 각자 다른 기준을 쓰면 화면과 규칙이 갈라진다.
		private readonly HashSet<Vector2Int> wallCells = new();

		private TowerDefenseMapLayout mapLayout;
		private TowerDefenseFlowField flowField;
		private ITacticNavigator flowNavigator;

		// 격자 A* — 목표가 코어든 벽이든 *실제 경로*를 찾는다. 흐름장(코어 전용)을 대신한다.
		private TowerDefenseGridPath gridPath;
		private readonly List<Vector3> activeSpawnPoints = new();

		// 이번 파도가 밀려오는 테두리 토막(무대 로컬). 파도마다 다시 뽑히므로 출구가 고정되지 않는다
		// = 「길」이 안 생긴다. 비어 있으면 옛 고정 둥지 방식으로 되돌아간다.
		private readonly List<Vector3> invasionFront = new();

		private readonly List<Vector3> activeNodePositions = new();
		private readonly List<float> activeNodeIncomeMultipliers = new();
		// 노드 등급 — 바깥 노드는 정수를 낸다(안쪽은 자원). 「멀리 나가야 강해진다」의 근거.
		private readonly List<bool> activeNodeIsOuter = new();
		private Vector3 activeCorePosition;
		private float activeGroundWidth;
		private float activeGroundLength;

		// 이번(또는 다음) 웨이브의 마수 구성 — 원소 = EnemyArchetypes 인덱스. 결정론이라 화면 예고와 실제 스폰이 같다.
		private readonly List<int> waveComposition = new();

		// 격파 보상은 종류마다 다르다(단단한 놈일수록 크게) — 죽은 뒤엔 어떤 종류였는지 알 수 없으므로
		// 스폰 시점에 CombatantId → 보상액을 기록해 둔다.
		private readonly Dictionary<int, int> enemyBountyById = new();

		// 셀 점유(TASK-WM-194 증분3) — 타워/채집건물 배치는 한 셀에 하나만(겹배치 차단). 키 = FloorToInt 셀(y=0 고정,
		// 층 무관 단일 격자). claimedNodes(자원 노드 자체 점유)와 직교 — 이건 "그 좌표에 뭔가 이미 서 있나"만 본다.
		private readonly HashSet<Vector3Int> occupiedCells = new();

		public event Action<TowerDefenseOutcome> MatchEnded = delegate { };

		// 웨이브 자동 진행 여부 — 플레이 중 토글되므로 코어(진행 중)와 필드(다음 매치)를 함께 갱신한다.
		// 재시작해도 방금 고른 방식이 유지돼야 한다(설정을 매번 다시 고르게 만들지 않는다).
		private bool autoAdvanceWaves = true;
		private bool waveModeInitialized;

		/// <summary>
		/// 이번 판의 난이도 — 다음 판에도 유지된다(매번 다시 고르게 하면 그건 설정이 아니라 잔소리다).
		/// 판이 도는 중에 바꿔도 이미 시작한 판에는 안 걸린다(시작 조건이므로).
		/// </summary>
		public TowerDefenseDifficultyKind Difficulty { get; set; } = TowerDefenseDifficultyKind.Normal;

		private TowerDefenseDifficulty difficulty = TowerDefenseDifficulty.For(TowerDefenseDifficultyKind.Normal);

		public bool AutoAdvanceWaves
		{
			get => autoAdvanceWaves;
			set
			{
				autoAdvanceWaves = value;
				if (core != null)
					core.AutoAdvance = value;
			}
		}

		/// <summary> 다음 웨이브 호출(수동 진행 / 자동에서도 즉시 시작). 건설 국면이 아니면 false. </summary>
		public bool RequestNextWave() => core != null && core.RequestNextWave();

		/// <summary> 수동 진행에서 호출이 예약된 상태인지 — HUD 표시용. </summary>
		public bool IsNextWaveRequested => core != null && core.IsNextWaveRequested;

		/// <summary>
		/// 이번 웨이브 적 추적 목록(읽기 전용) — **진단용**. "다 잡은 것 같은데 안 넘어간다"는
		/// 곧 "코어가 세는 생존자와 화면에서 보이는 것이 다르다"는 뜻이라, 무엇이 살아 있다고
		/// 집계되는지를 좌표·체력까지 직접 볼 수 있어야 원인을 짚는다(추측 금지).
		/// </summary>
		public IReadOnlyList<MatchCombatant> WaveEnemies => waveEnemies;

		/// <summary> 지금 판의 크기(월드 단위) — 미니맵이 좌표를 비율로 바꿀 때 쓴다. 판이 자라면 같이 커진다. </summary>
		public float GroundWidth => activeGroundWidth;
		public float GroundLength => activeGroundLength;

		/// <summary> 전초기지 위치들 — 미니맵이 「내가 넓힌 곳」을 그린다. </summary>
		public IReadOnlyList<Transform> Outposts => outposts;

		/// <summary>
		/// 코어가 보는 생존 적 수 — HUD 표시 + 진단 대조용. 매 프레임 읽히므로 목록을 건드리지 않는
		/// **순수 집계**(정리는 코어 틱의 CountAliveEnemies 가 담당 — 표시가 상태를 바꾸면 안 된다).
		/// </summary>
		public int AliveEnemyCount
		{
			get
			{
				int count = 0;
				foreach (MatchCombatant combatant in waveEnemies)
				{
					if (combatant == null || combatant.IsAlive == false)
						continue;
					// ★ 둥지는 이 목록에 *포탑이 쏘라고* 들어 있다 — 쳐들어오는 마수가 아니다.
					//   같이 세면 화면의 「적 N마리」가 둥지 수만큼 늘 거짓말을 하고(실측 +8),
					//   「아무도 안 죽는다」를 보는 진단도 절대 안 움직이는 8개에 묻힌다.
					if (nestCombatants.Contains(combatant))
						continue;
					count++;
				}
				return count;
			}
		}

		/// <summary>
		/// 진행 중인 스테이지 데이터(읽기 전용) — 검증 하네스가 좌표·수치를 **정본에서 읽게** 한다.
		/// 하네스에 좌표를 박아두면 레이아웃을 옮기는 순간 검사가 조용히 무의미해진다(항상 거절만 확인).
		/// </summary>
		public TowerDefenseStageSO Stage => stage;

		/// <summary> 코어 참가자(진단용) — 적이 코어를 실제로 때리고 있는지 체력으로 확인한다. </summary>
		public MatchCombatant CoreCombatant => coreCombatant;

		/// <summary> 매치에 등록된 전 참가자(진단용) — 수비 유닛 생존 여부 확인. </summary>
		public IReadOnlyList<ICombatant> RegisteredCombatants => registeredCombatants;

		public int Resource => core != null ? core.Resource : 0;
		public int WaveIndex => core != null ? core.WaveIndex : 0;
		public TowerDefensePhase Phase => core != null ? core.Phase : TowerDefensePhase.Prepare;
		public TowerDefenseOutcome Outcome => core != null ? core.Outcome : TowerDefenseOutcome.InProgress;
		public float PrepareRemaining => core != null ? core.PrepareRemaining : 0f;

		/// <summary> 다음 정산액 + 가동 채집 인형 수 — 「채집 인형이 뭐 하는 놈인지」를 화면이 말하는 근거 숫자. </summary>
		public int NextWaveIncome => core != null ? core.NextWaveIncome : 0;
		public int Essence => core != null ? core.Essence : 0;
		public int NextWaveEssence => core != null ? core.NextWaveEssence : 0;
		// 수입 가중치를 보급이 정하게 되면서 core 의 누적 카운트는 늘 0 이 됐다 — 실제 목록이 진실.
		public int HarvesterCount => harvesterTransforms.Count;

		/// <summary> 프로그래매틱 시작(런처/모드 진입용) — stage·stageRoot 주입 후 Begin. </summary>
		public void Begin(TowerDefenseStageSO stageConfig, Transform root)
		{
			stage = stageConfig;
			stageRoot = root;

			// 진행 방식 기본값은 스테이지가 정하지만, 플레이어가 한 번 고르면 그 선택이 재시작을 넘어 유지된다.
			if (waveModeInitialized == false && stage != null)
			{
				autoAdvanceWaves = stage.AutoAdvanceWavesDefault;
				waveModeInitialized = true;
			}

			Begin();
		}

		public void Begin()
		{
			if (started)
			{
				Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 이미 진행 중 — 중복 Begin 무시(재진입 가드).");
				return;
			}
			if (stage == null || stageRoot == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: stage/stageRoot 미할당 — 시작 불가.");
				return;
			}
			if (stage.CoreUnit == null || stage.CoreUnit.Prefab == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: stage.CoreUnit/Prefab 미할당 — 코어 없이 시작 불가.");
				return;
			}

			// 코어의 성장 곡선을 스테이지에서 받아 세운다 — 판마다 다시 세우므로 지난 판의 레벨이 새 판으로 새지 않는다.
			coreProgress = new TowerDefenseBuildingProgress(stage.CoreLevelBaseCost, stage.CoreLevelGrowth);

			started = true;
			StartCoroutine(BeginRoutine());
		}

		private IEnumerator BeginRoutine()
		{
			// init-order-ok: World 부팅 후 호출 보장(ArenaMatch 와 동형 — 스코프 미배선 v1). 진입부 1회 캡처(fail-fast).
			pool = ObjectPoolManager.Instance;
			timeManager = TimeManager.Instance;
			if (pool == null || timeManager == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: ObjectPoolManager/TimeManager Instance null — World 부팅 후 호출 필요.");
				started = false;
				yield break;
			}

			PrepareLayout(); // 판을 먼저 확정 — 지면·노드·스폰·길안내가 전부 여기서 파생된다.
			BuildGround();

			targeting = new TargetingSystem();

			// ★ 난이도는 *시작 조건*이다 — 규칙을 갈라 쓰지 않고 숫자만 곱한다(갈라 쓰면 다른 게임이 된다).
			difficulty = TowerDefenseDifficulty.For(Difficulty);
			TowerDefenseRules scaledRules = stage.Rules;
			scaledRules.StartingResource = Mathf.Max(1, Mathf.RoundToInt(scaledRules.StartingResource * difficulty.StartingResourceScale));
			scaledRules.StartingLives = Mathf.Max(1, Mathf.RoundToInt(scaledRules.StartingLives * difficulty.LivesScale));
			scaledRules.PressurePerMinute *= difficulty.PressureScale;
			scaledRules.FirstWaveEnemyCount = Mathf.Max(1, Mathf.RoundToInt(scaledRules.FirstWaveEnemyCount * difficulty.EnemyCountScale));

			core = new TowerDefenseCore(scaledRules)
			{
				AutoAdvance = autoAdvanceWaves,
				FirstAutoWave = stage.ManualFirstWave ? 1 : 0,
			};
			// 새 판 = 새 세대. 지난 판이 부르던 인형이 뒤늦게 도착해도 이 숫자가 갈라준다.
			matchGeneration++;

			nextCombatantId = 0;
			matchEndedFired = false;
			claimedNodes.Clear(); // 재진입 — 지난 매치의 노드 점유가 새 매치로 새는 것 방지.
			bountyPaidEnemyIds.Clear();
			enemyBountyById.Clear();
			harvesterTransforms.Clear();
			harvesterIsOuter.Clear();
			supplyChain.Clear();
			outposts.Clear();
			DisconnectedHarvesters = 0;
			LabCount = 0;
			RefreshAvailableSlots(); // 판이 열릴 때의 해금 상태 — 처음엔 채집뿐이다.
			TrapsSpent = 0;
			speedStep = 1;
			lastRunningStep = 1;
			ApplySpeed();
			occupiedCells.Clear(); // 재진입 — 지난 매치의 셀 점유가 새 매치로 새는 것 방지.

			// 새 판 = 새 선택·새 이름·새 영웅. 하나라도 남으면 "새 판"이 아니다.
			boons.Reset();
			dollLabels.Clear();
			soldDolls.Clear();
			nextDollOrdinal = 0;
			// 연구로 쌓은 것도 판과 함께 끝난다 — 안 지우면 다음 판이 지난 판의 연구를 물고 시작한다
			// (코어 성장과 같은 병. 「새 판」이라면 아무것도 안 남아야 한다).
			ClearResearch();

			heroActive = false;
			heroTransform = null;
			heroMovement = null; // 남겨두면 다음 판이 지난 판의 몸을 붙잡고 걷게 시킨다.
			heroCombatant = null;
			heroVisionSourceIndex = -1;
			heroRespawnRemaining = 0f;
			heroVisionCell = new Vector2Int(int.MinValue, int.MinValue);
			enemyMaxStopDistance = 0f;
			nests.Clear();
			nestCombatants.Clear();
			nestsEverSpawned = false;
			NestsDestroyed = 0;
			BuiltCount = 0;
			LostCount = 0;
			KilledCount = 0;
			PeakEnemies = 0;
			LeakedCount = 0;
			windowGrowing = false;
			powerGrid.Clear();
			enemyStillness.Clear();

			yield return SpawnCoreRoutine();
			if (coreCombatant == null)
			{
				// 코어 스폰 자체가 실패 — 이미 로그됨. 진입 상태만 리셋(started 가드 해제).
				started = false;
				yield break;
			}

			yield return SpawnHeroRoutine(); // 영웅 미설정 스테이지면 즉시 빠져나온다(기존 판과 동일).
			yield return SpawnNestsRoutine(); // 마수가 나오는 자리를 *부술 수 있는 것*으로 세운다.
			yield return SpawnLairsRoutine(); // 판 곳곳에 잠든 마수 — 넓히는 행위 자체를 위험으로 만든다.

			timeManager.RegisterCallback(Tick);
			ticking = true;

			// 이어하기가 예약돼 있으면 여기서 되살린다(값을 먼저 맞추고 건물을 한 채씩).
			if (pendingRestore != null)
			{
				TowerDefenseSaveData restore = pendingRestore;
				pendingRestore = null;
				yield return RestoreRoutine(restore);
			}
		}

		/// <summary>
		/// 이번 매치의 판 확정 — 절차 생성이면 생성기를 돌리고, 아니면 스테이지 SO 의 고정값을 그대로 담는다.
		/// 어느 쪽이든 결과는 같은 active* 목록이라 매치 본문에는 분기가 없다(분기를 여기저기 흩으면
		/// 언젠가 한 곳이 옛 경로를 보고 조용히 어긋난다).
		/// </summary>
		private void PrepareLayout()
		{
			activeSpawnPoints.Clear();
			activeNodePositions.Clear();
			activeNodeIncomeMultipliers.Clear();
			activeNodeIsOuter.Clear();
			mapLayout = null;
			flowField = null;
			flowNavigator = null;

			if (stage.UseProceduralMap == false)
			{
				activeCorePosition = stage.CorePosition;
				activeGroundWidth = stage.GroundWidth;
				activeGroundLength = stage.GroundLength;

				if (stage.EnemySpawnPoints != null)
					activeSpawnPoints.AddRange(stage.EnemySpawnPoints);
				if (stage.ResourceNodePositions != null)
				{
					foreach (Vector3 nodePosition in stage.ResourceNodePositions)
					{
						activeNodePositions.Add(nodePosition);
						activeNodeIncomeMultipliers.Add(1f);
						activeNodeIsOuter.Add(false);
					}
				}
				return;
			}

			TowerDefenseMapParameters parameters = stage.MapParameters;
			if (stage.RandomizeSeedEachMatch)
				parameters.Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

			// ★ 지정된 씨앗이 있으면 그 판을 그대로 다시 만든다 — 「이 씨앗 해봐」가 성립하는 자리.
			//   판 전체가 씨앗 하나에서 태어나므로, 판을 나누는 데 드는 것이 숫자 한 줄뿐이다.
			//   한 번 쓰면 지운다(다음 판까지 계속 같은 땅이면 그건 고정이지 공유가 아니다).
			if (nextMatchSeed.HasValue)
			{
				parameters.Seed = nextMatchSeed.Value;
				nextMatchSeed = null;
			}

			// 이어하기면 그 판의 씨앗을 그대로 쓴다 — 같은 땅이 다시 나와야 내 건물이 제자리에 선다.
			destroyedNestPositions.Clear();
			if (pendingRestore != null)
			{
				// 부순 둥지는 *세우기 전에* 알아야 한다 — 세운 뒤 지우면 한 프레임이라도 되살아난다.
				if (pendingRestore.DestroyedNestPositions != null)
					destroyedNestPositions.AddRange(pendingRestore.DestroyedNestPositions);

				parameters.Seed = pendingRestore.MapSeed;
				if (pendingRestore.MapWidth > 0 && pendingRestore.MapLength > 0)
				{
					parameters.Width = pendingRestore.MapWidth;
					parameters.Length = pendingRestore.MapLength;
				}
			}

			mapLayout = TowerDefenseMapGenerator.Generate(parameters);

			activeCorePosition = mapLayout.CorePosition;
			activeGroundWidth = mapLayout.GroundWidth;
			activeGroundLength = mapLayout.GroundLength;
			activeSpawnPoints.AddRange(mapLayout.EnemySpawnPoints);
			foreach (TowerDefenseResourceNodeSpot node in mapLayout.ResourceNodes)
			{
				activeNodePositions.Add(node.Position);
				activeNodeIncomeMultipliers.Add(node.IncomeMultiplier);
				activeNodeIsOuter.Add(node.Tier == TowerDefenseNodeTier.Outer);
			}

			// 길 안내판 — 암반이 생긴 순간 직선 이동은 벽에 박힌다(웨이브가 영원히 안 끝나는 그 사고).
			wallCells.Clear();
			flowField = new TowerDefenseFlowField(
				mapLayout.Width, mapLayout.Length, mapLayout.CoreCell, IsPathBlocked);
			gridPath = new TowerDefenseGridPath(mapLayout.Width, mapLayout.Length, IsPathBlocked);
			flowNavigator = new TowerDefensePathNavigator(
				mapLayout, gridPath, stageRoot, stage.GroundCellSize * 2f, stage.EnemyCornerSmoothing);

			vision = new TowerDefenseVision(mapLayout.Width, mapLayout.Length);
			visionSources.Clear();

			Debug.Log($"{nameof(TowerDefenseMatch)}: 판 생성 seed={mapLayout.Seed} "
				+ $"암반={mapLayout.ObstacleCells.Count}칸 노드={mapLayout.ResourceNodes.Count} 스폰={mapLayout.EnemySpawnPoints.Count}");
		}

		/// <summary> 통행 불가 판정 — 생성된 암반 + 내가 세운 벽. 길찾기·표시가 같은 함수를 본다. </summary>
		private bool IsPathBlocked(Vector2Int cell)
		{
			return mapLayout.IsBlocked(cell) || wallCells.Contains(cell);
		}

		/// <summary>
		/// 그 칸의 포탑을 한 단계 올린다 — 같은 종류일 때만. 값은 기본 비용 × 다음 단계.
		/// 최대 단계거나 다른 종류면 아무 일도 안 일어난다(자원 무변경).
		/// </summary>
		private bool TryUpgradeTowerAt(Vector3Int cellKey, int towerIndex)
		{
			TowerDefenseTowerArchetype archetype = TowerArchetypeAt(towerIndex);
			if (archetype == null)
				return false;

			foreach (GameObject unit in spawnedUnits)
			{
				if (unit == null || unit.activeInHierarchy == false)
					continue;
				if (ToCellKey(unit.transform.position) != cellKey)
					continue;
				// 그 칸을 지나가던 마수가 먼저 잡히면 승급이 조용히 거절된다 — 내가 세운 것만 본다.
				if (supplyChain.Contains(unit.transform) == false)
					continue;

				TowerDefenseWeapon weapon = unit.GetComponent<TowerDefenseWeapon>();
				// 같은 종류인지는 *정체*로 묻는다 — 값으로 물으면 값이 같은 두 종류가 한 종류로 뭉쳐
				// 엉뚱한 무기가 조용히 승급된다(값은 언제든 같아질 수 있는 수치일 뿐이다).
				if (weapon == null || weapon.Archetype != archetype)
					return false; // 다른 종류(또는 포탑이 아님) — 겹배치 차단 그대로.
				if (weapon.Level >= archetype.MaxLevel)
					return false;

				// 승급도 정수 — 「지금 더 짓기(자원)」 vs 「있는 걸 키우기(정수)」가 서로 다른 통장을 쓴다.
				// 값이 단계마다 얼마나 붙는지는 스테이지가 정한다 — 여기 숫자를 박아두면
				// 밸런스를 만질 때마다 코드를 고쳐야 하고, 화면에 노출된 다른 수치와 갈라진다.
				int upgradeCost = Mathf.Max(1, Mathf.RoundToInt(
					stage.UpgradeEssenceCost * (weapon.Level + 1) * stage.UpgradeCostGrowth));
				if (core.TrySpendEssence(upgradeCost) == false)
				{
					// ★ 조용히 false 를 돌려주면 「눌렀는데 아무 일도 안 일어난다」가 된다 —
					//   사람은 그걸 고장으로 읽는다. 왜 안 되는지와 어떻게 버는지를 그 자리에서 말한다.
					Reject(EssenceShortText(upgradeCost), unit.transform.position);
					return false;
				}

				weapon.TryUpgrade();

				// 이름표에도 단계가 붙는다 — 같은 아이가 자란 것이지 새 물건이 생긴 것이 아니다.
				TowerDefenseDollLabel label = FindDollLabel(unit.transform);
				if (label != null)
					label.Level = weapon.Level;

				PopWorldText("Lv." + weapon.Level, unit.transform.position, TextType.Exp);
				RefreshTowerRing(unit);
				return true;
			}

			return false;
		}

		/// <summary>
		/// 사거리가 자라면 화면의 원도 같이 자라야 한다 — 안 그러면 원이 거짓말한다.
		///
		/// ★ 반지름을 여기서 *다시 계산하지 않는다*. 예전엔 승급 배수만 손으로 곱했는데,
		///   그 식에는 강화로 늘어난 사거리가 빠져 있었다 — 사거리 강화를 골라도 원이 그대로였다.
		///   실제로 쏘는 거리를 쥔 쪽(무기)에게 물으면 둘이 갈라질 수가 없다.
		/// </summary>
		private void RefreshTowerRing(GameObject unit)
		{
			TowerDefenseWeapon weapon = unit != null ? unit.GetComponent<TowerDefenseWeapon>() : null;
			TowerDefenseRing ring = unit != null ? unit.GetComponentInChildren<TowerDefenseRing>() : null;
			if (weapon != null && ring != null)
				ring.SetRadius(weapon.Range);
		}

		/// <summary>
		/// 그 자리에 *무엇이든* 세울 수 있나 — 판 안인가 · 암반이 아닌가 · 내 땅인가.
		///
		/// ★ 왜 한 곳으로 모았나: 여섯 배치 경로가 이 검사를 각자 베껴 쓰고 있었고, 그러다 보니
		///   경로마다 빠진 것이 달랐다(함정은 판 끝 검사가 없어 판 밖에 깔렸고, 벽은 암반 위에 섰다.
		///   둘 다 「보급이 닿는 곳에만」 규칙 밖이었다 — 그건 사용자가 명시적으로 요청한 규칙이다).
		///   검사가 여러 벌이면 새 배치를 추가할 때마다 한 벌이 또 빠진다.
		/// 점유·값은 여기 없다 — 경로마다 다르게 취급한다(포탑은 같은 칸이면 승급, 채집은 노드로 스냅).
		/// </summary>
		private bool ValidateSite(Vector3 worldPosition)
		{
			if (CanBuildAt(worldPosition))
				return true;

			// 여기 왔으면 셋 중 하나가 막은 것 — 어느 것인지만 골라 말한다.
			if (IsInsideWindow(worldPosition) == false)
				return Reject("판 끝이다 — 여기부터는 아직 열리지 않았다", worldPosition);
			if (IsObstacleAt(worldPosition))
				return Reject("암반 위엔 못 짓는다", worldPosition);
			return Reject("보급이 닿는 곳에만 지을 수 있다", worldPosition);
		}

		/// <summary>
		/// 그 자리에 지을 수 있나 — *조용한* 판정. 미리보기가 매 프레임 묻는다(거절 사유를 쏟으면 안 된다).
		///
		/// ★ 규칙 자체는 이 한 줄이 전부다. 예전엔 미리보기가 같은 판정을 자기 손으로 다시 조립했고,
		///   그러다 **판 끝 검사를 빠뜨렸다** — 가장자리에서 초록불이 켜지는데 실제로는 거절됐다.
		///   화면이 「여기 된다」고 해놓고 안 되면 그 화면을 믿을 수 없게 된다.
		/// 칸이 찼는지는 여기 없다 — 경로마다 다르게 취급한다(포탑은 같은 칸이면 승급).
		/// </summary>
		public bool CanBuildAt(Vector3 worldPosition)
		{
			return IsMatchOver == false
				&& IsInsideWindow(worldPosition)
				&& IsObstacleAt(worldPosition) == false
				&& IsInBuildableRange(worldPosition);
		}

		/// <summary>
		/// 판이 끝났나 — 끝난 판에는 아무것도 더 못 짓는다.
		///
		/// ★ 라이브에서 잡았다: 목숨이 0 이 되어 결말 화면이 떠 있는데도 건물이 계속 세워졌다.
		///   끝난 판에 손을 대면 「무엇이 그 성적을 만들었나」가 흐려지고(요약은 끝난 시점을 말하는데
		///   화면엔 그 뒤에 세운 것이 서 있다), 다시 도전을 누르기 전까지 판이 끝난 것도 안 끝난 것도
		///   아닌 상태가 된다. 끝은 끝이어야 한다.
		/// </summary>
		public bool IsMatchOver => Outcome != TowerDefenseOutcome.InProgress;

		/// <summary>
		/// 함정 깔기 — 밟으면 터진다. 길목과 직결되므로 벽(길 그리기)의 짝.
		/// 통행을 막지 않으므로 길 검사가 필요 없다(그래서 벽보다 훨씬 가볍다).
		/// </summary>
		public bool TryPlaceTrap(Vector3 worldPosition)
		{
			if (core == null || mapLayout == null || stageRoot == null)
				return false;

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey))
				return Reject("여긴 이미 찼다", worldPosition);
			if (ValidateSite(worldPosition) == false)
				return false;

			int trapCost = CostOf(TowerDefensePlaceableKind.Trap);
			if (core.TrySpend(trapCost) == false)
				return Reject($"자원 부족 {core.Resource}/{trapCost}", worldPosition);

			occupiedCells.Add(cellKey);
			BuildTrapObject(worldPosition, cellKey);
			return true;
		}

		private void BuildTrapObject(Vector3 worldPosition, Vector3Int cellKey)
		{
			float cellSize = stage.GroundCellSize;
			GameObject trapObject = TowerDefenseVisuals.Primitive(PrimitiveType.Quad);
			trapObject.name = "Trap";
			Destroy(trapObject.GetComponent<Collider>()); // 밟는 판정은 거리로 한다 — 물리를 끼우면 마수가 걸린다.
			trapObject.transform.SetParent(stageRoot, false);
			trapObject.transform.position = worldPosition + new Vector3(0f, 0.05f, 0f);
			trapObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			trapObject.transform.localScale = Vector3.one * cellSize * 0.85f;

			Renderer trapRenderer = trapObject.GetComponent<Renderer>();
			if (trapRenderer != null)
			{
				Material trapMaterial = new Material(trapRenderer.sharedMaterial);
				TowerDefenseVisuals.MakeTransparent(trapMaterial);
				Color trapColor = stage.TrapTint;
				trapColor.a = 0.75f;
				trapMaterial.color = trapColor;
				if (trapMaterial.HasProperty("_BaseColor"))
					trapMaterial.SetColor("_BaseColor", trapColor);
				trapRenderer.sharedMaterial = trapMaterial;
			}

			TowerDefenseTrap trap = trapObject.AddComponent<TowerDefenseTrap>();
			// ★ 함정도 전기를 먹는다 — 벽·함정만 체계 밖에 있으면 「전기가 부족하면 방어가 선다」는 규칙이
			//   반쪽이 된다. 전기가 끊긴 함정은 밟혀도 안 터진다.
			powerGrid.AddConsumer(trapObject.transform);

			trap.Configure(waveEnemies, Mathf.RoundToInt(stage.TrapDamage * boons.TrapPowerMultiplier), stage.TrapCharges, stage.TrapRadius,
				spent =>
				{
					// 다 쓴 함정은 자리를 비워준다 — 안 비우면 그 칸이 영영 죽는다.
					occupiedCells.Remove(cellKey);
					TrapsSpent++;
					if (spent != null)
						Destroy(spent.gameObject);
				});
		}

		/// <summary> 다 쓰고 사라진 함정 수 — 검증·통계용. </summary>
		public int TrapsSpent { get; private set; }

		/// <summary>
		/// 벽 세우기 — 마수의 길을 *내가 그린다*. 장르적으로 여기가 가장 큰 전환점이다:
		/// 「어디에 지을까」가 「길을 어떻게 낼까」로 승격된다.
		///
		/// ★ 단 하나의 불변식: **길을 완전히 막을 수는 없다.** 모든 출현 지점에서 코어까지 가는 길이
		///   남아야 한다. 안 그러면 마수가 벽 앞에 굳고 웨이브가 영원히 안 끝난다(이미 겪은 사고).
		///   그래서 *먼저 세워보고 길이 남는지 확인한 뒤* 확정한다 — 안 되면 원상복구하고 거절.
		/// </summary>
		public bool TryPlaceWall(Vector3 worldPosition)
		{
			if (core == null || mapLayout == null || stageRoot == null)
				return false;

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey))
				return Reject("여긴 이미 찼다", worldPosition);
			if (ValidateSite(worldPosition) == false)
				return false;

			Vector2Int cell = mapLayout.WorldToCell(stageRoot.InverseTransformPoint(worldPosition));
			if (mapLayout.IsInside(cell) == false || IsPathBlocked(cell))
				return false;

			wallCells.Add(cell);
			if (RebuildPathing() == false)
			{
				wallCells.Remove(cell); // 길이 끊긴다 — 없던 일로.
				RebuildPathing();
				Debug.Log($"{nameof(TowerDefenseMatch)}: 벽 거절 — 여길 막으면 마수가 코어까지 갈 길이 없다.");
				return false;
			}

			if (core.TrySpend(CostOf(TowerDefensePlaceableKind.Wall)) == false)
			{
				wallCells.Remove(cell);
				RebuildPathing();
				return false;
			}

			occupiedCells.Add(cellKey);
			BuildWallObject(cell);
			return true;
		}

		/// <summary>
		/// 길 다시 계산 + 표시 갱신. 모든 출현 지점에서 코어까지 갈 수 있으면 true.
		/// 흐름장이 이미 있어 재계산이 싸다 — 벽을 세울 때마다 전부 다시 그려도 부담이 없다.
		/// </summary>
		/// <summary>
		/// 코어 둘레에 *여러 진입점*을 목표로 더한다 — 「사방에서 넓은 면으로 밀려온다」의 근본.
		///
		/// ★ 왜 필요한가 (사용자 실측: "여전히 거의 한 줄", "떼거지로"): 목표가 코어 한 점이면
		///   모든 길이 그 한 점으로 수렴한다. 같은 거리의 길 중 내 것을 고르게 해도, *정확한 대각선*
		///   방향에서는 최단 경로가 하나뿐이라 못 흩어진다(시험으로 확인한 한계).
		///   목표를 코어를 감싼 고리로 나누면, 마수마다 *가장 가까운 진입점*이 달라져서 마지막까지
		///   갈라진 채 다가온다 — 길찾기는 그대로 최단이고, 벽도 그대로 돈다.
		/// ★ 막힌 칸은 안 넣는다 — 못 가는 곳을 목표로 두면 그 방향이 통째로 죽는다.
		/// </summary>
		private void AddApproachRing(Vector2Int coreCell)
		{
			int radius = Mathf.Max(1, stage.CoreApproachRingCells);
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					// 고리 = 정사각 테두리만. 안쪽까지 채우면 코어 주변이 통째로 목표라 뜻이 없다.
					if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
						continue;

					Vector2Int cell = new Vector2Int(coreCell.x + dx, coreCell.y + dy);
					if (mapLayout.IsInside(cell) == false || IsPathBlocked(cell))
						continue;
					pathGoals.Add(cell);
				}
			}
		}

		private bool RebuildPathing()
		{
			pathGoals.Clear();
			pathGoals.Add(mapLayout.CoreCell);
			AddApproachRing(mapLayout.CoreCell);
			foreach (Transform outpost in outposts)
			{
				if (outpost != null)
					pathGoals.Add(mapLayout.WorldToCell(stageRoot.InverseTransformPoint(outpost.position)));
			}

			flowField = new TowerDefenseFlowField(
				mapLayout.Width, mapLayout.Length, pathGoals, IsPathBlocked);
			gridPath = new TowerDefenseGridPath(mapLayout.Width, mapLayout.Length, IsPathBlocked);
			flowNavigator = new TowerDefensePathNavigator(
				mapLayout, gridPath, stageRoot, stage.GroundCellSize * 2f, stage.EnemyCornerSmoothing);

			foreach (Vector3 spawnLocal in activeSpawnPoints)
			{
				if (flowField.IsReachable(mapLayout.WorldToCell(spawnLocal)) == false)
					return false;
			}

			// ★ 파도는 이제 **테두리 어디서든** 온다(테두리 침공). 그런데 이 검사는 옛 개념인 *둥지 자리*만
			//   보고 있었다 — 그래서 다음 파도가 올 토막을 벽으로 막아도 통과했고, 그러면 그 파도는
			//   길이 없는 자리에서 태어나 판이 교착된다. 「어디서든 온다」면 **테두리 전체**를 봐야 한다.
			if (stage.BorderInvasion && IsBorderReachable() == false)
				return false;

			// 이미 걷고 있는 마수도 새 길을 따라야 한다 — 안 그러면 벽 안쪽에 갇힌다.
			foreach (TacticDriver driver in drivers)
			{
				if (driver != null)
					driver.Navigator = flowNavigator;
			}

			terrainView.BuildPathLanes();
			return true;
		}

		/// <summary>
		/// 판 테두리의 어느 지점에서든 코어까지 닿는가.
		///
		/// ★ 파도가 매번 다른 토막에서 오므로 「그 토막만」 검사할 수는 없다 — 지금 막아둔 벽은 *나중*
		///   파도에도 그대로 남기 때문이다. 테두리를 고르게 훑어 하나라도 갇히면 그 벽은 거절한다.
		/// 각도 간격은 노출값 — 촘촘할수록 안전하고 그만큼 판정이 무겁다.
		/// </summary>
		private bool IsBorderReachable()
		{
			if (mapLayout == null || flowField == null)
				return true;

			// ★ 「테두리의 *모든* 점이 닿아야 한다」로 만들었더니 **벽이 하나도 안 섰다**(실측: placed=0).
			//   테두리에는 원래 암반이 박혀 있어서, 내 벽과 무관하게 못 닿는 점이 늘 있다 —
			//   새로 만든 자물쇠가 주인을 막은 것이다.
			// 진짜로 막아야 하는 것은 「한 방향이 통째로 봉인되는 것」이다. 출현 자리는 어차피
			//   갈 수 있는 칸으로 스냅되므로, **방위마다 한 곳이라도 닿으면** 그 방향은 살아 있다.
			float step = Mathf.Max(1f, stage.BorderCheckStepDegrees);
			for (int sector = 0; sector < 8; sector++)
			{
				float from = sector * 45f;
				bool anyReachable = false;

				for (float angle = from; angle < from + 45f; angle += step)
				{
					Vector3 local = TowerDefenseWaveOrigin.BorderPoint(
						angle, activeGroundWidth * 0.5f, activeGroundLength * 0.5f, stage.InvasionEdgeInset);

					if (flowField.IsReachable(mapLayout.WorldToCell(local)) == false)
						continue;

					anyReachable = true;
					break;
				}

				if (anyReachable == false)
					return false; // 이 방위가 통째로 막혔다 — 그쪽에서 올 파도가 갇힌다.
			}
			return true;
		}

		private void BuildWallObject(Vector2Int cell)
		{
			float cellSize = mapLayout.CellSize;
			GameObject wall = TowerDefenseVisuals.Primitive(PrimitiveType.Cube);
			wall.name = "Wall";
			wall.transform.SetParent(stageRoot, false);
			wall.transform.localPosition = mapLayout.CellToWorld(cell) + new Vector3(0f, cellSize * 0.35f, 0f);
			wall.transform.localScale = new Vector3(cellSize * 0.94f, cellSize * 0.7f, cellSize * 0.94f);

			Renderer wallRenderer = wall.GetComponent<Renderer>();
			if (wallRenderer == null)
				return;

			Color wallColor = stage.WallTint;
			Material wallMaterial = new Material(wallRenderer.sharedMaterial);
			wallMaterial.color = wallColor;
			if (wallMaterial.HasProperty("_BaseColor"))
				wallMaterial.SetColor("_BaseColor", wallColor);
			wallRenderer.sharedMaterial = wallMaterial;

			// 벽도 보급 중계다 — 길을 그리는 것과 보급선을 잇는 것이 같은 행위가 되어,
			// 「어디에 벽을 세울까」가 방어선과 살림살이 양쪽을 동시에 결정한다.
			supplyChain.Add(wall.transform);
			RefreshSupply();
		}


		/// <summary> URP Lit 재질을 반투명으로 — 불투명 그대로면 길 표시가 바닥을 덮어버린다. </summary>


		/// <summary> 지면(바닥) 런타임 생성 — RectangleArenaMap.Build 와 동형(Plane 스케일, SO 수치 그대로). </summary>
		private void BuildGround()
		{
			// 그리는 일은 통째로 다른 층이 한다 — 여기 남는 것은 「무엇을 그릴지」 넘겨주는 일뿐이다.
			terrainView.Configure(stageRoot, stage, mapLayout, flowField, activeSpawnPoints, activeNodePositions);
			terrainView.Build(activeGroundWidth, activeGroundLength);

			if (vision != null)
			{
				// ★ 안개는 *땅을 어둡게* 하는 것이지 인형을 덮는 판때기가 아니다(사용자 실증:
				//   "안개랑 길도 마찬가지. 뭐 유닛들 가리고 난리났어. 롤처럼 오브젝트 아예 안보이게
				//   하던지 해야지 판떼기로 가리려고 하지 않았으면"). 높이를 인형 머리 위(0.9)에서
				//   땅 바로 위로 내린다 — 못 본 자리의 *개체*는 렌더러를 꺼서 감춘다(ApplyEnemyVisibility).
				fogView = TowerDefenseFogView.Create(
					stageRoot, mapLayout.Width, mapLayout.Length, activeGroundWidth, activeGroundLength, stage.FogHeight);
				RefreshVision();
			}
		}



		/// <summary>
		/// 화면에서 즉시 읽히게 만드는 공통 처리 — 역할 색 + 한 칸 크기 + 애니메이터 정지.
		///
		/// ★ 애니메이터 정지가 핵심(실측): 프리팹 '[Sprite] Unit' 에 슬라임 애니메이터가 붙어 있어
		///   매 프레임 sprite 를 자기 클립으로 덮어쓴다 → 유닛 데이터의 그림을 아무리 넣어도 다음
		///   프레임에 슬라임으로 되돌아갔다(사용자 실증 2회 "여전히 슬라임"). 끄지 않으면 어떤 시각
		///   구분도 무의미.
		/// ★ 색 = 정체: 아트가 아직 없으므로 역할 4색을 서로 멀게 잡고, HUD 범례가 같은 색을 읽어
		///   화면에 이름을 띄운다(색↔이름 단일 소스 — 둘이 어긋나면 안내가 거짓말이 된다).
		/// ★ 크기 = 격자 한 칸: 칸보다 크면 서로 밀치고 소속도 안 읽힌다.
		/// </summary>
		/// <summary>
		/// 종류별 체력·속도 적용 — 기반 유닛 스탯에 배수를 씌운다. 새 유닛 에셋 없이 「단단한 놈/빠른 놈」이
		/// 성립하는 지점. HP_MAX_STAT(기반)까지 같이 올려야 이후 스탯 재계산이 원래 값으로 되돌리지 않는다.
		/// 리스는 ApplyReadability 가 이미 잡아뒀다(같은 스폰 경로) — 반납 시 원본 스탯으로 복원된다.
		/// </summary>
		private static void ApplyArchetypeStats(UnitObject unitObject, TowerDefenseEnemyArchetype archetype, float paceScale)
		{
			if (unitObject == null || archetype == null)
				return;

			if (Mathf.Approximately(archetype.HealthMultiplier, 1f) == false)
			{
				int scaledMax = Mathf.Max(1, Mathf.RoundToInt(unitObject.UnitStat[UnitStatType.HP_MAX] * archetype.HealthMultiplier));
				unitObject.UnitStat[UnitStatType.HP_MAX_STAT] = scaledMax;
				unitObject.UnitStat[UnitStatType.HP_MAX] = scaledMax;
				unitObject.UnitStat[UnitStatType.HP_CUR] = scaledMax;
			}

			// 판 전체 속도 배수 — 종류별 배수와 곱해진다(느린 놈은 더 느리게, 빠른 놈도 함께 느려진다).
			float paceMultiplier = archetype.SpeedMultiplier * paceScale;
			if (Mathf.Approximately(paceMultiplier, 1f) == false)
			{
				int scaledSpeed = Mathf.Max(1, Mathf.RoundToInt(unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] * paceMultiplier));
				unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] = scaledSpeed;
			}
		}

		private void ApplyReadability(UnitObject unitObject, Color tint, float scale)
		{
			if (unitObject == null)
				return;

			// 손대기 전 원본 스냅샷 — 반납 시 그대로 되돌린다(다시 시작해도 지난 매치 흔적 0).
			AcquireLease(unitObject);

			foreach (Animator animator in unitObject.GetComponentsInChildren<Animator>(true))
				animator.enabled = false;

			if (unitObject.SpriteRenderer != null)
				unitObject.SpriteRenderer.color = tint;

			unitObject.transform.localScale = Vector3.one * scale;

			// ★ 몸집을 키우면 *충돌 몸통도 같이 커진다* — 그러면 단단한 마수(1.35배)는 암반과 벽 사이
			//   좁은 틈에 끼어 나오지 못한다(사용자 실증: "단단한 마수 아까부터 껴서 못 움직인다").
			//   이동은 콜라이더를 쓸어서 미끄러지는 방식이라, 몸통이 한 칸보다 크면 길이 있어도 못 지난다.
			//   보이는 크기는 그대로 두고 *충돌 몸통만* 원래 굵기로 되돌린다 — 「단단함」은 체력이 말한다.
			CapsuleCollider capsule = unitObject.GetComponent<CapsuleCollider>();
			if (capsule != null && scale > 1f)
			{
				capsule.radius /= scale;
				capsule.height /= scale;
			}
		}

		/// <summary> 영웅과 지금 살아있는 마수들의 몸싸움을 서로 무시시킨다(길막 방지). </summary>
		private void IgnoreCollisionsWithEnemies(GameObject hero)
		{
			if (hero == null)
				return;

			foreach (ICombatant enemy in waveEnemies)
			{
				if (enemy is MonoBehaviour behaviour && behaviour != null)
					IgnorePair(hero, behaviour.gameObject);
			}
		}

		/// <summary> 이 마수와 영웅의 몸싸움을 무시시킨다 — 영웅이 이미 서 있을 때 태어난 마수용. </summary>
		private void IgnoreHeroCollision(GameObject enemy)
		{
			if (enemy == null || heroTransform == null)
				return;
			IgnorePair(heroTransform.gameObject, enemy);
		}

		private static void IgnorePair(GameObject left, GameObject right)
		{
			if (left == null || right == null || left == right)
				return;

			Collider[] leftColliders = left.GetComponentsInChildren<Collider>(true);
			Collider[] rightColliders = right.GetComponentsInChildren<Collider>(true);
			foreach (Collider leftCollider in leftColliders)
			{
				foreach (Collider rightCollider in rightColliders)
				{
					if (leftCollider != null && rightCollider != null)
						Physics.IgnoreCollision(leftCollider, rightCollider, true);
				}
			}
		}

		/// <summary> 대여 계약 부착 + 원본 스냅샷 — 멱등(이미 붙어 있으면 재사용, 스냅샷은 최초 1회만). </summary>
		private static void AcquireLease(UnitObject unitObject)
		{
			TowerDefenseUnitLease lease = unitObject.GetComponent<TowerDefenseUnitLease>();
			if (lease == null)
				lease = unitObject.gameObject.AddComponent<TowerDefenseUnitLease>();
			lease.Acquire(unitObject);
		}

		/// <summary>
		/// 풀 반납 단일 경로 — 반납 *전에* 원상복구(<see cref="TowerDefenseUnitLease.Release"/>).
		/// 이걸 거치지 않고 Despawn 하면 다음 매치가 지난 매치의 색·크기·정지된 애니메이터·역할 드라이버를
		/// 그대로 물려받는다(코어/포탑/채집/마수가 같은 프리팹 = 같은 풀이라 역할까지 섞인다).
		/// </summary>
		private static void ReleaseUnit(ObjectPoolManager targetPool, GameObject unit)
		{
			if (unit == null)
				return;

			TowerDefenseUnitLease lease = unit.GetComponent<TowerDefenseUnitLease>();
			if (lease != null)
				lease.Release(unit.GetComponent<UnitObject>());

			// ★ **끈 것은 켜서 돌려준다.** 서식지 목줄이 전술을 잠시 꺼두는데, 그 상태로 풀에 들어가면
			//   그 몸을 재사용한 *다음 마수*가 꺼진 채로 태어나 영영 안 움직인다 — 한 마리만 굳어도
			//   파도가 안 끝나던 그 사고와 같은 종류다. 풀은 남의 상태를 기억하면 안 된다.
			TacticDriver driver = unit.GetComponent<TacticDriver>();
			if (driver != null)
				driver.enabled = true;

			// ★ 소속도 끊어서 돌려준다. 안 끊으면 이 몸으로 되살아난 *파도 마수*를 옛 서식지가
			//   집으로 끌어당긴다(실측 「집에서 95~123, 목줄 20」). 반납 지점이 여기 하나뿐이라
			//   여기서 끊는 것이 빠뜨릴 자리가 없는 유일한 방법이다.
			TowerDefenseLairMember member = unit.GetComponent<TowerDefenseLairMember>();
			if (member != null)
				member.Leave();

			targetPool.Despawn(unit);
		}



		/// <summary>
		/// 마수 출현 표시 — 어디서 적이 들어오는지 모르면 방어선을 세울 수가 없다.
		/// 사용자 실증: 자원 노드 원을 "몬스터 나오는 원" 으로 오인했다. 원인은 ① 출현 지점에 아무
		/// 표시가 없었고 ② 자원 노드가 출현선 바로 앞(z=14 vs 출현 z=15)에 깔려 있어서 — 즉
		/// *표시 부재* + *배치 오류* 가 겹쳤다. 출현 지점에 붉은 표식을 세워 둘을 확실히 가른다.
		/// 노드(금빛 원반)와 형태·색을 다르게 해야 혼동이 안 난다.
		/// </summary>
		// ── 마수 둥지(출현지) ─────────────────────────────────────────────────────
		// ★ 왜 부술 수 있어야 하나 (사용자 지시: "적유닛이 나오는 곳도 뭔가 부술 수 있거나 나오는 적이
		//   한정되어야 할듯"): 무한히 쏟아지는 출구를 못 막으면 방어는 영원히 수세다. 둥지를 부수면
		//   그쪽 출구가 닫힌다 — 「버틴다」에서 「밀어낸다」로 게임의 동사가 하나 늘어난다.
		// ★ 왜 마수 프리팹으로 세우나: 포탑은 *마수 목록에 있는 것*만 쏜다. 둥지를 같은 종류로 세우면
		//   조준·피해·격파 보상 경로를 하나도 새로 만들지 않고 그대로 재사용한다.
		private readonly List<(MatchCombatant Combatant, Vector3 LocalPosition)> nests = new();
		// 둥지인지 즉시 알기 위한 집합 — 「쏠 대상」과 「쳐들어오는 마수」를 가르는 기준.
		private readonly HashSet<MatchCombatant> nestCombatants = new();
		private bool nestsEverSpawned; // 처음부터 둥지가 없던 판(옛 방식)을 「전멸」로 오인하지 않게.

		// 이 판에서 부순 둥지 자리 — 이어할 때 그 자리엔 다시 안 선다.
		private readonly List<Vector3> destroyedNestPositions = new();

		private bool IsNestAlreadyDestroyed(Vector3 localPosition)
		{
			foreach (Vector3 destroyed in destroyedNestPositions)
			{
				if ((destroyed - localPosition).sqrMagnitude <= 1f)
					return true;
			}
			return false;
		}

		private IEnumerator SpawnNestsRoutine()
		{
			if (stage.NestHealthMultiplier <= 0f || stage.EnemyUnit == null || stage.EnemyUnit.Prefab == null)
				yield break;

			foreach (Vector3 localPosition in new List<Vector3>(activeSpawnPoints))
			{
				// ★ 이미 부순 둥지는 다시 서지 않는다 — 안 그러면 이어할 때마다 부순 것이 되살아나
				//   「부술 수 있다」가 헛수고가 된다(부순 자리는 저장에 적혀 있다).
				if (IsNestAlreadyDestroyed(localPosition))
					continue;

				SpawnedUnit spawned = new();
				yield return SpawnUnitRoutine(stage.EnemyUnit, stageRoot.TransformPoint(localPosition),
					ATTACKER_TEAM, stage.NestTint, stage.NestScale, spawned);
				if (spawned.Ok == false)
					continue;

				GameObject nestObject = spawned.GameObject;
				UnitObject nestUnit = spawned.UnitObject;
				MatchCombatant nestCombatant = spawned.Combatant;

				yield return null;
				if (core == null)
					yield break;

				// 둥지는 걷지 않는다 — 이동을 끄고 자리에 못 박는다(브레인은 세우는 문이 이미 껐다).
				UnityEngine.AI.NavMeshAgent nestAgent = nestObject.GetComponent<UnityEngine.AI.NavMeshAgent>();
				if (nestAgent != null)
					nestAgent.enabled = false;
				UnitMovement nestMovement = nestObject.GetComponent<UnitMovement>();
				if (nestMovement != null)
					nestMovement.enabled = false;

				int nestHp = Mathf.Max(1, Mathf.RoundToInt(
					nestUnit.UnitStat[UnitStatType.HP_MAX] * stage.NestHealthMultiplier * difficulty.NestHealthScale));
				nestUnit.UnitStat[UnitStatType.HP_MAX] = nestHp;
				nestUnit.UnitStat[UnitStatType.HP_CUR] = nestHp;

				// 표적 등록은 세우는 문이 이미 했다 — 여기서 또 하면 같은 것이 목록에 두 번 들어간다.
				waveEnemies.Add(nestCombatant); // 포탑이 쏘는 대상 목록 — 둥지도 여기 있어야 맞는다.
				nests.Add((nestCombatant, localPosition));
				nestCombatants.Add(nestCombatant);
			}

			nestsEverSpawned = nests.Count > 0;
			Debug.Log($"{nameof(TowerDefenseMatch)}: 마수 둥지 {nests.Count}곳 — 전부 부수면 개척 성공.");
		}

		// 잠들어 있는 서식지 마수 — 깨어나기 전까지는 걷지도 때리지도 않는다.
		/// <summary> 서식지 번호 발급기 — 판이 새로 시작돼도 옛 번호와 안 겹치게 계속 는다. </summary>
		private int lastLairId;

		private sealed class SleepingLair
		{
			public int Id;
			public Vector3 WorldPosition;
			public readonly List<UnitObject> Guards = new();
			public readonly List<TacticDriver> Drivers = new();
			public bool Awake;
			public bool Cleared; // 보상은 한 번만 — 안 그러면 빈 서식지가 매 프레임 정수를 찍어낸다.
		}

		// 「지금 어디서 무슨 일이 났나」 — 화면 밖 사건을 가장자리 표식으로 알린다.
		private readonly TowerDefenseAlerts alerts = new();

		// 부서진 자리를 알리려면 *부서지기 전* 자리를 알아야 한다 — 사라진 뒤엔 물어볼 데가 없다.
		private readonly Dictionary<Transform, Vector3> lastBuildingPositions = new();

		/// <summary>
		/// 이어하기 복원이 도는 중인가 — 끝나기 전에 「건물 수가 다르다」를 재면 *멀쩡한 복원*을 결함으로 잡는다.
		/// (실측: 복원이 한 프레임씩 양보하며 도는 동안 하네스가 중간값을 읽어 거짓 실패를 냈다.)
		/// </summary>
		public bool RestoreInProgress { get; private set; }

		/// <summary>
		/// 알림을 하나 띄운다(검증 전용) — 화면 표식이 *실제로 뜨는지*는 사건이 나야만 잴 수 있는데,
		/// 서식지가 깨어나거나 건물이 부서지는 것을 하네스가 만들어내긴 어렵다. 알림 자체가 화면까지
		/// 도달하는지만 여기서 확인한다(사건 발생 경로는 그 위에 얹힌 별개 문제).
		/// </summary>
		public void RaiseAlertForVerification(string label)
		{
			if (stage == null || coreCombatant == null)
				return;
			alerts.Raise(label, coreCombatant.Position, Time.time, stage.AlertSeconds);
		}

		/// <summary>
		/// 서식지 하나를 강제로 깨운다(검증 전용) — 깨어난 마수가 *어디로 가는지*는 깨워봐야 잴 수 있다.
		/// 깨운 서식지의 자리를 돌려준다(못 깨웠으면 false).
		/// </summary>
		public bool WakeNearestLairForVerification(out Vector3 lairPosition)
		{
			lairPosition = Vector3.zero;

			// ★ 이름이 「가장 가까운」인데 실제로는 *목록의 첫 번째*를 깨우고 있었다. 그래서 판 반대편
			//   서식지가 뽑혀 「코어까지 102」 같은 값이 나왔고, 그걸 근거로 「서식지가 너무 멀다」고
			//   의심했다 — 실제 가장 가까운 것은 16 이었다. **이름이 거짓말하면 측정이 거짓말한다.**
			SleepingLair best = null;
			float bestDistance = float.MaxValue;
			Vector3 from = coreCombatant != null ? coreCombatant.Position : Vector3.zero;

			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake)
					continue;
				float distance = Vector3.Distance(lair.WorldPosition, from);
				if (distance >= bestDistance)
					continue;
				bestDistance = distance;
				best = lair;
			}

			if (best == null)
				return false;

			lairPosition = best.WorldPosition;
			WakeLair(best);
			return true;
		}

		/// <summary>
		/// 깨어난 서식지 마수들이 지금 코어에서 얼마나 떨어져 있나(평균). 시간에 따라 이 값이 줄면
		/// 「코어로 행진한다」, 제자리면 「그 일대를 지킨다」 — 둘은 완전히 다른 게임이다.
		/// </summary>
		public float AwakenedGuardDistanceToCore() => AwakenedGuardDistanceToCore(out _);

		/// <summary> 같은 값 + *몇 기를 재고 있나*. 0 기면 「가까워졌다」가 아니라 「죽어서 없다」다. </summary>
		public float AwakenedGuardDistanceToCore(out int aliveGuards) => AwakenedGuardDistanceToCore(out aliveGuards, out _, out _);

		/// <summary>
		/// 같은 값 + **사라진 방식**까지. 「참조가 비었다(파괴)」와 「꺼져 있다(풀 반납)」는 원인이 전혀 다르다 —
		/// 숫자 하나만 보면 둘이 똑같이 「없다」로 보여서 엉뚱한 데를 파게 된다.
		/// </summary>
		public float AwakenedGuardDistanceToCore(out int aliveGuards, out int destroyedGuards, out int disabledGuards)
		{
			aliveGuards = 0;
			destroyedGuards = 0;
			disabledGuards = 0;
			if (coreCombatant == null)
				return -1f;

			float total = 0f;
			int count = 0;
			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake == false)
					continue;
				foreach (UnitObject guard in lair.Guards)
				{
					if (guard == null)
					{
						destroyedGuards++;
						continue;
					}
					if (guard.gameObject.activeInHierarchy == false)
					{
						disabledGuards++;
						continue;
					}
					total += Vector3.Distance(guard.transform.position, coreCombatant.Position);
					count++;
				}
			}
			aliveGuards = count;
			return count > 0 ? total / count : -1f;
		}

		/// <summary>
		/// 깨어난 마수가 *제 서식지에서* 얼마나 멀어졌나(최대). 「지킨다」의 진짜 판정은 이것이다 —
		/// 코어까지의 거리로 재면 서식지가 원래 코어에 가까웠는지 멀었는지에 답이 좌우된다.
		/// </summary>
		public float AwakenedGuardDistanceFromHome()
		{
			float worst = -1f;
			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake == false)
					continue;
				foreach (UnitObject guard in lair.Guards)
				{
					if (guard == null || guard.gameObject.activeInHierarchy == false)
						continue;
					float distance = Vector3.Distance(guard.transform.position, lair.WorldPosition);
					if (distance > worst)
						worst = distance;
				}
			}
			return worst;
		}

		/// <summary>
		/// 그 자리를 밝힌다(검증 전용) — 「밝힌 서식지만 지도에 뜬다」는 규칙 때문에, 밝히지 않으면
		/// 그 표시를 영영 못 잰다(못 잰 것을 통과로 세면 검사가 있으나 마나다).
		/// </summary>
		public void RevealForVerification(Vector3 worldPosition, float radius)
		{
			AddVisionSource(worldPosition, radius);
		}

		/// <summary> 미니맵이 읽는 서식지 표식 — 자리와 「깨어났나」. </summary>
		public readonly struct LairMarker
		{
			public readonly Vector3 Position;
			public readonly bool Awake;

			public LairMarker(Vector3 position, bool awake)
			{
				Position = position;
				Awake = awake;
			}
		}

		private readonly List<LairMarker> lairMarkers = new();

		/// <summary>
		/// 서식지 자리 목록. **밝힌 것만 그리는 판단은 화면이 한다**(시야 규칙은 화면 공통).
		///
		/// ★ 왜 따로 내주나: 잠든 마수도 마수 목록에 있어서 미니맵이 「코어로 오는 중」이라는 *거짓말*을
		///   붙이고 있었다. 잠든 무리와 몰려오는 무리는 대응이 정반대다(피한다 / 막는다) —
		///   같은 점으로 그리면 「깨울지 말지」를 계산할 수가 없다.
		/// </summary>
		public IReadOnlyList<LairMarker> LairMarkers
		{
			get
			{
				lairMarkers.Clear();
				foreach (SleepingLair lair in lairs)
					lairMarkers.Add(new LairMarker(lair.WorldPosition, lair.Awake));
				return lairMarkers;
			}
		}

		/// <summary> 그 마수가 아직 잠든 서식지 소속인가 — 미니맵이 마수 점에서 걸러낸다. </summary>
		public bool IsSleepingLairGuard(MatchCombatant combatant)
		{
			if (combatant == null)
				return false;

			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake)
					continue;
				foreach (UnitObject guard in lair.Guards)
				{
					if (guard != null && guard.gameObject == combatant.gameObject)
						return true;
				}
			}
			return false;
		}

		/// <summary>
		/// 전술이 꺼진 채 살아 있는 마수 수 — 0 이 아니면 누군가 상태를 켜서 안 돌려준 것이다.
		/// (목줄이 잠시 끄는 것은 *서식지 마수*뿐이고 그건 제 자리를 지키는 중이라 정상이므로 뺀다.)
		/// </summary>
		public int FrozenEnemyCount
		{
			get
			{
				int frozen = 0;
				foreach (MatchCombatant enemy in waveEnemies)
				{
					if (enemy == null || enemy.IsAlive == false)
						continue;
					if (IsSleepingLairGuard(enemy) || IsAwakenedLairGuard(enemy))
						continue;

					TacticDriver driver = enemy.GetComponent<TacticDriver>();
					if (driver != null && driver.enabled == false)
						frozen++;
				}
				return frozen;
			}
		}

		/// <summary> 깨어난 서식지 소속인가 — 목줄이 그 전술을 잠시 끌 수 있어 굳음 판정에서 뺀다. </summary>
		private bool IsAwakenedLairGuard(MatchCombatant combatant)
		{
			if (combatant == null)
				return false;

			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake == false)
					continue;
				foreach (UnitObject guard in lair.Guards)
				{
					if (guard != null && guard.gameObject == combatant.gameObject)
						return true;
				}
			}
			return false;
		}

		/// <summary>
		/// 안내가 「길 없음」으로 끝난 횟수 — 앞을 막은 것을 부수러 붙는 중이면 정상이고,
		/// 아무도 안 부수고 서 있으면 판이 안 끝난다. 이 값과 「굳은 마수 수」를 같이 봐야 가려진다.
		/// </summary>
		public int NavigatorNoPathCount => flowNavigator is TowerDefensePathNavigator pathNavigator
			? pathNavigator.NoPathCount
			: 0;

		/// <summary>
		/// 신호를 받고 있는 중계(발전 인형) 수 — **컨트롤넷의 핵심 약속이 실제로 서는지**의 유일한 증거.
		/// 0 이면 사슬이 한 칸도 안 뻗은 것이고, 그러면 전기는 「코어 반경 안」이 전부다.
		/// </summary>
		public int FedRelayCount
		{
			get
			{
				int fed = 0;
				// 0 번은 코어(스스로 낸다) — 중계만 센다.
				for (int index = 1; index < powerGrid.Field.NodeCount; index++)
				{
					if (powerGrid.Field.IsFed(index))
						fed++;
				}
				return fed;
			}
		}

		/// <summary> 길찾기가 상한에 걸려 포기한 횟수 — 0 이 아니면 갈 길이 있는데도 못 가는 마수가 있다. </summary>
		public int PathCapHits => gridPath != null ? gridPath.CapHits : 0;

		/// <summary> 한 번의 길찾기에서 가장 많이 펼친 칸 수 — 상한(기본 4000)에 얼마나 가까운지. </summary>
		public int PathPeakCells => gridPath != null ? gridPath.PeakExpandedCells : 0;

		/// <summary> 판 크기(칸) — 상한이 판에 비해 충분한지 함께 봐야 판단이 된다. </summary>
		public int MapCellCount => mapLayout != null ? mapLayout.Width * mapLayout.Length : 0;

		/// <summary> 지금 판에 깔려 있는 함정 수 — 이어하기가 함정을 잃는지 하네스가 직접 센다. </summary>
		public int TrapCount => stageRoot != null ? stageRoot.GetComponentsInChildren<TowerDefenseTrap>(true).Length : 0;

		/// <summary> 지금 판의 벽 칸 수 — 같은 이유. </summary>
		public int WallCellCount => wallCells.Count;

		/// <summary> 화면이 읽는 알림 목록. </summary>
		public IReadOnlyList<TowerDefenseAlerts.Alert> Alerts => alerts.Active;

		// ── 라이브 검증용 창 ─────────────────────────────────────────────────────────
		// ★ 「돌아간다」를 사람 눈에만 맡기면 영영 안 재게 된다. 하네스가 판을 돌리며 직접 물어볼 수
		//   있어야 신호·서식지·침공이 *실제로* 살아 있는지 매번 확인된다(안 그러면 컴파일만 초록).
		public float CoreSignalCharge => powerGrid.Field.ChargeAt(0);
		public float CoreSignalRadius => powerGrid.Field.LiveRadiusAt(0);
		public int SignalNodeCount => powerGrid.Field.NodeCount;
		public int SleepingLairCount => lairs.Count;
		public int InvasionFrontCount => invasionFront.Count;

		private readonly List<SleepingLair> lairs = new();
		private readonly List<Vector3> lairWakeProbe = new();

		/// <summary> 지금까지 깨운 서식지 수 — 결과 기록판이 「얼마나 파고들었나」를 말한다. </summary>
		public int LairsAwakened { get; private set; }

		/// <summary> 그중 *소리만으로* 깨어난 수 — 거리로 깬 것과 갈라야 소리 규칙을 잴 수 있다. </summary>
		public int LairsAwakenedByNoise { get; private set; }

		/// <summary>
		/// 판 곳곳에 잠든 마수를 깐다 (TASK-WM-194, 데아빌 레퍼런스).
		///
		/// ★ 파도만 있으면 파도 사이의 판 안쪽은 완전히 안전해서, 넓히기를 미루는 데 아무 대가가 없다.
		///   잠든 것이 깔려 있으면 **넓히는 행위 자체가 위험**이 된다 = 「개척」이 성립한다.
		/// </summary>
		private IEnumerator SpawnLairsRoutine()
		{
			lairs.Clear();
			LairsAwakened = 0;
			LairsCleared = 0;
			// ★ 「바뀔 때만 알린다」용 기억도 판마다 비운다. 안 비우면 새 판이 옛 판의 상태를 이어받아
			//   *같은 일이 처음 일어나도 알리지 않는다*(적응) 또는 *한참 뒤까지 안 알린다*(강도).
			lastAdaptationNote = string.Empty;
			lastPressureStep = -1;

			// ★ 알림과 「건물이 서 있던 자리」 기억도 판마다 비운다.
			//   안 비우면 새 판 첫 틱에 *옛 판 건물들*이 전부 「내 것이 부서졌다」로 뜬다 —
			//   판이 끝나며 청산된 것을 적이 부순 것으로 오인한다(시작하자마자 거짓 경고 넷).
			alerts.Clear();
			lastBuildingPositions.Clear();
			breach.Clear(); // 새 판은 새 판이다 — 지난 판에서 뚫린 자리가 방향을 끌면 안 된다.
			noise.Clear(); // 지난 판의 소리가 새 판의 둥지를 깨우면 안 된다(판 넘는 상태를 여기서 세 번 잡았다).

			if (stage == null || stage.LairCount <= 0 || mapLayout == null || stage.EnemyUnit == null)
				yield break;

			List<Vector2Int> cells = new();
			TowerDefenseLairPlacement.Choose(
				mapLayout.Seed,
				mapLayout.Width,
				mapLayout.Length,
				mapLayout.CoreCell,
				cell => mapLayout.IsBlocked(cell),
				stage.LairCount,
				stage.LairMinCoreDistance,
				stage.LairMinSpacing,
				cells);

			foreach (Vector2Int cell in cells)
			{
				Vector3 localPosition = mapLayout.CellToWorld(cell);
				// 번호는 판 안에서만 유일하면 된다 — 소속 표가 이 번호로 「내 집인가」를 가른다.
				SleepingLair lair = new()
				{
					Id = ++lastLairId,
					WorldPosition = stageRoot.TransformPoint(localPosition),
				};

				for (int guard = 0; guard < stage.LairGuardCount; guard++)
				{
					// 한 자리에 겹쳐 세우면 서로의 몸에 끼어 못 나온다 — 둘레로 조금씩 벌린다.
					float angle = guard * Mathf.PI * 2f / Mathf.Max(1, stage.LairGuardCount);
					Vector3 offset = new(Mathf.Cos(angle) * stage.EnemySpawnSpread, 0f, Mathf.Sin(angle) * stage.EnemySpawnSpread);

					SpawnedUnit spawned = new();
					yield return SpawnUnitRoutine(stage.EnemyUnit, stageRoot.TransformPoint(localPosition + offset),
						ATTACKER_TEAM, stage.LairSleepTint, stage.EnemyScale, spawned);
					if (spawned.Ok == false)
						continue;

					yield return null;
					if (core == null || targeting == null || pool == null)
						yield break;

					// 잠든 동안은 걷지 않는다 — 브레인은 세우는 문이 이미 껐고, 여기서 이동만 못 박는다.
					UnitMovement movement = spawned.GameObject.GetComponent<UnitMovement>();
					if (movement != null)
						movement.enabled = false;

					IgnoreHeroCollision(spawned.GameObject);
					lair.Guards.Add(spawned.UnitObject);
					// 소속을 *몸에* 붙인다 — 목록만으로는 풀에서 되살아난 남의 몸을 못 가른다.
					TowerDefenseLairMember member = spawned.GameObject.GetComponent<TowerDefenseLairMember>();
					if (member == null)
						member = spawned.GameObject.AddComponent<TowerDefenseLairMember>();
					member.Join(lair.Id);
					waveEnemies.Add(spawned.Combatant); // 포탑이 쏘는 대상 — 잠들었어도 때릴 수는 있다.
					enemyBountyById[spawned.Combatant.CombatantId] = core.BountyPerKill;
				}

				if (lair.Guards.Count > 0)
					lairs.Add(lair);
			}

			Debug.Log($"{nameof(TowerDefenseMatch)}: 서식지 {lairs.Count}곳이 잠들어 있다 — 가까이 가면 깨어난다.");
		}

		/// <summary>
		/// 내 것이 가까이 왔으면 서식지를 깨운다. 깨어난 마수는 보통 마수와 똑같이 움직인다.
		///
		/// ★ 「가까이 가면 깬다」여야 넓히는 것이 위험이 된다 — 처음부터 다 깨어 있으면 파도가 하나 더
		///   있는 것이고, 영영 안 깨면 판을 장식하는 조형물이다.
		/// </summary>
		private void WakeNearbyLairs()
		{
			if (lairs.Count == 0 || stage == null || stage.LairWakeRadius <= 0f)
				return;

			lairWakeProbe.Clear();
			foreach (Transform building in supplyChain.Buildings)
			{
				if (building != null)
					lairWakeProbe.Add(building.position);
			}
			if (heroTransform != null)
				lairWakeProbe.Add(heroTransform.position); // 영웅이 정찰 나가는 것도 건드리는 것이다.

			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake)
					continue;

				bool tooClose = TowerDefenseLairPlacement.ShouldWake(
					lair.WorldPosition, lairWakeProbe, stage.LairWakeRadius);

				// ★ 거리만 보면 「멀찍이서 조용히 크는 것」과 「바로 옆에서 난사하는 것」이 똑같이
				//   안전하다 — 개척의 위험이 거리 하나로 납작해진다. 소리도 깨운다:
				//   짓고, 쏘고, 얻어맞는 소리가 마수를 부른다(데아빌의 축은 거리가 아니라 내 행동이다).
				bool tooLoud = stage.NoiseWakeThreshold > 0f
					&& noise.LevelAt(lair.WorldPosition, stage.NoiseHearingRadius) >= stage.NoiseWakeThreshold;

				if (tooClose == false && tooLoud == false)
					continue;

				// ★ 「소리 때문」과 「가까이 갔기 때문」은 사람에게 다른 사건이다. 가까이 간 건 스스로
				//   아는데(내가 걸어갔다), 소리는 *멀리서* 일어난 일이라 말해 주지 않으면 이유를 모른다.
				//   그래서 소리만으로 깬 경우에만 알린다 — 그리고 그 수를 따로 센다.
				//   둘을 안 세면 검사가 「소리로 깼나 거리로 깼나」를 영영 못 가른다(실측에서 막혔다).
				bool byNoise = tooLoud && tooClose == false;
				if (byNoise)
					LairsAwakenedByNoise++;

				WakeLair(lair, byNoise);
			}
		}

		/// <summary>
		/// 내 것이 부서졌으면 그 자리를 알린다.
		///
		/// ★ 부서진 *뒤에는* 자리를 물어볼 데가 없다(참조가 비어 버린다). 그래서 살아 있는 동안
		///   마지막 자리를 계속 적어 둔다 — 이게 없으면 「어딘가 부서졌다」까지만 알고 어디인지 모른다.
		/// ★ 이 장르에서 사람들이 가장 많이 꼽는 불만이 「무슨 일이 났는지 안 알려준다」였다.
		///   화면 밖 한 곳이 뚫리는 것을 못 보면, 알아챘을 땐 이미 늦는다.
		/// </summary>
		private void TrackLostBuildings()
		{
			if (stage == null)
				return;

			alerts.Prune(Time.time);
			breach.Tick(Time.deltaTime, stage.BreachCoolPerSecond); // 한 번 실수가 영원한 벌이 되면 안 된다.
			noise.Tick(Time.deltaTime, stage.NoiseDecayPerSecond); // 소리는 잦아든다 — 조용해질 기회가 있어야 한다.

			foreach (Transform building in supplyChain.Buildings)
			{
				if (building != null)
					lastBuildingPositions[building] = building.position;
			}

			List<Transform> lost = null;
			foreach (KeyValuePair<Transform, Vector3> tracked in lastBuildingPositions)
			{
				if (tracked.Key != null)
					continue;

				alerts.Raise("내 것이 부서졌다", tracked.Value, Time.time, stage.AlertSeconds);
				// 부서진 자리는 잊히지 않는다 — 다음 파도가 이쪽으로 끌린다.
				// ★ 그리고 그걸 *말해 준다*. 방향만 조용히 바꾸면 사람은 「이번엔 왜 여기로 오지」만
				//   남고 자기 선택과 결과를 못 잇는다 — 안 보이는 규칙은 없는 규칙이다.
				//   처음 뜨거워지는 순간 딱 한 번만 외친다(잃을 때마다 외치면 급한 알림을 덮는다).
				// 무너지는 소리가 가장 크다 — 이게 소리 사태의 시작점이다.
				EmitNoise(tracked.Value, stage.NoiseFromLoss);
				if (breach.Add(tracked.Value, stage.BreachMergeDistance, stage.BreachHeatPerLoss))
					alerts.Raise("뚫린 곳을 다시 노린다", tracked.Value, Time.time, stage.AlertSeconds);
				lost ??= new List<Transform>();
				lost.Add(tracked.Key);
			}

			if (lost == null)
				return;
			foreach (Transform gone in lost)
				lastBuildingPositions.Remove(gone);
		}

		/// <summary>
		/// 깨어난 서식지 마수를 제 자리에 묶어 둔다.
		///
		/// ★ 실측으로 잡았다: 깨우면 8초에 코어 쪽으로 58 만큼 다가갔다(101 → 43). 그러면 서식지는
		///   「파도 하나 더」일 뿐이고, 「넓히는 행위 자체가 위험」이라는 이 기능의 존재 이유가 사라진다.
		///   *그 자리를 지켜야* 「저기 자는 걸 깨우면 저기가 위험해진다」가 성립한다.
		/// ★ 목줄 밖에서는 전술을 잠시 끄고 집으로 몬다 — 켜둔 채 방향만 덮어쓰면 같은 프레임에
		///   전술이 다시 코어를 겨눠 서로 밀치며 덜덜 떤다(어느 쪽이 나중에 도는지에 결과가 달림).
		/// </summary>
		private void TickLairLeash()
		{
			if (stage == null || stage.LairLeashRadius <= 0f || lairs.Count == 0)
				return;

			float leash = stage.LairLeashRadius;
			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake == false)
					continue;

				for (int index = lair.Guards.Count - 1; index >= 0; index--)
				{
					UnitObject guard = lair.Guards[index];
					if (guard == null)
					{
						lair.Guards.RemoveAt(index);
						continue;
					}

					// ★ 죽은 마수의 몸은 풀로 돌아가 *다른 곳에서 다른 마수로* 되살아난다. 그런데 이 목록이
					//   그 몸을 계속 들고 있으면, 테두리에서 막 나온 파도 마수를 이 서식지가 집으로 끌어당긴다
					//   — 실측에서 「집에서 123 (목줄 20)」이 그것이었다. 죽는 순간 목록에서 뺀다.
					MatchCombatant combatant = guard.GetComponent<MatchCombatant>();
					if (combatant == null || combatant.IsAlive == false)
					{
						lair.Guards.RemoveAt(index);
						continue;
					}

					// ★ 살아 있다고 내 식구인 것은 아니다 — 죽었다가 풀에서 *다른 마수로* 되살아난
					//   몸은 멀쩡히 살아 있다. 소속 표를 봐야 가른다(「죽었으면 뺀다」로는 못 막았다).
					TowerDefenseLairMember member = guard.GetComponent<TowerDefenseLairMember>();
					if (member == null || member.LairId != lair.Id)
					{
						lair.Guards.RemoveAt(index);
						continue;
					}

					if (guard.gameObject.activeInHierarchy == false)
						continue;

					Vector3 toHome = lair.WorldPosition - guard.transform.position;
					bool tooFar = toHome.sqrMagnitude > leash * leash;

					TacticDriver driver = guard.GetComponent<TacticDriver>();
					if (driver != null && driver.enabled == tooFar)
						driver.enabled = tooFar == false;

					if (tooFar == false)
						continue;

					UnitMovement movement = guard.GetComponent<UnitMovement>();
					if (movement != null)
						movement.SetMoveDirection(toHome.normalized);
				}
			}
		}

		// 마지막으로 알린 적응 — 같은 말을 매 프레임 다시 띄우지 않기 위해.
		private string lastAdaptationNote = string.Empty;

		/// <summary>
		/// 마수가 무엇에 익숙해졌는지 알린다.
		///
		/// ★ 적응은 *판을 바꾸는 규칙*이다(한 수단에만 기대면 그 수단이 덜 먹힌다). 그런데 그걸 그리던
		///   칸이 숨겨지면서 화면 어디에도 안 나오게 됐다 — 그러면 사람은 자기 포탑이 고장 났다고 여긴다
		///   (이 규칙을 처음 넣을 때 정본 주석에 적어 둔 그대로다: 「안 보이는 규칙은 없는 규칙이다」).
		/// ★ 숫자가 아니라 **말**로 알린다("광역에 익숙해졌다") — 판이 도는 중에 수치를 늘어놓지
		///   않기로 한 결정과 어긋나지 않는다.
		/// ★ 바뀔 때만 한 번 — 매 프레임 띄우면 다른 알림을 덮어 정작 급한 것을 가린다.
		/// </summary>
		private void AnnounceAdaptation()
		{
			if (stage == null || coreCombatant == null)
				return;

			string note = TowerDefenseAdaptation.Describe(Adaptation);
			if (note == lastAdaptationNote)
				return;

			lastAdaptationNote = note;
			if (string.IsNullOrEmpty(note))
				return;

			// "광역에 익숙함" → "마수가 광역에 익숙해졌다".
			string spoken = "마수가 " + note.Replace("에 익숙함", "에 익숙해졌다");
			alerts.Raise(spoken, coreCombatant.Position, Time.time, stage.AlertSeconds);
			Debug.Log($"{nameof(TowerDefenseMatch)}: 적응 — {spoken}");
		}

		// 마지막으로 알린 강도 단계 — 같은 단계를 다시 알리지 않기 위해.
		private int lastPressureStep = -1;

		/// <summary>
		/// 시간이 올린 마수 강도를 알린다.
		///
		/// ★ 이것도 숨긴 칸에 얹혀 있다가 같이 묻힌 규칙이다(적응·파도 성격에 이어 셋째). 시간이 지나면
		///   같은 마수가 더 단단해지는데, 그걸 모르면 「내 포탑이 약해졌다」로 읽는다 — 원인을 정반대로 짚는다.
		/// ★ 숫자가 아니라 **말**로, 그리고 *오를 때만* 알린다. 매 프레임 수치를 띄우면 판이 도는 중에
		///   숫자를 안 늘어놓기로 한 결정을 되돌리는 셈이 된다.
		/// </summary>
		private void AnnouncePressure()
		{
			if (stage == null || stage.PressureAnnounceStep <= 0f || coreCombatant == null)
				return;

			int step = TowerDefenseAlerts.StepFor(Pressure, 1f, stage.PressureAnnounceStep);
			if (step <= lastPressureStep)
			{
				if (step < lastPressureStep)
					lastPressureStep = step; // 판이 새로 시작되면 되돌린다.
				return;
			}

			bool first = lastPressureStep < 0;
			lastPressureStep = step;
			if (first || step <= 0)
				return; // 판 시작의 기준선은 알릴 것이 아니다.

			alerts.Raise("마수가 더 단단해졌다", coreCombatant.Position, Time.time, stage.AlertSeconds);
			Debug.Log($"{nameof(TowerDefenseMatch)}: 강도 상승 — 마수 강도 {Pressure:F2}");
		}

		/// <summary> 지금 적응이 무엇이라 말하는가 — 하네스가 「보이는가」를 잴 때 기준으로 쓴다. </summary>
		public string AdaptationNote => TowerDefenseAdaptation.Describe(Adaptation);

		/// <summary>
		/// 다 쓸어낸 서식지에 보상을 준다.
		///
		/// ★ 왜 필요한가: 정수는 「바깥 노드까지 나가서 캐는 것」 하나에만 묶여 있었고, 그 길이 막히면
		///   강화가 통째로 잠긴다(사용자 실증: "초반에 연구 어떻게 하라는 겁니까"). 둥지를 부수는 길이
		///   이미 그 짝으로 있으므로, 서식지 소탕도 같은 자리에 둔다 —
		///   **캐서 버는 길과 싸워서 버는 길이 갈라져야** 어느 한쪽이 막혀도 판이 안 죽는다.
		/// ★ 깨운 적 없는 서식지는 세지 않는다. 안 그러면 판이 시작하자마자 「빈 서식지」로 오인될 수 있다.
		/// </summary>
		private void CollectClearedLairs()
		{
			if (stage == null || stage.LairClearEssenceReward <= 0 || core == null)
				return;

			foreach (SleepingLair lair in lairs)
			{
				if (lair.Cleared || lair.Awake == false)
					continue;

				bool anyAlive = false;
				foreach (UnitObject guard in lair.Guards)
				{
					if (guard != null && guard.gameObject.activeInHierarchy)
					{
						anyAlive = true;
						break;
					}
				}
				if (anyAlive)
					continue;

				lair.Cleared = true;
				LairsCleared++;
				int reward = Mathf.Max(0, Mathf.RoundToInt(stage.LairClearEssenceReward * boons.EssenceMultiplier));
				core.AddEssence(reward);
				PopWorldText("정수 +" + reward, lair.WorldPosition, TextType.Exp);
				alerts.Raise("서식지를 쓸었다", lair.WorldPosition, Time.time, stage.AlertSeconds);
				Debug.Log($"{nameof(TowerDefenseMatch)}: 서식지 소탕 — 정수 +{reward} (지금까지 {LairsCleared}곳).");
			}
		}

		/// <summary>
		/// 깨어난 서식지 한 곳을 쓸어낸 것으로 만든다(검증 전용) — 보상 경로는 *다 죽어야* 밟히는데,
		/// 하네스가 전투로 그걸 만들기는 어렵다. 규칙이 보는 조건(살아있는 게 없다)을 그대로 만들어
		/// 「다 쓸면 정수가 나오는가」만 확인한다.
		/// </summary>
		public bool ClearAwakenedLairForVerification()
		{
			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake == false || lair.Cleared)
					continue;

				foreach (UnitObject guard in lair.Guards)
				{
					if (guard != null)
						guard.gameObject.SetActive(false);
				}
				return true;
			}
			return false;
		}

		/// <summary> 쓸어낸 서식지 수 — 결과 기록판이 「얼마나 밀어냈나」를 말한다. </summary>
		public int LairsCleared { get; private set; }

		/// <param name="byNoise">
		/// 소리만으로 깼는가. ★ 한 사건에는 알림 하나여야 한다 — 예전엔 「소리를 듣고 깨어났다」를
		/// 띄운 직후 여기서 「서식지가 깨어났다」를 또 띄웠고, 둘이 같은 자리라 합쳐지면서
		/// *뒤엣것이 앞엣것을 덮었다*. 이유를 말하려고 띄운 문구가 조용히 사라진 것이다(실측으로 잡음).
		/// </param>
		private void WakeLair(SleepingLair lair, bool byNoise = false)
		{
			lair.Awake = true;
			LairsAwakened++;

			foreach (UnitObject guard in lair.Guards)
			{
				if (guard == null)
					continue;

				UnitMovement movement = guard.GetComponent<UnitMovement>();
				if (movement != null)
					movement.enabled = true;

				foreach (Renderer guardRenderer in guard.GetComponentsInChildren<Renderer>(true))
					guardRenderer.material.color = stage.EnemyTint; // 잠든 색을 벗는다 — 깨어난 것이 보여야 한다.

				TacticDriver driver = guard.GetComponent<TacticDriver>();
				if (driver == null)
					driver = guard.gameObject.AddComponent<TacticDriver>();
				driver.Initialize(stage.EnemyTactic, targeting, timeManager);
				driver.Navigator = flowNavigator;
				driver.StopsToAttack = false;
				drivers.Add(driver);
				lair.Drivers.Add(driver);
			}

			PopWorldText("깨어났다", lair.WorldPosition, TextType.Warning);
			alerts.Raise(byNoise ? "소리를 듣고 깨어났다" : "서식지가 깨어났다",
				lair.WorldPosition, Time.time, stage.AlertSeconds);
			Debug.Log($"{nameof(TowerDefenseMatch)}: 서식지 하나가 깨어났다 — 지금까지 {LairsAwakened}곳.");
		}

		/// <summary> 부서진 둥지의 출구를 닫는다 — 그 자리에서 더는 마수가 안 나온다. </summary>
		private void CullDestroyedNests()
		{
			for (int index = nests.Count - 1; index >= 0; index--)
			{
				(MatchCombatant combatant, Vector3 localPosition) = nests[index];
				if (combatant != null && combatant.IsAlive)
					continue;

				nests.RemoveAt(index);
				NestsDestroyed++;
				destroyedNestPositions.Add(localPosition); // 저장이 「어디를 부쉈나」를 적을 수 있게.

				// ★ 정수가 「바깥 채집」 하나에만 묶여 있으면 그 길이 막히는 순간 강화 전체가 잠긴다
				//   (이 작업에서 두 번 겪었다). 둥지를 부수는 것도 정수가 나오는 길이다 —
				//   *캐서 버는 길*과 *싸워서 버는 길*이 갈라지면 어느 한쪽이 막혀도 판이 안 죽는다.
				if (core != null && stage.NestEssenceReward > 0)
				{
					// ★ 「정수 수급」 카드를 여기 태운다. 카드는 뽑히는데 **걸리는 자리가 한 군데도 없어서**
					//   화면엔 「정수↑」라 적히고 실제로는 한 톨도 더 안 들어왔다(뽑으면 그 선택이 버려진다).
					core.AddEssence(Mathf.Max(0, Mathf.RoundToInt(stage.NestEssenceReward * boons.EssenceMultiplier)));
					PopWorldText("정수 +" + stage.NestEssenceReward, stageRoot.TransformPoint(localPosition), TextType.Exp);
				}
				activeSpawnPoints.Remove(localPosition);
				PopWorldText("둥지 파괴", stageRoot.TransformPoint(localPosition), TextType.Heal);
				Debug.Log($"{nameof(TowerDefenseMatch)}: 둥지 하나가 무너졌다 — 남은 출구 {activeSpawnPoints.Count}곳.");

				// ★ 마지막 둥지가 무너지면 이긴다 — 실시간 전환으로 「N웨이브를 넘기면 승리」가 사라진 뒤
				//   유일하게 남은 *끝*이다. 끝이 없으면 아무리 잘해도 언젠가 지는 게임이 되고,
				//   그건 「밀어낸다」를 넣은 의미를 통째로 없앤다.
				if (nests.Count == 0 && nestsEverSpawned)
				{
					Debug.Log($"{nameof(TowerDefenseMatch)}: 마지막 둥지가 무너졌다 — 개척 성공.");
					Conclude(TowerDefenseOutcome.Victory);
				}
			}
		}

		private bool IsNest(MatchCombatant combatant)
		{
			foreach ((MatchCombatant nest, Vector3 _) in nests)
			{
				if (nest == combatant)
					return true;
			}
			return false;
		}

		/// <summary> 남은 마수 출구 수 — 화면이 「얼마나 밀어냈나」를 말한다. </summary>
		public int NestCount => nests.Count;

		/// <summary> 둥지 자리들 — 미니맵이 마수와 갈라 크게 그린다. </summary>
		public IEnumerable<Vector3> NestPositions
		{
			get
			{
				foreach ((MatchCombatant nest, Vector3 _) in nests)
				{
					if (nest != null && nest.IsAlive)
						yield return nest.Position;
				}
			}
		}

		/// <summary> 그 마수가 둥지인가 — 화면이 둘을 다르게 그린다. </summary>
		public bool IsNestCombatant(MatchCombatant combatant) => IsNest(combatant);

		/// <summary>
		/// 인형 하나가 판에 서기까지의 *공통 절차* 결과 — 코루틴은 값을 못 돌려주므로 담아서 준다.
		/// </summary>
		private sealed class SpawnedUnit
		{
			public GameObject GameObject;
			public UnitObject UnitObject;
			public MatchCombatant Combatant;
			public bool Ok;
		}

		/// <summary>
		/// 인형 하나를 판에 세운다 — 꺼내기부터 표적 등록까지의 아홉 단계.
		///
		/// ★ 왜 한 곳으로 모았나: 코어·마수·둥지·수비대·영웅 다섯 경로가 *같은 아홉 단계*를 각자
		///   되풀이하고 있었다(438줄). 한 경로가 한 줄만 빠뜨리면 그 인형만 혼자 다르게 논다 —
		///   실제로 「스킬 자동시전 끄기」를 빠뜨리면 그 유닛만 제멋대로 스킬을 쏜다(트랩#1).
		/// ★ 한 프레임 양보가 이 안에 있다: 꺼낸 직후 Init 하면 Start 초기화와 겹친다(트랩#4).
		///   그 대기 중에 판이 사라질 수 있어 되돌아온 뒤 반드시 다시 확인한다.
		/// 각 경로가 다른 것(무기·전술·이름표·둥지 체력·영웅 조종권)은 부른 쪽이 뒤에 얹는다.
		/// </summary>
		private IEnumerator SpawnUnitRoutine(Unit unitData, Vector3 worldPosition, int team,
			Color tint, float scale, SpawnedUnit result)
		{
			result.Ok = false;
			int generation = matchGeneration; // 이 인형은 *이 판의 것*이다.

			GameObject unitGameObject = pool.Spawn(unitData.Prefab);
			if (spawnedUnits.Contains(unitGameObject) == false)
				spawnedUnits.Add(unitGameObject); // 풀이 옛 시체를 재사용하면 같은 참조 — 중복 추적 방지.

			// ★ 세운 것은 *움직이지 않는다*. 물리를 안 끄면 영웅·마수가 지나가며 건물을 밀어낸다
			//   (사용자 실증: "코어 건물이 영웅 유닛에 밀립니다. 건물 좀 고정되게"). 칸에 세운 것이
			//   칸 밖으로 밀리면 「그 자리에 지었다」는 규칙 자체가 거짓이 된다 — 점유 칸은 그대로인데
			//   그림만 옆에 가 있다. 여기 한 곳에서 전부 고정한다(스폰의 단일 관문).
			Rigidbody spawnedBody = unitGameObject.GetComponent<Rigidbody>();
			if (spawnedBody != null)
			{
				spawnedBody.isKinematic = true;
				spawnedBody.useGravity = false;
			}

			// ★ 안개는 안 보이는 개체의 렌더러를 *끈다*. 그 개체가 풀로 돌아갔다 재사용되면 꺼진 채로
			//   다시 태어난다 — 사용자 실증: "다시시작 하면 건물 모습이 안보임". 세우는 순간 되켠다.
			//   (끄는 쪽과 켜는 쪽이 짝이 안 맞으면, 그 병은 *다음 판*에 나타나 원인을 찾기 어렵다.)
			foreach (Renderer spawnedRenderer in unitGameObject.GetComponentsInChildren<Renderer>(true))
				spawnedRenderer.enabled = true;
			unitGameObject.transform.position = worldPosition;
			result.GameObject = unitGameObject;

			yield return null; // 트랩#4 — Start 초기화가 가라앉은 뒤 Init.

			// 대기 중 판이 사라졌거나(모드 이탈) *다른 판으로 갈렸으면*(다시 시작) 여기서 멈춘다.
			if (core == null || targeting == null || pool == null || generation != matchGeneration)
			{
				// 꺼내둔 몸은 돌려준다 — 안 돌려주면 아무 판에도 안 속한 인형이 화면에 남는다.
				if (unitGameObject != null && ObjectPoolManager.TryGetExistingInstance(out ObjectPoolManager strayPool))
					ReleaseUnit(strayPool, unitGameObject);
				spawnedUnits.Remove(unitGameObject);
				yield break;
			}

			UnitObject unitObject = unitGameObject.GetComponent<UnitObject>();
			if (unitObject == null)
			{
				Debug.LogWarning($"{nameof(TowerDefenseMatch)}: {unitData.Prefab.name} 에 UnitObject 없음 — 세우지 못했다.");
				yield break;
			}

			// Init → 트랩#1(자동시전 차단) → MatchCombatant 부여 = 투기장과 공유하는 편입 절차.
			MatchCombatant combatant = CombatUnitSpawner.Enlist(unitObject, unitData, team, nextCombatantId++);

			ApplyReadability(unitObject, tint, scale);
			unitGameObject.SetActive(true);

			// 트랩#2 — 다섯 경로(코어·마수·둥지·수비대·영웅)가 예외 없이 하던 것이라 관문 안으로 들였다.
			// 활성화 *뒤*라야 OnEnable 로 뜬 코루틴이 OnDisable 로 멈춘다.
			CombatUnitSpawner.SilenceBrains(unitGameObject);

			targeting.Register(combatant);
			registeredCombatants.Add(combatant);

			result.UnitObject = unitObject;
			result.Combatant = combatant;
			result.Ok = true;
		}

		/// <summary>
		/// 이 판의 점수 재료 — 실시간이 되면서 「몇 웨이브를 넘겼나」는 척도가 아니게 됐다.
		/// 웨이브는 이제 시계가 40초마다 자동으로 부르므로, 오래 버틴 것이 곧 잘한 것이다.
		/// 둥지를 부순 수는 「버텼다」와 다른 축 — *밀어냈다*를 센다.
		/// </summary>
		public int SurvivedSeconds => core != null ? Mathf.FloorToInt(core.ElapsedSeconds) : 0;
		public int NestsDestroyed { get; private set; }


		private IEnumerator SpawnCoreRoutine()
		{
			SpawnedUnit spawned = new();
			yield return SpawnUnitRoutine(stage.CoreUnit, stageRoot.TransformPoint(activeCorePosition),
				DEFENDER_TEAM, stage.CoreTint, stage.CoreScale, spawned);
			if (spawned.Ok == false)
				yield break;

			GameObject coreGameObject = spawned.GameObject;
			UnitObject coreUnitObject = spawned.UnitObject;
			MatchCombatant combatant = spawned.Combatant;

			// 여기부터는 *코어만의 것*이다. (트랩#2 브레인 비활성은 세우는 문이 이미 했다.)
			targeting.RegisterObjective(combatant); // 적이 전진할 목표물 — 일반 등록과 직교, 둘 다 필요.

			coreCombatant = combatant;

			// ★ 코어도 반격한다(개선 목록 22번) — 마지막 보루가 무방비면 「여기까지 왔다」가 곧 끝이다.
			//   포탑과 *같은 무기 표*를 쓴다: 다른 표를 두면 두 곳이 갈라져 화면과 규칙이 어긋난다.
			if (stage.CoreWeapon != null)
			{
				TowerDefenseWeapon coreWeapon = coreGameObject.GetComponent<TowerDefenseWeapon>();
				if (coreWeapon == null)
					coreWeapon = coreGameObject.AddComponent<TowerDefenseWeapon>();
				coreWeapon.Configure(stage.CoreWeapon, targeting, combatant, waveEnemies,
					IsVisibleAt, DamageMultiplierFor, () => Adaptation, () => TowerRangeMultiplier);
			}

			AddVisionSource(coreGameObject.transform.position, stage.CoreVisionRadius);

			// 보급이 여기서 출발해 어디까지 닿는지 — 안 보이면 「왜 안 이어지지」를 짐작으로 풀어야 한다.
			ShowSupplyReachRing(coreGameObject.transform);
		}

		private void Tick()
		{
			if (ticking == false || core == null)
				return;

			TickHero();

			// ★ 실시간이라 카드가 걸려도 판은 멈추지 않는다(사용자 지시, 데아빌). 멈추고 싶으면 사람이
			//   직접 멈춘다(⏸ 버튼) — 시간을 쥐는 것은 시스템이 아니라 플레이어다.
			CullEscapedEnemies(); // 무대 밖 개체가 웨이브를 영원히 붙잡지 못하게 — 집계 *전에* 정리.
			CullLeakedEnemies();  // 목표에 닿은 마수는 사라지고 목숨이 준다(유출제).
			UnstickEnemies();     // 굳은 마수를 풀어준다 — 한 마리가 굳으면 웨이브가 영영 안 끝난다.
			CullDestroyedNests(); // 부순 둥지의 출구를 닫는다 — 「버틴다」가 「밀어낸다」가 되는 자리.
			WakeNearbyLairs();    // 내 것이 가까이 갔으면 잠든 서식지가 깨어난다.
			AnnounceAdaptation(); // 마수가 무엇에 익숙해졌는지 — 안 보이면 없는 규칙이다.
			AnnouncePressure();   // 시간이 올린 강도 — 같은 포탑이 갑자기 안 통하는 이유.
			TickLairLeash();      // 깨어난 것은 제 자리를 지킨다 — 코어로 행진하면 그냥 파도가 하나 더다.
			CollectClearedLairs();// 다 쓴 서식지는 정수를 낸다 — 싸워서 버는 길.
			TrackLostBuildings(); // 내 것이 부서지면 *그 자리*를 알린다 — 화면 밖이면 알 길이 없었다.
			RefreshPower();       // 전기를 못 받는 건물은 선다(도시 건설의 규칙 그대로).
			TickSignalView();     // 신호가 번지는 것을 눈으로 보여준다 — 테두리와 파동.
			RefreshBuildingProgress(); // 「무엇이 일하고 있나」를 머리 위 바에 채운다.
			TryGrowWindow();      // 내 것이 판 끝에 닿으면 판이 자란다(무한 맵).
			ApplyEnemyVisibility(); // 안 보이는 마수는 화면에서도 지운다(규칙과 그림이 같아야 한다).
			RefreshSupply();        // 방어 건물이 부서지면 그 순간 사슬이 끊긴다.
			PayKillBounties();    // 격파 즉시 보상 — 웨이브 정산만 있으면 교전 중엔 아무 보상도 안 온다.

			bool coreAlive = coreCombatant != null && coreCombatant.IsAlive;
			CountAliveEnemies(); // 죽은 참조 정리 — 세는 값은 아래 집계를 쓴다.

			// ★ 「한때 몇 마리까지」도 *쳐들어온 마수*만 센다. 둥지는 목록에 있지만 쳐들어온 것이 아니다 —
			//   화면의 「적 N마리」만 고쳐놨더니 판 요약은 여전히 둥지 수만큼 부풀어 있었다(같은 병, 다른 자리).
			if (AliveEnemyCount > PeakEnemies)
				PeakEnemies = AliveEnemyCount;

			TowerDefenseSignal signal = core.Tick(TimeManager.TICK, coreAlive);
			switch (signal)
			{
				case TowerDefenseSignal.WaveStarted:
					RefreshVision(); // 어스름 진입/이탈이 시야에 즉시 반영돼야 한다.
					StartCoroutine(SpawnGroupRoutine(ScaledEnemyCount(core.WaveIndex)));
					break;

				// 상시로 한 마리씩 새어 나온다 — 「웨이브 사이엔 안전하다」가 사라진다(데아빌의 배회 감염체).
				case TowerDefenseSignal.TrickleDue:
					StartCoroutine(SpawnGroupRoutine(1));
					break;

				// 정산은 시계가 돈다 — 웨이브를 격퇴해야 벌던 옛 구조에서는 실시간에 아무것도 안 들어온다.
				case TowerDefenseSignal.IncomeDue:
					ShowIncomeBreakdown();
					HealDefenders();
					AwardHarvestExperience(); // 캐는 것도 일이다 — 채집도 자란다.
					AwardCoreExperience(stage.HarvestExperience);
					break;
				case TowerDefenseSignal.Victory:
					Conclude(TowerDefenseOutcome.Victory);
					break;
				case TowerDefenseSignal.Defeat:
					Conclude(TowerDefenseOutcome.Defeat);
					break;
				// (구 페이즈제 잔재 — 실시간에서는 안 온다.)
				case TowerDefenseSignal.WaveCleared:
					break;
				// None = 규칙 상 상태전이 없음 — 셸 actuation 0.
				case TowerDefenseSignal.None:
				default:
					break;
			}

			// ★ 끝났다는 사실은 *신호가 아니라 상태*가 진실이다. 목숨이 다 닳는 패배(유출제 = 지금의 주
			//   패배 경로)는 규칙층이 결과만 적고 신호를 안 내보내서, 화면이 그걸 영영 못 들었다
			//   (실측: outcome=Defeat 인데 배너가 안 뜨고 요약도 안 나옴). 결과를 직접 보면
			//   앞으로 어떤 새 끝 조건이 생겨도 「신호 내는 걸 깜빡해서 화면이 조용한」 일이 안 생긴다.
			if (matchEndedFired == false && core.Outcome != TowerDefenseOutcome.InProgress)
				Conclude(core.Outcome);
		}

		/// <summary> WaveStarted 신호 처리 — SO 스폰 지점에 분산 스폰. 한 마리도 못 내보내면 FastFail 로그. </summary>
		/// <summary>
		/// 마수를 내보낸다. count 가 큰 무리면 웨이브, 1이면 상시로 새어 나오는 한 마리다.
		///
		/// ★ 실시간 전환의 핵심(사용자 지시, 데아빌): 살아있는 마수 목록을 *비우지 않는다*. 페이즈제에서는
		///   「이번 웨이브 것만」 추적하면 됐지만, 실시간에서는 앞 무리와 상시 마수가 동시에 판에 있다.
		///   비우면 아직 살아있는 마수를 놓쳐 화면과 집계가 갈라진다.
		/// </summary>
		private IEnumerator SpawnGroupRoutine(int count)
		{
			PruneDeadEnemies(); // 죽은 것만 걷어낸다 — 살아있는 것은 남긴다(실시간이라 겹쳐 존재한다).

			ComposeWave(core.WaveIndex, waveComposition); // 예고와 같은 함수 = 화면이 말한 대로 나온다.
			RebuildInvasionFront(core.WaveIndex);          // 이번 파도가 밀려올 테두리 토막 — 예고와 같은 함수.

			TowerDefenseWaveEventKind waveEvent = WaveEventAt(core.WaveIndex);
			int enemyCount = count;
			int spawnedCount = 0; // 실제로 UnitObject 확보 + 등록까지 끝난 수 — 이게 0 이면 스테이지 데이터 구멍이다.

			for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
			{
				if (stage.EnemyUnit == null || stage.EnemyUnit.Prefab == null)
				{
					Debug.LogWarning($"{nameof(TowerDefenseMatch)}: stage.EnemyUnit/Prefab 미할당 — 웨이브 스폰 skip.");
					break;
				}

				// ★ **파도는 테두리, 상시로 새는 것은 둥지.**
				//   테두리 침공을 넣으면서 둘 다 테두리로 보냈더니 「둥지를 부수면 그 출구가 닫힌다」가
				//   거짓말이 됐다 — 부숴도 오는 양이 그대로였다(규칙은 바꿨는데 그 약속을 안 옮긴 것).
				//   파도가 테두리에서 오는 것과, 둥지가 상시 압박의 출구인 것은 서로 다른 층이라 둘 다 산다.
				bool isWave = count > 1;
				IReadOnlyList<Vector3> origins = isWave && invasionFront.Count > 0
					? invasionFront
					: activeSpawnPoints;

				// 둥지를 다 부쉈으면 상시로 샐 곳이 없다 — 그게 「출구가 닫혔다」의 실제 모습이다.
				// (여기서 안 막으면 자리가 없어 무대 한가운데(0,0)에서 솟는다.)
				if (isWave == false && origins.Count == 0)
					yield break;
				Vector3 localSpawn = origins.Count > 0
					? origins[enemyIndex % origins.Count] + SpawnSpreadOffset(enemyIndex, origins.Count)
					: Vector3.zero;

				// ★ 분산(SpawnSpreadOffset)이 마수를 암반 위/뒤에 떨구면 그 마리는 「갈 수 없는 자리」에서 시작해
				//   그대로 굳는다 — 한 마리만 굳어도 웨이브가 영영 안 끝난다(사용자 실증: "멈춰서 안올때가 있음").
				//   출현 지점 자체는 길이 보장돼 있으므로(RebuildPathing 검사) 벌어진 자리만 되돌린다.
				localSpawn = SnapSpawnToReachable(localSpawn);

				// 종류를 먼저 정한다 — 색·덩치가 그 종류에서 나오므로 세우기 전에 알아야 한다.
				TowerDefenseEnemyArchetype archetype = enemyIndex < waveComposition.Count
					? EnemyArchetypeAt(waveComposition[enemyIndex])
					: null;

				SpawnedUnit spawned = new();
				yield return SpawnUnitRoutine(stage.EnemyUnit, stageRoot.TransformPoint(localSpawn), ATTACKER_TEAM,
					archetype != null ? archetype.Tint : stage.EnemyTint,
					stage.EnemyScale * (archetype != null ? archetype.ScaleMultiplier : 1f), spawned);
				if (spawned.Ok == false)
					continue;

				GameObject enemyGameObject = spawned.GameObject;
				UnitObject enemyUnitObject = spawned.UnitObject;
				MatchCombatant enemyCombatant = spawned.Combatant;
				enemyBountyById[enemyCombatant.CombatantId] = archetype != null ? archetype.Bounty : core.BountyPerKill;

				// ★ 스탯 배수는 *켠 다음 프레임*에 씌운다. UnitObject.Start 가 UnitData 로 스탯을 통째 다시
				//   세팅하므로(재-Init 규약), 켜기 전에 올려둔 체력은 첫 프레임에 조용히 원래대로 돌아간다
				//   (라이브 실증: 덩치·보상은 갈리는데 체력만 전부 같았다).
				yield return null;
				if (core == null || targeting == null || pool == null)
					yield break;
				ApplyArchetypeStats(enemyUnitObject, archetype, stage != null ? stage.EnemyMoveSpeedMultiplier : 1f);
				ApplyPressure(enemyUnitObject); // 오래 버틸수록 단단해진다 — 실시간의 난이도는 시간이 올린다.
				ApplyWaveEventStats(enemyUnitObject, waveEvent);


				TacticDriver enemyDriver = enemyUnitObject.GetComponent<TacticDriver>();
				if (enemyDriver == null)
					enemyDriver = enemyUnitObject.gameObject.AddComponent<TacticDriver>();
				enemyDriver.enabled = true; // 풀이 어떤 상태로 주든 켜고 시작한다(허리띠 + 멜빵).
				enemyDriver.Initialize(stage.EnemyTactic, targeting, timeManager);
				IgnoreHeroCollision(enemyUnitObject.gameObject); // 새로 온 마수도 영웅을 통과한다.
				enemyDriver.Navigator = flowNavigator; // 지형이 있으면 돌아가고, 없으면(null) 직선 그대로.
				enemyDriver.StopsToAttack = false;     // 걸으면서 쏜다 — 전진이 멈추면 판이 안 끝난다.
				// 마수가 코어 둘레에 「고리」로 서는 거리 — 유출 반경이 이보다 작으면 바깥 고리는 영영 안 닿는다.
				enemyMaxStopDistance = Mathf.Max(enemyMaxStopDistance, enemyDriver.MaxStopDistance);
				drivers.Add(enemyDriver);

				// 표적 등록은 세우는 문이 이미 했다.
				waveEnemies.Add(enemyCombatant);
				spawnedCount++;

				// ★ 한 지점에 한꺼번에 쏟으면 마수들이 서로의 몸에 끼어 그 자리에서 못 나온다
				//   (라이브 실측: 출현 줄에서 세 마리가 나란히 4초씩 정지). 좌우로 벌리는 것만으로는
				//   마릿수가 늘면 결국 겹친다 — *시간*으로 흘려보내야 구조적으로 안 겹친다.
				//   덤으로 「웨이브가 밀려온다」는 감각이 생긴다(장르 표준의 trickle spawn).
				// ★ 무리로 내보낸다 (사용자 지시: "여러 기가 한 번에 천천히 몰려오게").
				//   무리 안 = 눈에 안 띄는 짧은 간격(0으로 두면 서로의 몸에 끼어 그 자리서 못 나온다 — 실측).
				//   무리가 다 나왔으면 = 긴 간격. 그래서 「덩어리로 밀려오고, 다음 덩어리까지는 숨 돌린다」.
				bool groupFinished = stage.EnemyGroupSize <= 1
					|| spawnedCount % stage.EnemyGroupSize == 0;
				float wait = groupFinished ? stage.EnemySpawnInterval : stage.EnemyGroupSpacing;
				if (wait > 0f)
					yield return new WaitForSeconds(wait);
			}

			// 웨이브를 불렀는데 한 마리도 안 나온 것은 그 자체로 스테이지 데이터 구멍이다 — 조용히 넘어가면
			// 「큰 무리가 왔다」는 화면 글자만 뜨고 판은 텅 빈다. (실시간 전환 뒤 규칙은 살아있는 적 수를
			// 안 보므로 클리어 오인 위험은 사라졌고, 남은 것은 이 FastFail 알림뿐이다.)
			if (spawnedCount == 0 && count > 1)
				Debug.LogError($"{nameof(TowerDefenseMatch)}: 웨이브 적 0마리 스폰 — stage.EnemyUnit/EnemySpawnPoints 확인 필요.");
		}

		/// <summary>
		/// 같은 출현 지점에 나오는 마수들을 서로 벌린다.
		///
		/// ★ 겹쳐 스폰하면 물리가 파고듦을 해소하려고 서로를 튕겨내 **맵 밖으로 날려버린다**
		///   (실측: 살아있는 마수 2기가 (1236, -2906, 2015) 로 날아가 웨이브가 영원히 안 끝났다).
		///   출현 지점 수보다 마수가 많아지는 후반 웨이브에서 반드시 발생하므로 스폰 단계에서 막는다.
		/// 같은 지점을 쓰는 몇 번째인지로 좌우 지그재그 — 결정적(같은 웨이브 → 같은 배치).
		/// </summary>
		/// <summary>
		/// waveIndex 파도가 밀려올 테두리 토막을 다시 뽑는다. 스폰과 예고가 **같은 함수**를 쓰므로
		/// 화면이 가리킨 쪽과 실제로 오는 쪽이 갈라질 수 없다(갈라지면 예고가 거짓말이 된다).
		/// </summary>
		private void RebuildInvasionFront(int waveIndex)
		{
			invasionFront.Clear();
			if (stage == null || stage.BorderInvasion == false)
				return;

			TowerDefenseWaveOrigin.SampleAt(
				InvasionAngleAt(waveIndex),
				stage.InvasionArcDegrees,
				activeGroundWidth * 0.5f,
				activeGroundLength * 0.5f,
				stage.InvasionEdgeInset,
				stage.InvasionFrontPoints,
				invasionFront);
		}

		/// <summary>
		/// 그 파도가 들어오는 방향(도). 화면 예고가 이걸 그대로 읽는다 — 미래 파도도 물어볼 수 있다.
		///
		/// ★ 뚫린 자리가 있으면 그쪽으로 끌린다 — 「지킬 수 있는 만큼만 넓혀라」를 말이 아니라 규칙으로
		///   만드는 자리다. 예고와 스폰이 **같은 이 함수**를 봐야 한다. 갈라지면 화면이 북이라 하고
		///   마수는 남에서 오는, 준비 자체가 무의미해지는 거짓말이 된다.
		/// </summary>
		public float InvasionAngleAt(int waveIndex)
		{
			float baseAngle = TowerDefenseWaveOrigin.AngleDegrees(waveIndex, MapSeed);
			if (stage == null || stage.BreachPull <= 0f || coreCombatant == null)
				return baseAngle;
			if (breach.TryGetBiasAngle(coreCombatant.Position, out float biasAngle) == false)
				return baseAngle;

			return TowerDefenseWaveOrigin.Blend(baseAngle, biasAngle, stage.BreachPull);
		}

		/// <summary> 지금 뜨거운 뚫린 자리 수 — 화면·검사가 「규칙이 살아 있나」를 볼 창. </summary>
		public int BreachHotCount => breach.HotCount;

		/// <summary>
		/// 판의 시계를 앞으로 감는다 — 검사 전용.
		///
		/// ★ 왜 필요한가: 마수 강도는 *시간*이 올린다. 한 칸 오르는 데 실제로 몇 분이 걸려서
		///   하네스가 도는 1~2분 안에는 절대 안 오른다 — 그래서 「강도가 올랐다」는 알림이
		///   여태 한 번도 화면에 안 떴고, 계산만 시험으로 덮인 채 남아 있었다.
		///   재는 쪽이 사건을 일으킬 수 있어야 닫힌다(적응·뚫린 자리에서 두 번 통한 방법).
		/// ★ 이어하기가 쓰는 것과 **같은 문**(시계 되돌리기)으로 들어간다 — 다른 문을 새로 뚫으면
		///   검사만 통과하는 길이 생긴다.
		/// </summary>
		/// <summary> 1분당 강도 상승폭 — 검사가 「몇 초를 감아야 한 칸 오르나」를 역산한다(초 박기 금지). </summary>
		public float PressurePerMinute => stage != null ? stage.Rules.PressurePerMinute : 0f;

		/// <summary> 알림이 뜨는 강도 간격 — 위와 같은 이유로 밖에서 읽을 수 있어야 한다. </summary>
		public float PressureAnnounceStep => stage != null ? stage.PressureAnnounceStep : 0f;

		public void AdvanceClockForVerification(float seconds)
		{
			if (core == null || seconds <= 0f)
				return;

			core.Restore(core.ElapsedSeconds + seconds, core.WaveIndex, core.Lives);
		}

		/// <summary>
		/// 내 건물 하나를 코어에서 *가장 먼* 것으로 골라 없앤다 — 검사 전용.
		///
		/// ★ 왜 필요한가: 「뚫린 자리가 다음 파도를 끌어당긴다」는 건물을 잃어야만 확인된다. 그런데
		///   하네스는 마수가 내 건물을 부술 때까지 기다릴 수밖에 없고, 그건 판마다 오거나 안 온다
		///   (적응 검사에서 이미 다섯 사이클을 그렇게 날렸다). 재는 쪽이 사건을 일으킬 수 있어야 한다.
		/// ★ 왜 가장 먼 것인가: 코어 바로 옆을 없애면 방향이 거의 안 바뀌어 「끌렸다」를 못 가른다.
		///   멀수록 각이 뚜렷해 참·거짓이 갈린다.
		/// ★ 없애는 방법은 마수가 부수는 것과 같은 문(오브젝트 소멸)이다 — 다른 문으로 들어가면
		///   *검사만 통과하는* 길이 생긴다.
		/// </summary>
		/// <summary> 이보다 가까운 것을 없애면 방향이 안 나온다 — 재는 의미가 없다. </summary>
		private const float MIN_VERIFY_LOSS_DISTANCE = 6f;

		public bool DestroyFarthestBuildingForVerification(out Vector3 destroyedAt)
		{
			destroyedAt = Vector3.zero;
			if (coreCombatant == null)
				return false;

			Transform farthest = null;
			float bestDistance = -1f;
			foreach (Transform building in supplyChain.Buildings)
			{
				if (building == null || building == coreCombatant.transform)
					continue;

				float distance = Vector3.Distance(building.position, coreCombatant.Position);
				if (distance <= bestDistance)
					continue;
				bestDistance = distance;
				farthest = building;
			}

			// ★ 코어 위(또는 코앞)에 있는 것을 고르면 잃은 방향이 0 도로 나와 「끌렸나」를 못 가른다.
			//   실제로 전체 실행에서 그렇게 뽑혀 「잃은 쪽 0.0도 · 뜨거운 자리 0곳」이라는 읽을 수
			//   없는 결과가 나왔다. 방향이 성립할 만큼 떨어진 것이 없으면 **없앨 것이 없다**고 답한다
			//   — 아무거나 없애고 재는 것보다 「못 쟀다」가 낫다.
			if (farthest == null || bestDistance < MIN_VERIFY_LOSS_DISTANCE)
				return false;

			destroyedAt = farthest.position;
			Destroy(farthest.gameObject);
			return true;
		}

		/// <summary> 부서진 자리는 잊히지 않는다 — 다음 파도가 그쪽으로 끌린다. </summary>
		private readonly TowerDefenseBreach breach = new();

		/// <summary> 내가 낸 소리 — 자는 것을 깨운다. </summary>
		private readonly TowerDefenseNoise noise = new();

		/// <summary> 지금 판에서 가장 시끄러운 소리 — 화면·검사가 「규칙이 도나」를 볼 창. </summary>
		public float LoudestNoise => noise.LoudestLevel;

		/// <summary> 서식지가 깨어나는 소리 문턱 · 거리 — 검사가 값을 박지 않고 판에서 읽는다. </summary>
		public float NoiseWakeThreshold => stage != null ? stage.NoiseWakeThreshold : 0f;
		public float LairWakeRadius => stage != null ? stage.LairWakeRadius : 0f;

		/// <summary> 그 자리에서 들리는 소리 — 검사가 「둥지가 들을 만한가」를 직접 잰다. </summary>
		public float NoiseHeardAt(Vector3 worldPosition)
		{
			return stage != null ? noise.LevelAt(worldPosition, stage.NoiseHearingRadius) : 0f;
		}

		/// <summary>
		/// 소리를 낸다 — 짓기·사격·얻어맞기가 전부 이 문으로 들어온다.
		///
		/// ★ 문을 하나로 두는 이유: 소리를 내는 자리가 늘어날 때마다 합치는 거리·상한을 각자
		///   정하면, 어떤 소리는 자리를 스무 개 만들고 어떤 소리는 하나로 뭉친다. 규칙이 갈라진다.
		/// </summary>
		public void EmitNoise(Vector3 worldPosition, float amount)
		{
			if (stage == null)
				return;
			noise.Emit(worldPosition, amount, stage.NoiseMergeDistance);
		}

		/// <summary>
		/// 다음 파도의 성격 이름 + 조사("떼거리가"). 성격이 없으면 빈 문자열.
		///
		/// ★ 이 값은 계산은 되는데 *화면에 도달하지 못하고 있었다* — 웨이브 미리보기 칸을 숨기면서
		///   같이 묻혔다(숫자를 안 띄우기로 한 결정의 부작용). 성격은 **말**이라 숫자 금지와 무관하고,
		///   「무엇이 오는가」를 모르면 대비가 성립하지 않는다.
		/// </summary>
		public string NextWaveEventPhrase()
		{
			return TowerDefenseWaveEvent.SubjectPhrase(WaveEventAt(WaveIndex + 1));
		}

		/// <summary> 다음 파도가 오는 쪽 이름("북동" 등). 숫자 대신 말로 예고하기 위한 값. </summary>
		public string NextInvasionDirectionName()
		{
			return TowerDefenseWaveOrigin.DirectionName(InvasionAngleAt(WaveIndex + 1));
		}

		/// <summary> 테두리 침공이 실제로 켜져 돌고 있는가 — 화면이 예고를 띄울지 정하는 근거. </summary>
		public bool IsBorderInvasion => stage != null && stage.BorderInvasion;

		/// <summary>
		/// 다음 파도가 들어올 자리(월드). 화면이 여기에 표식을 세워 **어디를 막을지**를 미리 말한다.
		/// 스폰과 같은 함수를 쓰므로 표식이 선 자리가 곧 실제로 나올 자리다.
		/// </summary>
		public void CollectNextInvasionPoints(List<Vector3> into)
		{
			if (into == null)
				return;

			into.Clear();
			if (stage == null || stage.BorderInvasion == false || stageRoot == null)
				return;

			TowerDefenseWaveOrigin.Sample(
				WaveIndex + 1,
				MapSeed,
				stage.InvasionArcDegrees,
				activeGroundWidth * 0.5f,
				activeGroundLength * 0.5f,
				stage.InvasionEdgeInset,
				stage.InvasionFrontPoints,
				into);

			for (int index = 0; index < into.Count; index++)
				into[index] = stageRoot.TransformPoint(into[index]);
		}

		private Vector3 SpawnSpreadOffset(int enemyIndex, int pointCount)
		{
			if (pointCount <= 0)
				return Vector3.zero;

			int repeat = enemyIndex / pointCount;          // 이 지점을 몇 번째로 쓰는가
			int lane = (repeat + 1) / 2;                   // 0,1,1,2,2,...
			float side = repeat % 2 == 0 ? 1f : -1f;       // 좌우 번갈아
			float spread = stage.EnemySpawnSpread;

			// z 도 조금 밀어 완전히 같은 줄에 서지 않게(앞뒤로도 벌림).
			return new Vector3(lane * spread * side, 0f, repeat * spread * 0.35f);
		}

		/// <summary>
		/// 벌어진 출현 자리가 길 위인지 확인하고, 아니면 가장 가까운 갈 수 있는 칸으로 되돌린다.
		/// 고정 판(흐름장 없음)에서는 아무것도 안 한다 — 그쪽은 애초에 암반이 없다.
		/// </summary>
		private Vector3 SnapSpawnToReachable(Vector3 localSpawn)
		{
			if (mapLayout == null || flowField == null)
				return localSpawn;

			Vector2Int cell = mapLayout.WorldToCell(localSpawn);
			if (flowField.IsReachable(cell))
				return localSpawn;

			if (TrySnapToReachable(cell, out Vector2Int freeCell) == false)
				return localSpawn;

			return mapLayout.CellToWorld(freeCell);
		}

		/// <summary>
		/// 무대를 벗어난 적 정리 — 지면 아래로 떨어졌거나 개척지 밖으로 날아간 개체는 죽은 것으로 친다.
		///
		/// ★ 이게 없으면 *어떤* 물리 사고든 곧바로 「웨이브가 영원히 안 끝남」이 된다(코어는 생존 적을
		///   세는데 그 적은 화면 밖에 있어 플레이어가 손쓸 방법이 없다). 원인을 하나 막는 것과 별개로,
		///   무대 밖 개체가 진행을 막지 못하게 하는 안전망이 진행 규칙 쪽에 있어야 한다.
		/// </summary>
		private void CullEscapedEnemies()
		{
			if (stage == null || stageRoot == null)
				return;

			float halfWidth = activeGroundWidth * 0.5f + stage.StageBoundsMargin;
			float halfLength = activeGroundLength * 0.5f + stage.StageBoundsMargin;

			for (int index = waveEnemies.Count - 1; index >= 0; index--)
			{
				MatchCombatant enemy = waveEnemies[index];
				if (enemy == null || enemy.IsAlive == false)
					continue;

				Vector3 local = stageRoot.InverseTransformPoint(enemy.Position);
				bool escaped = local.y < stage.StageFloorDepth
					|| Mathf.Abs(local.x) > halfWidth
					|| Mathf.Abs(local.z) > halfLength;
				if (escaped == false)
					continue;

				Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 마수가 무대를 이탈 — 제거로 처리 local={local}. "
					+ "(스폰 겹침·물리 튕김 흔적이면 EnemySpawnSpread 확인)");

				targeting.Unregister(enemy);
				registeredCombatants.Remove(enemy);
				waveEnemies.RemoveAt(index);

				TacticDriver driver = enemy.GetComponent<TacticDriver>();
				if (driver != null)
					driver.StopDriving();

				ReleaseUnit(pool, enemy.gameObject);
				spawnedUnits.Remove(enemy.gameObject);
			}
		}

		/// <summary>
		/// 포탑 사거리 = 전술의 표적 탐색 반경. 별도 수치를 두면 화면의 원과 실제 사거리가 갈라진다
		/// (원이 거짓말하는 순간 배치 판단 전체가 무의미해진다) — 그래서 전술 정본에서 읽는다.
		/// </summary>
		public float TowerRange(int towerIndex = 0)
		{
			// ★ 연구 배수를 *여기서* 곱한다. 총은 이 배수를 곱해 쏘는데 원만 안 곱하면, 원은 그대로인데
			//   실제로는 더 멀리 쏘는 「거짓말하는 원」이 된다 — 배치 판단의 유일한 근거가 그 원이다.
			return RawTowerRange(towerIndex) * TowerRangeMultiplier;
		}

		/// <summary> 연구를 빼고 무대가 적어둔 그대로의 사거리 — 배수를 두 번 곱하지 않으려면 여기서 읽는다. </summary>
		public float RawTowerRange(int towerIndex = 0)
		{
			TowerDefenseTowerArchetype archetype = TowerArchetypeAt(towerIndex);
			if (archetype != null)
				return archetype.Range;

			if (stage == null || stage.TowerTactic.Rules == null)
				return 0f;

			float best = 0f;
			foreach (TacticRule rule in stage.TowerTactic.Rules)
			{
				if (rule.Target.MaxRange > best)
					best = rule.Target.MaxRange;
			}
			return best;
		}

		/// <summary> 첫 웨이브를 사람이 부르길 기다리는 중인가 — 화면이 「시계가 돈다」고 거짓말하지 않게. </summary>
		public bool IsWaitingForFirstCall =>
			core != null
			&& core.Phase == TowerDefensePhase.Prepare
			&& core.WaveIndex < core.FirstAutoWave
			&& core.IsNextWaveRequested == false;

		/// <summary>
		/// 이번 판의 자원 노드 위치 — **무대 로컬 좌표**다. 쓰기 전에 `StageRoot.TransformPoint` 로 옮겨야 한다.
		///
		/// ★ 이름에 로컬을 박아둔 이유: 옆의 배치 API(TryPlaceHarvester/CanBuildAt/TryFindPlaceableNode)는
		///   전부 *월드* 를 받는다. 그대로 넘기면 판이 원점에서 멀리 있을 때(개척은 z≈2000) 전부 조용히
		///   거절당한다 — 오류도 로그도 없이 「왜 안 지어지지」만 남는다(실측: 노드까지 1906칸으로 계산됨).
		/// </summary>
		public IReadOnlyList<Vector3> ActiveResourceNodeLocalPositions => activeNodePositions;

		/// <summary> 그 자리가 지금 보이는가 — 안 보이면 포탑도 못 쏘고 마수도 안 그려진다. </summary>
		public bool IsVisibleAt(Vector3 worldPosition)
		{
			if (vision == null || mapLayout == null || stageRoot == null)
				return true; // 시야 없는 판(고정 레이아웃) = 전부 보임.

			return vision.IsVisible(mapLayout.WorldToCell(stageRoot.InverseTransformPoint(worldPosition)));
		}

		/// <summary> 한 번이라도 밝혔던 자리인가 — 기억한 지형·노드는 계속 보여준다. </summary>
		public bool IsExploredAt(Vector3 worldPosition)
		{
			if (vision == null || mapLayout == null || stageRoot == null)
				return true;

			return vision.IsExplored(mapLayout.WorldToCell(stageRoot.InverseTransformPoint(worldPosition)));
		}

		/// <summary> 시야원 하나 추가 + 즉시 반영 — 건물을 세운 그 순간 밝아져야 「넓혔다」가 읽힌다. </summary>
		private void AddVisionSource(Vector3 worldPosition, float radius)
		{
			if (vision == null || mapLayout == null || stageRoot == null || radius <= 0f)
				return;

			visionSources.Add(new TowerDefenseVision.Source(
				mapLayout.WorldToCell(stageRoot.InverseTransformPoint(worldPosition)), radius));
			RefreshVision();
		}

		private void RefreshVision()
		{
			if (vision == null)
				return;

			// 어스름 웨이브면 모든 시야가 함께 좁아진다 — 「보이는 만큼만 쏜다」가 아프게 걸린다.
			float visionScale = CurrentVisionScale() * boons.VisionMultiplier;
			if (Mathf.Approximately(visionScale, 1f))
			{
				vision.Recompute(visionSources);
			}
			else
			{
				scaledVisionSources.Clear();
				foreach (TowerDefenseVision.Source source in visionSources)
					scaledVisionSources.Add(new TowerDefenseVision.Source(source.Cell, source.Radius * visionScale));
				vision.Recompute(scaledVisionSources);
			}
			if (fogView != null)
				fogView.Apply(vision);
		}

		/// <summary> index 번 노드의 벌이 배수 — 화면 표시와 실제 수입이 같은 값을 읽는다. </summary>
		public float NodeIncomeMultiplierAt(int index)
		{
			return index >= 0 && index < activeNodeIncomeMultipliers.Count ? activeNodeIncomeMultipliers[index] : 1f;
		}

		/// <summary> 무대 루트 — 화면 표시가 로컬 좌표를 월드로 옮길 때 쓴다. </summary>
		public Transform StageRoot => stageRoot;

		/// <summary> 이번 판의 마수 출현 지점(무대 로컬). </summary>
		public IReadOnlyList<Vector3> ActiveEnemySpawnPoints => activeSpawnPoints;

		/// <summary> 지금까지 고른 것 한 줄 요약(없으면 빈 문자열). </summary>
		public string BoonSummary => boons.Describe();

		/// <summary> 지금까지 고른 장수. </summary>
		public int BoonCount => boons.TakenCount;

		// ── 영웅 인형 ─────────────────────────────────────────────────────────────
		// ★ 왜 필요한가: 지금 개척은 「전부 미리 배치하고 지켜본다」라 교전 중에 사람이 할 일이 0 이다.
		//   움직이는 내 편이 하나 있으면 「부족한 곳을 내가 뛰어가 메운다」가 생긴다(Kingdom Rush 의 영웅).
		//   WM 은 본편에 이미 조종하는 인형이 있으니 **한 명만 데려간다**가 세계관 정합이다.
		// ★ 왜 포탑과 같은 표를 쓰나: 전투 수치를 따로 두면 두 곳이 갈라진다. 다른 점은 단 하나 — 움직인다.

		/// <summary> 영웅이 판에 있는가. </summary>
		public bool HasHero => heroActive && heroTransform != null;

		/// <summary> 영웅 현재 위치(없으면 코어 자리). </summary>
		public Vector3 HeroPosition => heroTransform != null ? heroTransform.position : activeCorePosition;

		/// <summary> 영웅을 그 자리로 보낸다 — 걸어간다(순간이동 X, 늦는 것 자체가 판단의 대가다). </summary>
		public bool CommandHero(Vector3 worldPosition)
		{
			if (HasHero == false)
				return false;

			heroTargetPosition = new Vector3(worldPosition.x, heroTransform.position.y, worldPosition.z);
			return true;
		}

		private IEnumerator SpawnHeroRoutine()
		{
			if (stage.HeroUnit == null || stage.HeroUnit.Prefab == null)
				yield break; // 영웅 미설정 스테이지 — 기존 판과 완전히 동일하게 진행.

			Vector3 spawnPosition = stageRoot.TransformPoint(activeCorePosition) + new Vector3(stage.GroundCellSize * 1.5f, 0f, 0f);

			SpawnedUnit spawned = new();
			yield return SpawnUnitRoutine(stage.HeroUnit, spawnPosition,
				DEFENDER_TEAM, stage.HeroTint, stage.HeroScale, spawned);
			if (spawned.Ok == false)
				yield break;

			GameObject heroGameObject = spawned.GameObject;
			UnitObject heroUnitObject = spawned.UnitObject;
			heroCombatant = spawned.Combatant;

			// ★ 영웅의 자리는 *사람이 정한다*. 강체를 그대로 두면 내가 옮긴 좌표를 물리가 매 프레임 되돌린다
			//   (라이브 실측: 옮긴 다음 틱에 뒤로 밀려 제자리 — 명령해도 안 움직이는 것처럼 보였다).
			// ★ 켠 *다음 프레임*에 씌운다 — 스탯 배수와 같은 이유로, 켜기 전에 바꾼 값은 UnitObject.Start 의
			//   재-Init 규약에 조용히 덮인다(이 파일에서 이미 한 번 겪은 트랩).
			//   대여 계약(TowerDefenseUnitLease)이 반납 때 원래 값으로 되돌리므로 다음 대여(마수 등)에 안 샌다.
			yield return null;
			if (core == null || targeting == null || pool == null)
				yield break;

			Rigidbody heroBody = heroGameObject.GetComponent<Rigidbody>();
			if (heroBody != null)
			{
				heroBody.isKinematic = true;
				heroBody.useGravity = false;
			}

			// 길찾기 에이전트(NavMeshAgent)만 끈다 — 개척 지면은 런타임 생성이라 NavMesh 자체가 없고,
			// 켜두면 에이전트가 좌표를 도로 잡아당긴다(실측).
			UnityEngine.AI.NavMeshAgent heroAgent = heroGameObject.GetComponent<UnityEngine.AI.NavMeshAgent>();
			if (heroAgent != null)
				heroAgent.enabled = false;

			// ★ 이동 부품(UnitMovement)은 *켜 둔다*. 예전엔 이것까지 끄고 영웅 좌표를 매 틱 직접 옮겼는데,
			//   그 하나가 사용자 실측 결함 셋을 한꺼번에 만들었다:
			//   ① 뚝뚝 끊김 — 틱은 초당 20번이라 그 사이 프레임엔 영웅이 아예 안 움직인다(순간이동).
			//   ② 벽 통과 — 좌표를 직접 쓰면 충돌을 아무도 안 본다. 이동 부품은 쓸어보고 미끄러진다.
			//   ③ 마수가 밀려남 — 몸을 겹친 채 좌표만 옮기니 물리가 마수를 밀어내 해결한다.
			//   마수는 원래부터 이 부품으로 걷는다 — 영웅만 체계 밖에 있었다.
			heroMovement = heroGameObject.GetComponent<UnitMovement>();
			if (heroMovement != null)
				heroMovement.enabled = true;

			// 「초당 몇 칸」으로 적어둔 영웅 속도를 이동 부품이 읽는 스탯으로 옮긴다(환산 상수는 그쪽 정본).
			if (heroUnitObject != null)
			{
				heroUnitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] =
					Mathf.Max(1, Mathf.RoundToInt(stage.HeroMoveSpeed * InputContributor.STAT_PER_UNIT_PER_SECOND));
			}


			if (stage.HeroArchetype != null)
			{
				TowerDefenseWeapon heroWeapon = heroUnitObject.GetComponent<TowerDefenseWeapon>();
				if (heroWeapon == null)
					heroWeapon = heroUnitObject.gameObject.AddComponent<TowerDefenseWeapon>();
				// 영웅은 포탑 연구가 아니라 *영웅 갈래*를 탄다 — 한 갈래를 뚫었는데 엉뚱한 게 세지면
				// 성좌를 보고 고른 뜻이 사라진다.
				heroWeapon.Configure(stage.HeroArchetype, targeting, heroCombatant, waveEnemies,
					IsVisibleAt,
					target => DamageMultiplierFor(target) * (1f + ResearchBonus(TowerDefenseResearchEffect.HeroPower)),
					() => Adaptation, () => TowerRangeMultiplier);
			}

			// 표적 등록은 세우는 문이 이미 했다 — 여기서 또 하면 같은 것이 목록에 두 번 들어간다.

			// ★ 마수는 영웅을 *통과한다* (사용자 실증: "영웅 유닛으로 길막이 됨").
			//   이동이 몸통을 쓸어 미끄러지는 방식이라, 영웅을 길목에 세워두면 그 자체가 벽이 된다 —
			//   지어야 막는 게임에서 공짜 벽이다. 영웅은 여전히 지형·건물에 막히되(그건 유지),
			//   마수와의 몸싸움만 서로 무시한다. 때리는 것은 사거리로 하지 몸으로 하지 않는다.
			IgnoreCollisionsWithEnemies(heroGameObject);

			heroTransform = heroGameObject.transform;
			heroTargetPosition = heroTransform.position;
			heroActive = true;
			// 영웅 칸은 영웅이 실제로 서야 생긴다 — 없는데 칸만 있으면 또 「눌리지 않는 칸」이다.
			RefreshAvailableSlots();
			SlotsChanged();

			// 영웅에게도 이름이 있어야 「데려간 아이」가 된다 — 이름 없는 영웅은 커서다.
			RegisterDoll(heroTransform, stage.HeroTint);
			RefreshHeroVision();
		}

		/// <summary>
		/// 영웅 이동 + 움직이는 시야. 건물 시야는 지어질 때 한 번만 계산하면 되지만 영웅은 매 틱 자리가 바뀌므로
		/// **칸이 바뀐 순간에만** 다시 계산한다(매 틱 전면 재계산은 44칸 판에서 그냥 낭비다).
		/// </summary>
		private void TickHero()
		{
			// 쓰러진 뒤 시계 — 다 되면 코어 옆에서 일어난다.
			if (heroActive == false && heroTransform != null && stage != null && stage.HeroRespawnSeconds > 0f)
			{
				heroRespawnRemaining -= TimeManager.TICK;
				if (heroRespawnRemaining <= 0f)
					ReviveHero();
				return;
			}

			if (HasHero == false)
				return;

			if (heroCombatant != null && heroCombatant.IsAlive == false)
			{
				// ★ 쓰러져도 영영 끝은 아니다(개선 목록 8번). 「한 명만 데려간다」의 무게는 *되돌리는 데
				//   드는 값*으로 표현한다 — 돌아올 방법이 하나도 없는 건 무게가 아니라 그냥 벽이다.
				heroActive = false;
				// 걷던 명령을 지운다 — 안 지우면 쓰러진 몸이 반납될 때까지 계속 걷는다.
				heroMovement?.SetMoveDirection(Vector3.zero);
				heroRespawnRemaining = stage.HeroRespawnSeconds;
				Debug.Log($"{nameof(TowerDefenseMatch)}: 영웅 쓰러짐 — {stage.HeroRespawnSeconds:F0}초 뒤 코어에서 일어난다.");
				if (coreCombatant != null)
					PopWorldText("영웅 쓰러짐", heroTransform.position, TextType.Warning);
				// ★ 월드에 뜨는 글자는 그 자리를 보고 있어야만 보인다 — 영웅은 대개 화면 밖에서 죽는다
				//   (혼자 정찰 나가 있으니까). 가장자리 알림으로도 알린다.
				alerts.Raise("영웅이 쓰러졌다", heroTransform != null ? heroTransform.position : coreCombatant.Position,
					Time.time, stage.AlertSeconds);
				return;
			}

			if (heroMovement == null)
				return;

			Vector3 delta = heroTargetPosition - heroTransform.position;
			delta.y = 0f;

			// 도착 판정은 *한 틱에 갈 거리*로 잡는다 — 더 좁게 잡으면 목표를 지나쳤다 되돌아오길 반복하며 떤다.
			float arriveDistance = stage.HeroMoveSpeed * TimeManager.TICK;
			if (delta.sqrMagnitude <= arriveDistance * arriveDistance)
			{
				heroMovement.SetMoveDirection(Vector3.zero);
				return;
			}

			// 방향만 준다 — 실제로 얼마나 가는지는 이동 부품이 매 프레임 정한다(그래서 부드럽고, 벽에 막힌다).
			heroMovement.SetMoveDirection(delta.normalized);
			RefreshHeroVision();
		}

		private float heroRespawnRemaining;

		/// <summary> 영웅이 다시 일어나기까지 남은 시간(0 = 살아있음) — 화면이 「곧 온다」를 말한다. </summary>
		public float HeroRespawnIn => heroActive ? 0f : Mathf.Max(0f, heroRespawnRemaining);

		/// <summary>
		/// 쓰러진 영웅을 코어 옆에서 되살린다 — 자리·체력을 처음처럼 돌리되 *경험은 남긴다*
		/// (그 아이가 다른 아이가 되면 데려간 의미가 없다).
		/// </summary>
		private void ReviveHero()
		{
			if (heroTransform == null || heroCombatant == null || coreCombatant == null)
				return;

			UnitObject heroUnit = heroCombatant.UnitObject;
			if (heroUnit == null)
				return;

			heroTransform.position = coreCombatant.Position + new Vector3(stage.GroundCellSize * 1.5f, 0f, 0f);
			heroUnit.UnitStat[UnitStatType.HP_CUR] = heroUnit.UnitStat[UnitStatType.HP_MAX];
			heroTargetPosition = heroTransform.position;
			heroActive = true;
			heroRespawnRemaining = 0f;

			PopWorldText("영웅 복귀", heroTransform.position, TextType.Heal);
			Debug.Log($"{nameof(TowerDefenseMatch)}: 영웅이 코어에서 다시 일어났다.");
		}

		private Vector2Int heroVisionCell = new Vector2Int(int.MinValue, int.MinValue);
		private int heroVisionSourceIndex = -1;

		private void RefreshHeroVision()
		{
			if (vision == null || mapLayout == null || stageRoot == null || heroTransform == null || stage.HeroVisionRadius <= 0f)
				return;

			Vector2Int cell = mapLayout.WorldToCell(stageRoot.InverseTransformPoint(heroTransform.position));
			if (cell == heroVisionCell)
				return;

			heroVisionCell = cell;
			TowerDefenseVision.Source source = new(cell, stage.HeroVisionRadius);

			// 영웅의 시야원은 *하나*다 — 지나간 자리마다 원을 남기면 판이 통째로 밝아진다(밝힌 자리는
			// Explored 로 남으므로 「가봤다」는 기록은 그대로 유지된다).
			if (heroVisionSourceIndex >= 0 && heroVisionSourceIndex < visionSources.Count)
				visionSources[heroVisionSourceIndex] = source;
			else
			{
				heroVisionSourceIndex = visionSources.Count;
				visionSources.Add(source);
			}

			RefreshVision();
		}

		// ── 이름 붙은 인형 ────────────────────────────────────────────────────────
		// ★ 왜 필요한가: 「광역 포탑」은 물건이고, 물건은 팔 때 아깝지 않다. 이름이 붙는 순간 같은 유닛이
		//   아이가 되어 잃는 것에 무게가 생긴다. 개척은 마녀가 인형을 데리고 나가는 이야기다.

		/// <summary> 화면에 띄울 이름표들 — 사라진 앵커는 조회 겸 정리(멱등). </summary>
		public IReadOnlyList<TowerDefenseDollLabel> DollLabels
		{
			get
			{
				for (int index = dollLabels.Count - 1; index >= 0; index--)
				{
					if (dollLabels[index].IsAlive == false)
					{
						// ★ *잃은* 것만 센다. 판 것은 내가 치운 것이지 뺏긴 것이 아닌데,
						//   둘을 같이 세면 판 요약의 「잃음」이 판매 횟수만큼 부풀어 거짓말을 한다.
						if (soldDolls.Remove(dollLabels[index]) == false)
							LostCount++;
						dollLabels.RemoveAt(index);
					}
				}
				return dollLabels;
			}
		}

		/// <summary> 세워진 인형에게 이름을 준다 + 한 마디 시킨다. 같은 판·같은 순서면 같은 이름. </summary>
		private void RegisterDoll(Transform anchor, Color tint, bool isHarvester = false,
			bool isPlacedBuilding = false, int variant = 0)
		{
			if (anchor == null)
				return;

			int ordinal = nextDollOrdinal++;
			string name = TowerDefenseNames.For(MapSeed, ordinal);
			TowerDefenseDollLabel doll = new(anchor, name, tint,
				stage.BuildingLevelBaseCost, stage.BuildingLevelGrowth)
			{
				BuildingId = MapSeed + ordinal * 7919,
				IsHarvester = isHarvester,
				IsPlacedBuilding = isPlacedBuilding,
				Variant = variant,
			};
			dollLabels.Add(doll);
			PopWorldText("「" + name + "」 " + TowerDefenseNames.Greeting(MapSeed, ordinal), anchor.position, TextType.Heal);
		}

		/// <summary>
		/// 보급 원점(코어·전초기지)에 사거리 원 — 「사슬이 여기서 출발해 이만큼 닿는다」.
		/// 이 원이 없으면 채집을 어디에 세워야 이어지는지가 순수한 시행착오가 된다.
		/// </summary>
		private void ShowSupplyReachRing(Transform origin)
		{
			if (origin == null || stage == null || EffectiveSupplyReach <= 0f)
				return;

			Color ringColor = stage.HarvesterTint;
			ringColor.a = 0.18f;
			TowerDefenseRing ring = TowerDefenseRing.Create(origin, "SupplyReachRing", ringColor, 0.06f, 0.03f);
			ring.SetRadius(EffectiveSupplyReach);
			supplyRings.Add(ring); // 연구로 보급이 길어지면 이 원도 따라 커져야 한다.
		}

		/// <summary>
		/// 그 유닛이 무엇인지 사람 말로(툴팁). 화면에 서 있는 것이 「무엇이고 얼마나 버티는지」를 물어볼
		/// 수단이 없으면, 색과 크기만으로 짐작해야 한다(사용자 요청: 유닛 툴팁).
		/// 모르는 대상이면 빈 문자열 — 아무거나 지어내지 않는다.
		/// </summary>
		public string DescribeUnit(MatchCombatant combatant)
		{
			if (combatant == null || combatant.UnitObject == null)
				return string.Empty;

			Transform unit = combatant.transform;
			int currentHp = combatant.UnitObject.UnitStat[UnitStatType.HP_CUR];
			int maxHp = combatant.UnitObject.UnitStat[UnitStatType.HP_MAX];

			// 마수 — 지금 얼마나 남았고 잡으면 얼마인지.
			if (combatant.TeamId == ATTACKER_TEAM)
			{
				string bounty = enemyBountyById.TryGetValue(combatant.CombatantId, out int reward)
					? "  ·  잡으면 +" + Mathf.RoundToInt(reward * boons.BountyMultiplier)
					: string.Empty;
				return "마수\n체력 " + currentHp + " / " + maxHp + bounty;
			}

			TowerDefenseDollLabel label = FindDollLabel(unit);
			string name = label != null ? label.Name : "인형";

			// ★ 코어를 *제일 먼저* 가린다 (WM-200 실측). 코어도 무기를 들고 있어서 아래 포탑 가지가
			//   먼저 낚아채 갔고, 그 아래 코어 설명은 통째로 죽은 가지였다 — 화면엔 「인형 …
			//   같은 자리에 같은 종류를 또 지으면 승급」이라 떴다. 코어는 다시 못 짓는데.
			//   무엇인가(역할)는 무엇을 들었나(무기)보다 앞선다.
			if (coreCombatant == combatant)
			{
				TowerDefenseWeapon coreWeapon = unit.GetComponent<TowerDefenseWeapon>();
				return "코어\n체력 " + currentHp + " / " + maxHp
					+ (coreWeapon != null
						? "\n사거리 " + coreWeapon.Range.ToString("0.#") + "  ·  피해 " + coreWeapon.CurrentDamage
						: string.Empty)
					+ "\n여기까지 새면 목숨이 준다";
			}

			// 포탑 — 무기가 붙어 있으면 그 수치가 정본이다(화면과 규칙이 같은 곳을 읽는다).
			TowerDefenseWeapon weapon = unit.GetComponent<TowerDefenseWeapon>();
			if (weapon != null)
			{
				bool isHero = HasHero && heroTransform == unit;
				return (isHero ? name + " (영웅)" : name + (label != null && label.Level > 1 ? " ★" + label.Level : ""))
					+ "\n체력 " + currentHp + " / " + maxHp
					+ "\n사거리 " + weapon.Range.ToString("0.#") + "  ·  피해 " + weapon.CurrentDamage
					+ (isHero ? "\n핫바에서 「영웅 이동」을 고르고 찍으면 그리 간다" : "\n같은 자리에 같은 종류를 또 지으면 승급");
			}

			// 채집 인형 — 무엇을 얼마나 캐고, 이어져 있는지.
			if (harvesterTransforms.Contains(unit))
			{
				bool outer = harvesterIsOuter.TryGetValue(unit, out bool isOuter) && isOuter;
				bool connected = label == null || label.Disconnected == false;
				return name + " (채집 인형)"
					+ "\n체력 " + currentHp + " / " + maxHp
					+ "\n" + (outer ? "정수" : "자원") + " ×" + HarvesterMultiplierOf(unit).ToString("0.0")
					+ "\n" + (connected ? "보급 이어짐" : "⚠ 보급 끊김 — 한 푼도 안 들어온다");
			}

			return name + "\n체력 " + currentHp + " / " + maxHp;
		}


		// ── 전기 ─────────────────────────────────────────────────────────────────
		// 이 층은 통째로 떨어져 나갔다 — 매치가 4000줄이 넘어 「한 덩어리가 너무 많은 걸 아는」 병이
		// 실제 결함으로 몇 번 나왔다. 여기 남는 것은 *물어보고 넘겨주는 일*뿐이다.
		private readonly TowerDefensePowerGrid powerGrid = new();

		/// <summary> 전체 전기 용량 / 요구 — 화면이 「얼마나 모자라나」를 말한다. </summary>
		public int PowerCapacity => powerGrid.Capacity;
		public int PowerDemand => powerGrid.Demand;

		/// <summary> 전기를 못 받아 멈춘 건물 수. </summary>
		public int UnpoweredBuildings => powerGrid.UnpoweredBuildings;

		/// <summary> 신호장을 화면에 그린다. 무대가 있어야 그릴 자리가 생기므로 여기서 늦게 만든다. </summary>
		private void TickSignalView()
		{
			if (stageRoot == null || stage == null)
				return;

			if (signalView == null)
				signalView = TowerDefenseSignalView.Create(stageRoot);

			signalView.Tick(powerGrid.Field, stage, Time.deltaTime);
		}

		private void RefreshPower()
		{
			if (coreCombatant == null)
				return;

			powerGrid.Refresh(stage, coreCombatant.Position, bonusPowerCapacity,
				harvesterTransforms.Contains, FindDollLabel, Time.deltaTime);
		}

		/// <summary>
		/// 발전 인형 배치 — 자원으로 짓고, 범위 안 건물에 전기를 댄다.
		/// 보급 사슬의 징검다리도 겸한다(내 건물이므로) — 전기를 늘리는 일이 곧 땅을 넓히는 일이 된다.
		/// </summary>
		public bool TryPlaceGenerator(Vector3 worldPosition)
		{
			if (core == null || pool == null || timeManager == null || targeting == null)
				return false;
			if (stage.HarvesterUnit == null || stage.HarvesterUnit.Prefab == null)
				return false;

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey))
				return Reject("여긴 이미 찼다", worldPosition);
			if (ValidateSite(worldPosition) == false)
				return false;
			int generatorCost = CostOf(TowerDefensePlaceableKind.Generator);
			if (core.TrySpend(generatorCost) == false)
				return Reject($"자원 부족 {core.Resource}/{generatorCost}", worldPosition);

			occupiedCells.Add(cellKey);
			StartCoroutine(SpawnDefensiveUnitRoutine(
				stage.HarvesterUnit, null, worldPosition, isHarvester: false, incomeMultiplier: 1f,
				towerArchetype: null, isOuterNode: false, isGenerator: true));
			return true;
		}

		/// <summary>
		/// 코어에서 연구를 한 단계 올린다 — 정수로 산다(사용자 지시: "연구소 건물 없애고, 코어 건물에서
		/// 연구를 진행할 수 있게").
		///
		/// ★ 왜 건물을 없앴나: 짓는 것(자리를 차지하고 지켜야 하는 것)과 키우는 것(판 전체에 걸리는 것)은
		///   성격이 다른 행위인데 같은 핫바에 섞여 있었다. 연구를 코어에 두면 「어디에 지을까」를 고민할
		///   필요 없는 대신 *코어를 지키는 이유*가 하나 더 늘어난다.
		/// 값은 단계마다 오른다 — 무한히 싸게 쌓이면 그건 선택이 아니다.
		/// </summary>
		/// <summary>
		/// 값 없이 연구 한 단계 — **성좌의 큰 마디를 뚫었을 때** 부른다.
		///
		/// ★ 왜 값이 없나: 마디를 찍을 때 이미 정수를 치렀다. 여기서 또 받으면 한 번 뚫는 데 두 번 낸다.
		/// ★ 왜 필요한가: 건물 해금은 연구 *단계*가 정한다. 성좌가 단계를 못 올리면 「성좌를 다 뚫었는데
		///   지을 수 있는 건 그대로」가 되어, 연구창이 판을 바꾸지 못한다.
		/// </summary>
		public void GrantResearchLevel()
		{
			LabCount++;
			RefreshAvailableSlots();
			SlotsChanged();
			if (coreCombatant != null)
				PopWorldText("연구 " + LabCount + "단계", coreCombatant.Position, TextType.Exp);
			Debug.Log($"{nameof(TowerDefenseMatch)}: 성좌로 연구 {LabCount}단계 — 새 칸이 열린다.");
		}

		public bool TryResearch()
		{
			if (core == null || stage == null)
				return false;

			int cost = ResearchCost;
			// ★ 초반 연구는 *일반 자원*으로 산다(사용자 지시). 정수는 바깥 노드에서만 나는데, 그걸
			//   초반 해금의 통로로 두면 「연구로 하나씩 연다」가 시작부터 잠긴다 — 실제로 그랬다.
			//   고급 테크(정수 단계)부터가 개척을 강요하는 자리다.
			if (ResearchUsesEssence)
			{
				if (core.TrySpendEssence(cost) == false)
				{
					if (coreCombatant != null)
						Reject(EssenceShortText(cost), coreCombatant.Position);
					return false;
				}
			}
			else if (core.TrySpend(cost) == false)
			{
				if (coreCombatant != null)
					Reject($"자원 부족 {core.Resource}/{cost}", coreCombatant.Position);
				return false;
			}

			LabCount++;
			RefreshAvailableSlots();
			SlotsChanged();
			if (coreCombatant != null)
				PopWorldText("연구 " + LabCount + "단계", coreCombatant.Position, TextType.Exp);
			Debug.Log($"{nameof(TowerDefenseMatch)}: 연구 {LabCount}단계 — 모든 포탑 피해 배수 {TowerDamageMultiplier:F2}");
			return true;
		}

		/// <summary> 다음 연구가 정수를 먹나(고급 테크) — 아니면 일반 자원이다. </summary>
		public bool ResearchUsesEssence => stage != null && LabCount + 1 >= stage.ResearchEssenceFromLevel;

		/// <summary> 다음 연구 단계 값 — 단계마다 오른다. 초반은 자원, 고급 테크부터 정수. </summary>
		public int ResearchCost
		{
			get
			{
				if (stage == null)
					return 0;
				int baseCost = ResearchUsesEssence ? stage.LabEssenceCost : stage.LabResourceCost;
				return Mathf.Max(1, Mathf.RoundToInt(baseCost * (LabCount + 1) * boons.ResearchCostMultiplier));
			}
		}

		/// <summary>
		/// 지금 쓸 수 있는 칸 — 화면(핫바)과 입력(배치)이 *같은 목록*을 읽는다.
		///
		/// ★ 여기가 해금의 단일 정본이다. 예전엔 칸 번호 → 종류가 고정 산술로 두 곳에 박혀 있어,
		///   해금으로 칸 수가 변하는 순간 「함정을 골랐는데 전초기지가 지어진다」가 된다.
		/// 순서는 손이 기억한다 — 새로 열린 것은 *뒤에* 붙는다(앞이 밀리면 손가락이 헛나간다).
		/// </summary>
		public System.Collections.Generic.IReadOnlyList<TowerDefenseSlot> AvailableSlots => availableSlots;

		private readonly System.Collections.Generic.List<TowerDefenseSlot> availableSlots = new();

		/// <summary> 해금 목록을 다시 만든다 — 연구 단계가 오를 때·판이 시작할 때. </summary>
		private void RefreshAvailableSlots()
		{
			availableSlots.Clear();
			if (stage == null)
				return;

			// ★ 해금 계산은 여기 없다 (WM-200) — 연구 창도 같은 것을 알아야 하는데, 각자 계산하면
			//   *창이 약속한 것과 실제로 열리는 것이 어긋난다*. 표는 하나고, 규칙층은 「여기까지」를
			//   잘라 쓰기만 한다.
			TowerDefenseUnlockSchedule.Available(UnlockLevels, TowerArchetypeCount, LabCount, unlockScratch, availableSlots);

			// ★ 영웅은 핫바에서 뺐다(사용자 지시: "영웅 이동 따로 핫바 두지 않았으면"). 핫바는
			//   *짓는 것*의 자리인데 영웅은 보내는 것이라 뜻이 어긋났고, WASD(시점)와도 헷갈렸다.
			//   이제 빈 땅 우클릭이 영웅을 보낸다 — 대상이 있으면 판매, 없으면 이동(RTS 관용).
		}

		private readonly System.Collections.Generic.List<TowerDefenseUnlockEntry> unlockScratch = new();

		/// <summary> 무대가 정한 해금 단계 수치 — 계산은 순수 표가 한다. </summary>
		private TowerDefenseUnlockLevels UnlockLevels => new(
			stage.TowerUnlockLevel, stage.WallUnlockLevel, stage.TrapUnlockLevel,
			stage.GeneratorUnlockLevel, stage.OutpostUnlockLevel, stage.TowerVariantUnlockStep);

		/// <summary>
		/// 연구 길 전체 — 「몇 단계에 무엇이 열리나」. 연구 창이 이걸 그린다.
		/// 지금 열린 것과 *같은 표*에서 나오므로 창이 약속한 것은 반드시 열린다.
		/// </summary>
		public void DescribeUnlockPath(System.Collections.Generic.List<TowerDefenseUnlockEntry> into)
		{
			if (stage == null)
			{
				into?.Clear();
				return;
			}
			TowerDefenseUnlockSchedule.Build(UnlockLevels, TowerArchetypeCount, into);
		}

		/// <summary> 해금이 바뀌었다 — 화면이 핫바를 다시 그려야 한다. </summary>
		public event System.Action SlotsChanged = delegate { };

		/// <summary> 지금 연구 단계 — 화면이 코어를 골랐을 때 보여준다. </summary>
		public int ResearchLevel => LabCount;

		/// <summary> 그 대상이 코어인가 — 화면이 「연구」 패널을 띄울지 정한다. </summary>
		public bool IsCore(MatchCombatant combatant) => combatant != null && combatant == coreCombatant;

		/// <summary>
		/// 건물마다 「지금 얼마나 찼나 / 일하고 있나」를 이름표에 채워 넣는다.
		/// 화면이 유닛에게 직접 캐물으면 표시와 규칙이 두 경로로 갈라지므로, 규칙을 아는 쪽이 채운다.
		/// </summary>
		private void RefreshBuildingProgress()
		{
			foreach (TowerDefenseDollLabel label in dollLabels)
			{
				if (label.IsAlive == false)
					continue;

				bool powered = IsPowered(label.Anchor);
				TowerDefenseWeapon weapon = label.Anchor.GetComponent<TowerDefenseWeapon>();
				if (weapon != null)
				{
					label.ReadyRatio = weapon.ReadyRatio;
					label.Working = powered;
					continue;
				}

				if (harvesterTransforms.Contains(label.Anchor))
				{
					// 채집은 「다음 정산까지」가 곧 진행이다 — 시계가 돌면 들어온다.
					// ★ 단, *일하고 있을 때만* 찬다. 멈춘 인형의 바가 계속 차오르면 화면이 거짓말을 한다
					//   (사용자 실증: "전기 없다고 뜨는데 채굴은 또 되는 것 같고"). 규칙은 이미 한 푼도
					//   안 주고 있었으므로, 갈라진 것은 그림뿐이었다 — 안 도는 것은 안 차야 한다.
					bool working = powered && label.Disconnected == false;
					label.ReadyRatio = working && core != null && stage.Rules.IncomeInterval > 0f
						? 1f - core.NextIncomeIn / stage.Rules.IncomeInterval
						: 0f;
					label.Working = working;
					continue;
				}

				label.ReadyRatio = 1f; // 패시브 — 언제나 준비됨.
				label.Working = powered;
			}
		}

		/// <summary>
		/// 마수가 죽은 자리 *사거리 안*의 포탑들에게 경험치 — 「처치 관여」(사용자 지시).
		///
		/// ★ 왜 마지막 한 방이 아니라 관여인가: 마지막 타격만 세면 연사 포탑이 경험치를 독식하고,
		///   길목을 지키느라 계속 쏘던 포탑이 아무것도 못 받는다. 관여로 세면 *자리를 잘 잡은 것*이 자란다.
		/// </summary>
		private void AwardKillExperience(Vector3 deathPosition)
		{
			if (stage == null || stage.KillExperience <= 0)
				return;

			foreach (TowerDefenseDollLabel doll in dollLabels)
			{
				if (doll.IsAlive == false || doll.IsHarvester)
					continue;

				TowerDefenseWeapon weapon = doll.Anchor.GetComponent<TowerDefenseWeapon>();
				if (weapon == null)
					continue;
				if ((doll.Anchor.position - deathPosition).sqrMagnitude > weapon.Range * weapon.Range)
					continue;

				doll.Progress.AddExperience(Mathf.RoundToInt(stage.KillExperience * boons.ExperienceMultiplier));
			}
		}

		/// <summary> 정산 때 채집 인형에게 경험치 — 캐는 것도 일이다. </summary>
		private void AwardHarvestExperience()
		{
			if (stage == null || stage.HarvestExperience <= 0)
				return;

			foreach (TowerDefenseDollLabel doll in dollLabels)
			{
				if (doll.IsAlive == false || doll.IsHarvester == false)
					continue;
				if (doll.Disconnected || doll.Unpowered)
					continue; // 멈춘 채집은 배우지도 않는다.

				doll.Progress.AddExperience(Mathf.RoundToInt(stage.HarvestExperience * boons.ExperienceMultiplier));
			}
		}

		/// <summary>
		/// 흐른 시간만큼 마수를 단단하게 + 카드로 고른 감속을 건다.
		///
		/// ★ 왜 시간인가: 실시간에서 웨이브는 시계가 부른다 — 웨이브 수로 난이도를 올리면 플레이어가
		///   무엇을 하든 똑같이 오른다. 「빨리 정리했다」와 「겨우 버텼다」가 구분되지 않는다.
		///   시간으로 올리면 *오래 끌수록 아프다* 가 되어 둥지를 부수러 나갈 이유가 생긴다.
		/// ★ 상한을 두는 이유: 무한히 오르면 어느 순간부터는 무엇을 해도 지는 판이 된다 — 그건 난이도가
		///   아니라 타이머다.
		/// </summary>
		private void ApplyPressure(UnitObject enemyUnit)
		{
			if (enemyUnit == null || core == null)
				return;

			float pressure = core.Pressure;
			if (pressure > 1f)
			{
				int scaledHp = Mathf.Max(1, Mathf.RoundToInt(enemyUnit.UnitStat[UnitStatType.HP_MAX] * pressure));
				enemyUnit.UnitStat[UnitStatType.HP_MAX] = scaledHp;
				enemyUnit.UnitStat[UnitStatType.HP_CUR] = scaledHp;
			}

			// 카드로 고른 「무거운 걸음」은 *앞으로 나오는* 마수에만 걸린다(이미 걷는 것을 늦추면
			// 고른 순간 판이 통째로 멎어 선택이 아니라 버튼이 된다).
			float speedMultiplier = boons.EnemySpeedMultiplier;
			if (speedMultiplier < 1f)
			{
				enemyUnit.UnitStat[UnitStatType.MOVEMENT_SPEED] =
					Mathf.Max(1, Mathf.RoundToInt(enemyUnit.UnitStat[UnitStatType.MOVEMENT_SPEED] * speedMultiplier));
			}
		}

		// ── 판 기록 ───────────────────────────────────────────────────────────────
		// ★ 왜 필요한가 (개선 목록 24번): 지금은 지고 나면 「몇 분 버팀」 한 줄뿐이라 *왜 졌는지*를
		//   되짚을 수단이 없다. 무엇을 몇 개 지었고, 몇 개를 잃었고, 마수가 가장 많을 때 몇이었는지가
		//   남아야 다음 판이 달라진다 — 안 남으면 매 판이 같은 실수의 반복이 된다.
		// 방금 판 인형들 — 다음 정리에서 「잃음」으로 세지 않기 위한 표시.
		private readonly HashSet<TowerDefenseDollLabel> soldDolls = new();

		public int BuiltCount { get; private set; }
		public int LostCount { get; private set; }
		public int KilledCount { get; private set; }
		public int PeakEnemies { get; private set; }
		public int LeakedCount { get; private set; }

		// ── 판 도중 저장 ──────────────────────────────────────────────────────────
		// ★ 「장면 통째」가 아니라 *다시 지을 수 있는 최소 정보*만 담는다 — 판은 씨앗에서 다시 태어나고
		//   내가 한 일은 「무엇을 어디에 세웠나」로 전부 적힌다. 그러면 프리팹이 바뀌어도 저장이 살아남는다.
		// ★ 걷고 있는 마수는 저장하지 않는다 — 되살리는 것보다 *다시 몰려오게* 두는 편이 규칙이 단순하고,
		//   불러온 직후의 짧은 숨돌릴 틈이 오히려 자연스럽다.

		/// <summary>
		/// 확인 도구 전용 — 값만 채운다. **배치 규칙은 우회하지 않는다**(보급·암반·점유 그대로).
		/// 값이 모자라 확인 자체를 못 하던 것들(전초기지·바깥 채집)을 라이브로 보기 위한 최소 통로.
		/// </summary>
		public void GrantForVerification(int resource, int essence)
		{
			if (core == null)
				return;

			core.AddResource(resource);
			core.AddEssence(essence);
		}

		/// <summary>
		/// 확인용 코어 경험치 — 카드가 실제로 뜬 화면을 재려면 레벨이 올라야 한다.
		/// ★ 값만 준다 — 카드가 나오는 규칙(무엇이 몇 장 나오나)은 그대로 통과시켜야 확인이 의미가 있다.
		/// </summary>
		public void GrantCoreExperienceForVerification(int amount)
		{
			AwardCoreExperience(amount);
		}

		/// <summary>
		/// 확인용 건물 경험치 — 강화 선택지가 실제로 걸린 화면을 재려면 그 건물이 자라야 한다.
		/// ★ 값만 준다 — 무엇이 몇 장 나오나는 그대로 통과시켜야 확인이 의미가 있다.
		/// </summary>
		public bool GrantBuildingExperienceForVerification(MatchCombatant combatant, int amount)
		{
			TowerDefenseDollLabel doll = FindDoll(combatant);
			if (doll == null)
				return false;

			doll.Progress.AddExperience(amount);
			return true;
		}

		/// <summary> 지금 판을 저장 가능한 형태로 뽑는다(끝난 판이면 null). </summary>
		public TowerDefenseSaveData CaptureSave()
		{
			if (core == null || stage == null || core.Outcome != TowerDefenseOutcome.InProgress)
				return null;

			TowerDefenseSaveData save = new()
			{
				StageId = stage.ID.ToString(),
				MapSeed = MapSeed,
				MapWidth = mapLayout != null ? mapLayout.Width : 0,
				MapLength = mapLayout != null ? mapLayout.Length : 0,
				Difficulty = (int)Difficulty,
				ElapsedSeconds = core.ElapsedSeconds,
				WaveIndex = core.WaveIndex,
				Resource = core.Resource,
				Essence = core.Essence,
				Lives = core.Lives,
				CoreLevel = CoreLevel,
				CoreExperience = coreProgress.Experience,
				CorePendingChoices = CorePendingChoices,
				ResearchLevel = LabCount,
				NestsDestroyed = NestsDestroyed,
			};

			// 이 판의 성격 — 고른 카드와 부순 둥지. 이게 빠지면 이어한 판이 「같은 판」이 아니다.
			foreach (TowerDefenseBoonKind kind in boons.TakenKinds)
				save.TakenBoons.Add((int)kind);
			// 성좌 자국 — 정본은 화면이 들고 있으므로 물어서 받아 적는다(값을 두 곳에 두지 않는다).
			CollectResearchInto(save.TakenResearch);
			save.DestroyedNestPositions.AddRange(destroyedNestPositions);

			foreach (TowerDefenseDollLabel doll in dollLabels)
			{
				// 사람이 세운 것만 적는다 — 영웅처럼 판이 스스로 만드는 인형까지 적으면 이어할 때마다 는다.
				if (doll.IsAlive == false || doll.IsPlacedBuilding == false)
					continue;

				TowerDefenseBuildingSave building = new()
				{
					Kind = (int)(doll.IsHarvester ? TowerDefensePlaceableKind.Harvester : TowerDefensePlaceableKind.Tower),
					Variant = doll.Variant,
					Position = stageRoot.InverseTransformPoint(doll.Anchor.position),
					Level = doll.Level,
					Experience = doll.Progress.Experience,
					PendingChoices = doll.Progress.PendingChoices,
					Perks = new List<int>(),
				};
				foreach (TowerDefenseBuildingPerk perk in doll.Progress.Taken)
					building.Perks.Add((int)perk);

				save.Buildings.Add(building);
			}

			// 함정도 적는다 — 자리와 남은 횟수를 같이. 안 적으면 깔아둔 함정이 이어하는 순간 통째로 사라진다.
			if (stageRoot != null)
			{
				foreach (TowerDefenseTrap trap in stageRoot.GetComponentsInChildren<TowerDefenseTrap>(true))
				{
					save.Traps.Add(new TowerDefenseTrapSave
					{
						Position = stageRoot.InverseTransformPoint(trap.transform.position),
						ChargesLeft = trap.ChargesLeft,
					});
				}
			}

			// 전초기지도 적는다 — 이건 보급의 *새 원점*이라 안 적으면 그 일대가 통째로 사슬 밖이 된다.
			foreach (Transform outpost in outposts)
			{
				if (outpost != null)
					save.Outposts.Add(stageRoot.InverseTransformPoint(outpost.position));
			}

			// 벽도 적는다 — 벽은 보급 징검다리라, 안 적으면 사슬이 짧아져 그 너머 포탑이 되살아나지 못한다.
			foreach (Vector2Int wallCell in wallCells)
				save.Walls.Add(mapLayout != null ? mapLayout.CellToWorld(wallCell) : Vector3.zero);

			return save;
		}

		/// <summary>
		/// 저장을 이어받아 판을 그 상태로 되돌린다 — **Begin 직전**에 부른다.
		///
		/// ★ 왜 직전인가: 지형이 같아야 「이어하기」다. 판은 씨앗에서 태어나므로 씨앗을 먼저 넘겨야
		///   같은 땅이 다시 나온다. Begin 뒤에 부르면 이미 다른 땅이 깔린 뒤라 내 건물만 엉뚱한 자리에
		///   다시 서게 된다. 그래서 여기서는 예약만 하고, 판을 깔 때 씨앗을, 판이 선 뒤 건물을 얹는다.
		/// </summary>
		public void RestoreSave(TowerDefenseSaveData save)
		{
			if (save == null || save.IsResumable == false)
				return;

			pendingRestore = save;
			Debug.Log($"{nameof(TowerDefenseMatch)}: 이어하기 — {save.Describe()}");
		}

		private TowerDefenseSaveData pendingRestore;

		/// <summary>
		/// 실제 복원 — 건물 스폰이 코루틴이라 Begin 이 끝난 *다음*에 한 채씩 다시 세운다.
		/// 값(자원·정수·목숨)은 먼저 맞춰야 세우는 도중에 「돈이 없어 거절」이 나지 않는다.
		/// </summary>
		private IEnumerator RestoreRoutine(TowerDefenseSaveData save)
		{
			core.AddResource(Mathf.Max(0, save.Resource - core.Resource));
			core.AddEssence(Mathf.Max(0, save.Essence - core.Essence));
			LabCount = save.ResearchLevel;
			RefreshAvailableSlots(); // 이어하면 그때 열려 있던 칸이 그대로 서야 한다.
			NestsDestroyed = save.NestsDestroyed;

			// 판의 시계·목숨·코어 성장 — 이게 안 돌아오면 오래 버틴 판이 이어하는 순간 처음으로 되감긴다.
			core.Restore(save.ElapsedSeconds, save.WaveIndex, save.Lives);
			coreProgress.Restore(save.CoreLevel, save.CoreExperience, save.CorePendingChoices, null);

			// 고른 카드 — 종류만 적어뒀고 값은 이 판의 규칙에서 다시 나온다(같은 규칙 = 같은 값).
			// 즉시 효과(목숨·정수·자원)는 다시 주지 않는다 — 그 결과는 위에서 이미 되돌렸다.
			// 성좌 — 화면에 자국을 되돌리고, 효과도 같이 다시 쌓는다(둘 중 하나만 하면 갈라진다).
			if (save.TakenResearch != null && save.TakenResearch.Count > 0)
				RestoreResearchFrom(save.TakenResearch);

			if (save.TakenBoons != null)
			{
				foreach (int kind in save.TakenBoons)
					boons.Take(TowerDefenseDraft.Make((TowerDefenseBoonKind)kind, stage.DraftRules));
				core.IncomeMultiplier = boons.IncomeMultiplier * (1f + ResearchBonus(TowerDefenseResearchEffect.HarvestYield));
			}

			RestoreInProgress = true;

			// ★ 전초기지가 가장 먼저다 — 보급의 원점이라, 이게 서야 그 일대의 자리가 열린다.
			if (save.Outposts != null && stageRoot != null)
			{
				foreach (Vector3 outpostLocal in save.Outposts)
				{
					core.AddEssence(stage.OutpostEssenceCost); // 되살리는 것은 짓는 일이 아니다.
					if (TryPlaceOutpost(stageRoot.TransformPoint(outpostLocal)) == false)
						core.TrySpendEssence(stage.OutpostEssenceCost);
					yield return null;
				}
			}

			// ★ 벽을 **먼저** 세운다 — 벽이 보급을 뻗어 주므로, 뒤에 놓을 포탑의 자리가 그때 열린다.
			if (save.Walls != null && stageRoot != null)
			{
				int wallsBack = 0;
				foreach (Vector3 wallLocal in save.Walls)
				{
					core.AddResource(stage.WallCost); // 되살리는 것은 짓는 일이 아니다 — 값은 아래에서 정확히 맞춘다.
					if (TryPlaceWall(stageRoot.TransformPoint(wallLocal)))
						wallsBack++;
					else
						core.TrySpend(stage.WallCost);
					yield return null;
				}
				if (wallsBack < save.Walls.Count)
					Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 이어하기 — 벽 {save.Walls.Count - wallsBack}칸을 못 되살렸다.");
			}

			// 함정은 보급 사슬과 무관하다 — 자리만 맞으면 서므로 한 번에 되돌린다.
			if (save.Traps != null && stageRoot != null)
			{
				foreach (TowerDefenseTrapSave trapSave in save.Traps)
				{
					Vector3 trapWorld = stageRoot.TransformPoint(trapSave.Position);
					core.AddResource(stage.TrapCost); // 되살리는 것은 짓는 일이 아니다.
					if (TryPlaceTrap(trapWorld) == false)
					{
						core.TrySpend(stage.TrapCost);
						continue;
					}

					// ★ 남은 횟수를 도로 얹는다 — 안 하면 다 쓴 함정이 새것으로 살아나 「닳는다」가 무효가 된다.
					foreach (TowerDefenseTrap trap in stageRoot.GetComponentsInChildren<TowerDefenseTrap>(true))
					{
						if ((trap.transform.position - trapWorld).sqrMagnitude <= 1f)
						{
							trap.RestoreCharges(trapSave.ChargesLeft);
							break;
						}
					}
					yield return null;
				}
			}

			// ★ **순서에 기대지 않는다.** 지을 수 있는 자리는 「보급이 닿는 곳」이고, 보급은 내 건물이
			//   징검다리라 *다른 건물이 먼저 서야* 뻗어 나간다. 저장 순서대로 한 번만 훑으면 바깥 것이
			//   「보급이 안 닿는다」로 거절되고 그대로 사라진다 — 라이브 실측에서 9채가 3채로 줄었다.
			//   그래서 **놓을 수 있는 것을 놓고, 놓은 게 있으면 다시 훑는다.** 더 못 놓으면 멈춘다.
			List<TowerDefenseBuildingSave> pending = new(save.Buildings);
			List<TowerDefenseBuildingSave> stillPending = new();

			while (pending.Count > 0)
			{
				stillPending.Clear();
				int placedThisPass = 0;

				foreach (TowerDefenseBuildingSave building in pending)
				{
					Vector3 world = stageRoot.TransformPoint(building.Position);
					// ★ 되살리는 것은 *짓는 일이 아니다* — 이미 치른 값을 또 치르면 이어할 때마다 지갑이 깎인다.
					//   배치 경로를 그대로 쓰되(자리·보급 규칙은 지켜야 한다) 그 값만큼 미리 채워 넣고,
					//   전부 세운 뒤 저장된 액수로 정확히 되돌린다.
					int restoreCost = building.Kind == (int)TowerDefensePlaceableKind.Harvester
						? stage.HarvesterCost
						: TowerCostAt(building.Variant);
					core.AddResource(restoreCost);

					bool placed = building.Kind == (int)TowerDefensePlaceableKind.Harvester
						? TryPlaceHarvester(world)
						: TryPlaceTower(world, building.Variant);

					if (placed == false)
					{
						// 아직 못 놓는다 = *지금은* 보급이 안 닿는다. 다음 통과에서 다시 본다.
						// 미리 채운 값은 도로 뺀다 — 안 그러면 통과할 때마다 지갑이 부풀어 오른다.
						core.TrySpend(restoreCost);
						stillPending.Add(building);
						continue;
					}

					placedThisPass++;

					yield return null; // 스폰이 끝나야 그 인형에 성장을 얹을 수 있다.
					yield return null;

					TowerDefenseDollLabel doll = FindDollLabel(world);
					if (doll == null)
						continue;

					// 고른 것들은 *효과*를 다시 얹어야 한다(수치가 붙는 일이라 기록만으론 부족).
					if (building.Perks != null)
					{
						foreach (int perk in building.Perks)
							ApplyPerk(doll, (TowerDefenseBuildingPerk)perk);
					}

					// 자란 단계·경험치는 기록 그대로 얹는다 — 경험치로 되감으면 선택지가 다시 쌓인다.
					doll.Progress.Restore(building.Level, building.Experience, building.PendingChoices, doll.Progress.Taken);
					doll.Level = building.Level;

					// 승급은 무기에도 걸려 있다 — 이름표만 올리면 사거리·피해가 1단계인 채로 남는다.
					TowerDefenseWeapon weapon = doll.Anchor != null ? doll.Anchor.GetComponent<TowerDefenseWeapon>() : null;
					if (weapon == null)
						continue;
					while (weapon.Level < building.Level && weapon.TryUpgrade())
					{
					}
					RefreshTowerRing(weapon.gameObject);
				}

				// 한 바퀴 돌았는데 하나도 못 놓았으면 더 돌아도 결과가 같다 — 그 자리들은 진짜로 못 놓는다.
				if (placedThisPass == 0)
				{
					if (stillPending.Count > 0)
					{
						Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 이어하기 — {stillPending.Count}채를 되살릴 자리가 없다"
							+ " (지형이 바뀌었거나 보급이 끊긴 자리).");
					}
					break;
				}

				pending.Clear();
				pending.AddRange(stillPending);
			}

			// 지갑을 저장된 액수로 정확히 맞춘다 — 남거나 모자라면 이어할 때마다 판이 조금씩 달라진다.
			core.AddResource(Mathf.Max(0, save.Resource - core.Resource));
			if (core.Resource > save.Resource)
				core.TrySpend(core.Resource - save.Resource);
			core.AddEssence(Mathf.Max(0, save.Essence - core.Essence));
			if (core.Essence > save.Essence)
				core.TrySpendEssence(core.Essence - save.Essence);

			RestoreInProgress = false;

			Debug.Log($"{nameof(TowerDefenseMatch)}: 이어하기 복원 끝 — 건물 {dollLabels.Count}채"
				+ $" · 자원 {core.Resource}/{save.Resource} · 정수 {core.Essence}/{save.Essence}.");
		}

		/// <summary> 그 자리에 선 인형의 이름표 — 복원이 방금 세운 것을 다시 찾는다. </summary>
		private TowerDefenseDollLabel FindDollLabel(Vector3 worldPosition)
		{
			foreach (TowerDefenseDollLabel label in dollLabels)
			{
				if (label.IsAlive && (label.Anchor.position - worldPosition).sqrMagnitude <= 1f)
					return label;
			}
			return null;
		}

		/// <summary> 판이 끝난 뒤 화면이 그대로 읽는 한 덩어리 요약. </summary>
		public string BuildSummary()
		{
			string newline = System.Environment.NewLine;
			// 씨앗을 적어둔다 — 끝난 직후가 「이 판 해봐」를 건네기 가장 자연스러운 순간이다.
			return "씨앗 " + MapSeed + newline
				+ "지음 " + BuiltCount + "  ·  잃음 " + LostCount + newline
				+ "잡음 " + KilledCount + "  ·  샌 마수 " + LeakedCount + newline
				+ "한때 " + PeakEnemies + "마리까지  ·  마수 강도 x" + Pressure.ToString("0.0");
		}

		/// <summary> 지금 마수에 걸린 압력 — 화면이 「점점 세진다」를 말한다. </summary>
		public float Pressure => core != null ? core.Pressure : 1f;

		/// <summary> 코어 경험치 — 레벨이 오르면 판 전체에 걸리는 선택지가 쌓인다. </summary>
		private void AwardCoreExperience(int amount)
		{
			int before = coreProgress.Level;
			coreProgress.AddExperience(amount);
			if (coreProgress.Level > before && coreCombatant != null)
				PopWorldText("코어 Lv." + coreProgress.Level, coreCombatant.Position, TextType.Exp);
		}

		/// <summary> 코어 레벨 / 이번 구간 진행 / 아직 안 고른 선택지 수 — 화면이 읽는다. </summary>
		public int CoreLevel => coreProgress.Level;
		public float CoreLevelRatio => coreProgress.LevelRatio;
		public int CorePendingChoices => coreProgress.PendingChoices;

		/// <summary>
		/// 코어가 지금 내놓는 카드들 — 레벨이 씨앗이라 같은 레벨이면 언제 열어도 같은 세 장이다.
		/// 판을 멈추지 않는다(실시간) — 고를 때까지 카드가 코어에 붙어 기다린다.
		/// </summary>
		public void OfferCoreCards(List<TowerDefenseBoon> result)
		{
			result.Clear();
			if (stage == null || coreProgress.PendingChoices <= 0)
				return;

			TowerDefenseDraft.Offer(coreProgress.Level, MapSeed, stage.DraftRules, result);
		}

		/// <summary> 코어 카드 한 장 선택 — 고른 것은 판 전체에 걸린다. </summary>
		public bool ChooseCoreCard(int index)
		{
			List<TowerDefenseBoon> offers = new();
			OfferCoreCards(offers);
			if (index < 0 || index >= offers.Count)
				return false;
			if (coreProgress.Choose(TowerDefenseBuildingPerk.Damage) == false)
				return false; // 대기 하나 소비(어떤 것을 골랐는지는 아래 boons 가 기억한다).

			TowerDefenseBoon boon = offers[index];
			boons.Take(boon);

			switch (boon.Kind)
			{
				case TowerDefenseBoonKind.Life:
					core.AddLives(Mathf.RoundToInt(boon.Magnitude));
					break;
				case TowerDefenseBoonKind.Essence:
					core.AddEssence(Mathf.RoundToInt(boon.Magnitude));
					break;
				case TowerDefenseBoonKind.Windfall:
					core.AddResource(Mathf.RoundToInt(boon.Magnitude));
					break;
				case TowerDefenseBoonKind.PowerCapacity:
					bonusPowerCapacity += Mathf.RoundToInt(boon.Magnitude);
					break;
				case TowerDefenseBoonKind.MaxLives:
					core.AddLives(Mathf.RoundToInt(boon.Magnitude));
					break;
				case TowerDefenseBoonKind.CoreRepair:
					RepairCore(boon.Magnitude);
					break;
				default:
					break;
			}

			core.IncomeMultiplier = boons.IncomeMultiplier * (1f + ResearchBonus(TowerDefenseResearchEffect.HarvestYield));
			if (coreCombatant != null)
				PopWorldText("「" + boon.DisplayName + "」", coreCombatant.Position, TextType.Heal);
			Debug.Log($"{nameof(TowerDefenseMatch)}: 코어 선택 — {boon.DisplayName} ({boon.Note})");
			return true;
		}

		/// <summary> 고른 건물의 성장 정보(없으면 null) — 화면이 선택지를 그릴 때 쓴다. </summary>
		public TowerDefenseDollLabel FindDoll(MatchCombatant combatant)
		{
			return combatant != null ? FindDollLabel(combatant.transform) : null;
		}

		/// <summary> 고른 건물의 레벨업 선택지를 확정한다. </summary>
		public bool ChooseBuildingPerk(MatchCombatant combatant, TowerDefenseBuildingPerk perk)
		{
			TowerDefenseDollLabel doll = FindDoll(combatant);
			if (doll == null || doll.Progress.Choose(perk) == false)
				return false;

			ApplyPerk(doll, perk);
			PopWorldText(TowerDefenseBuildingProgress.NameOf(perk), doll.Anchor.position, TextType.Exp);
			return true;
		}

		// 고른 것을 실제 수치에 건다 — 화면만 바뀌고 실물이 그대로면 그건 선택이 아니다.
		private void ApplyPerk(TowerDefenseDollLabel doll, TowerDefenseBuildingPerk perk)
		{
			TowerDefenseWeapon weapon = doll.Anchor.GetComponent<TowerDefenseWeapon>();
			if (weapon != null)
			{
				weapon.ApplyPerk(perk, stage.PerkStep);
				// 사거리를 올렸으면 원도 그 자리에서 자란다 — 다음 승급까지 기다리면 그동안 원이 거짓말한다.
				RefreshTowerRing(doll.Anchor.gameObject);
			}

			if (perk == TowerDefenseBuildingPerk.Endure)
			{
				UnitObject unit = doll.Anchor.GetComponent<UnitObject>();
				if (unit != null)
				{
					int bonus = Mathf.Max(1, Mathf.RoundToInt(unit.UnitStat[UnitStatType.HP_MAX] * stage.PerkStep));
					unit.UnitStat[UnitStatType.HP_MAX] += bonus;
					unit.UnitStat[UnitStatType.HP_CUR] += bonus;
				}
			}
		}

		// 카드로 늘린 전기 용량 — 코어가 대주는 양에 더해진다.
		private int bonusPowerCapacity;

		/// <summary> 코어를 최대 체력의 비율만큼 즉시 회복(카드). </summary>
		private void RepairCore(float ratio)
		{
			if (coreCombatant == null || coreCombatant.UnitObject == null)
				return;

			UnitHealth health = coreCombatant.UnitObject.GetComponent<UnitHealth>();
			if (health == null)
				return;

			int amount = Mathf.Max(1, Mathf.RoundToInt(coreCombatant.UnitObject.UnitStat[UnitStatType.HP_MAX] * ratio));
			health.ReceiveHeal(amount);
			PopWorldText("+" + amount, coreCombatant.Position, TextType.Heal);
		}

		/// <summary> 이 건물이 전기를 받고 있나 — 채집 수입이 이 값을 본다. </summary>
		private bool IsPowered(Transform building)
		{
			if (stage == null || stage.CorePowerCapacity <= 0)
				return true;

			return powerGrid.IsPowered(building);
		}

		/// <summary>
		/// 내 것이 판 끝에 다가오면 판을 넓힌다 — *무한 맵의 실체*.
		///
		/// ★ 왜 「넓히기」만 하고 「옮기기」는 안 하나: 창의 원점을 옮기면 이미 저장된 좌표(점유 칸·벽·
		///   전초기지·채집)가 전부 밀린다. 한 곳이라도 안 옮기면 조용히 어긋나는데, 그 병은 이 작업에서
		///   이미 두 번 겪었다(좌표 키 drift / 반경 무음 잠김). 넓히기만 하면 **기존 좌표가 그대로 유효**하다.
		/// ★ 지형은 다시 안 만든다 — 좌표에서 파생되므로 넓힌 자리의 지형은 원래부터 거기 있던 것과 같다.
		///   그래서 넓혀도 이미 본 자리가 변하지 않는다(그게 「경계 없는 지형」을 먼저 만든 이유다).
		/// ★ 다시 세우는 것은 창에 묶인 것들뿐: 격자(암반 목록) · 길찾기 · 안개 · 지면 · 바위.
		/// </summary>
		private void TryGrowWindow()
		{
			if (stage == null || stage.WindowGrowMargin <= 0 || mapLayout == null || windowGrowing)
				return;
			if (CellsToWindowEdge > stage.WindowGrowMargin)
				return;

			windowGrowing = true;
			StartCoroutine(GrowWindowRoutine());
		}

		private bool windowGrowing;

		private IEnumerator GrowWindowRoutine()
		{
			// ★ 확장은 판 전체를 다시 세우는 일이라 *반드시 잰다* — 여기서 프레임이 튀면 무한 맵은
			//   「넓어질 때마다 게임이 멈추는」 것이 된다. 재두면 나중에 무거워져도 바로 안다.
			float growStartedAt = Time.realtimeSinceStartup;
			int newWidth = mapLayout.Width + stage.WindowGrowStep;
			int newLength = mapLayout.Length + stage.WindowGrowStep;
			Debug.Log($"{nameof(TowerDefenseMatch)}: 판이 자란다 — {mapLayout.Width} → {newWidth}칸 (내 것이 끝에서 {CellsToWindowEdge}칸).");

			// ★ 원점을 유지한 채 +방향으로만 넓힌다 — 기존 좌표가 그대로 살아야 한다.
			// ★ 판 전체를 다시 만들지 않는다(실측 981ms) — 지형은 좌표에서 나오므로 *새 띠만* 묻는다.
			TowerDefenseMapParameters parameters = stage.MapParameters.Normalized();
			int siteSpacing = Mathf.Max(2, Mathf.RoundToInt(
				Mathf.Sqrt(mapLayout.Width * (float)mapLayout.Length / Mathf.Max(1, parameters.RockSiteCount))));
			TowerDefenseInfiniteTerrain terrain = new(
				mapLayout.Seed, mapLayout.CoreCell, siteSpacing,
				parameters.RidgeWidth, parameters.ObstacleDensity, parameters.CoreClearRadius);

			TowerDefenseVision olderVision = vision;
			mapLayout = TowerDefenseMapLayout.Grown(mapLayout, newWidth, newLength, terrain.IsBlocked);

			activeGroundWidth = mapLayout.GroundWidth;
			activeGroundLength = mapLayout.GroundLength;

			yield return null;
			if (core == null)
				yield break;

			// 창에 묶인 것들만 다시 세운다 — 지형 자체는 좌표에서 나오므로 이미 본 자리는 안 변한다.
			vision = new TowerDefenseVision(mapLayout.Width, mapLayout.Length);
			vision.CopyExploredFrom(olderVision); // 가봤던 곳이 통째로 어두워지지 않게.
			if (fogView != null)
			{
				Destroy(fogView.gameObject);
				fogView = null;
			}
			fogView = TowerDefenseFogView.Create(
				stageRoot, mapLayout.Width, mapLayout.Length, activeGroundWidth, activeGroundLength, stage.FogHeight);

			RebuildPathing();
			RefreshVision();
			windowGrowing = false;
			float grewInMs = (Time.realtimeSinceStartup - growStartedAt) * 1000f;
			Debug.Log($"{nameof(TowerDefenseMatch)}: 판 확장 끝 — 이제 {mapLayout.Width}칸 "
				+ $"(걸린 시간 {grewInMs:F0}ms, 암반 {mapLayout.ObstacleCells.Count}칸).");
		}

		/// <summary>
		/// 지금 열려 있는 창 안인가 — 창 밖은 「암반」이 아니라 「아직 안 열린 곳」이다(무한 맵 1단계).
		/// 고정 판(생성 안 씀)에서는 경계가 없으므로 언제나 참.
		/// </summary>
		public bool IsInsideWindow(Vector3 worldPosition)
		{
			if (mapLayout == null || stageRoot == null)
				return true;
			return mapLayout.IsInsideWindow(stageRoot.InverseTransformPoint(worldPosition));
		}

		/// <summary> 내 것 중 가장 바깥이 창 가장자리에서 몇 칸 남았나 — 창을 넓힐 시점을 정하는 값. </summary>
		public int CellsToWindowEdge
		{
			get
			{
				if (mapLayout == null || stageRoot == null)
					return int.MaxValue;

				int nearest = int.MaxValue;
				foreach (Transform building in supplyChain.Buildings)
				{
					if (building == null)
						continue;
					int distance = mapLayout.CellsToWindowEdge(stageRoot.InverseTransformPoint(building.position));
					if (distance < nearest)
						nearest = distance;
				}
				return nearest;
			}
		}

		/// <summary>
		/// 실제로 쓰는 보급 거리 — 설정값과 *판 크기에서 파생한 값* 중 큰 쪽.
		///
		/// ★ 왜 파생시키나 (같은 병을 두 번 앓았다): 절대값으로 박아두면 판을 키울 때마다 상대적으로
		///   짧아져 **기능이 통째로 무음 잠김**된다. 44칸 시절 7 → 바깥 노드가 어떤 사슬로도 안 닿아
		///   정수가 영영 0. 12로 고쳤더니 판을 200칸으로 키우며 같은 일이 재발해 채집이 0기가 됐다.
		///   판 크기가 반경을 모르는 것이 진짜 근본이라, 판이 커지면 반경도 저절로 따라오게 묶는다.
		/// </summary>
		public float EffectiveSupplyReach
		{
			get
			{
				if (stage == null)
					return 0f;

				float derived = Mathf.Min(activeGroundWidth, activeGroundLength) * stage.SupplyReachRatio;
				return Mathf.Max(stage.SupplyReach, derived) * boons.SupplyReachMultiplier
					* (1f + ResearchBonus(TowerDefenseResearchEffect.SupplyReach));
			}
		}

		/// <summary>
		/// 거기에 지을 수 있는가 — **보급이 닿는 곳에만** 지을 수 있다.
		///
		/// ★ 왜 필요한가 (사용자 지시: "설치할 수 있는 범위가 제한이 되어야 할 것 같은데. 지금 그냥 맨 땅에
		///   설치할 수 있으니까 문제"): 아무 데나 지을 수 있으면 개척이라는 말이 성립하지 않는다. 마수가
		///   나오는 자리 옆에 바로 포탑을 박으면 길목도, 넓히는 결정도, 보급선도 전부 의미를 잃는다.
		/// ★ 왜 *보급* 기준인가: 이미 있는 규칙을 그대로 쓴다. 코어·전초기지·이어진 내 건물에서 뻗어 나가는
		///   것이 곧 「내 땅」이고, 화면에 그려둔 보급 사거리 원이 그 경계를 이미 보여주고 있다.
		///   새 숫자를 만들면 화면의 원과 실제 규칙이 갈라진다.
		/// </summary>
		public bool IsInBuildableRange(Vector3 worldPosition)
		{
			if (stage == null || coreCombatant == null)
				return true;

			return supplyChain.IsWithinReach(worldPosition, coreCombatant.Position, outposts, EffectiveSupplyReach);
		}

		/// <summary>
		/// 마우스가 얹힌 건물의 사거리만 켠다(나머지는 끈다). 상시 표시가 정보가 아니라 노이즈가 되는 것을
		/// 막는 유일한 장치 — 「지금 이것」 하나만 보여준다.
		/// </summary>
		public void HighlightRangeOf(Transform unit)
		{
			if (showAllRanges)
				return; // 디버그 토글이 켜져 있으면 전부 보여주는 중 — 손대지 않는다.

			TowerDefenseRing wanted = null;
			if (unit != null)
				wanted = unit.GetComponentInChildren<TowerDefenseRing>(true);

			if (wanted == highlightedRing)
				return;

			if (highlightedRing != null)
				highlightedRing.SetVisible(false);

			highlightedRing = wanted;
			if (highlightedRing != null)
				highlightedRing.SetVisible(true);
		}

		/// <summary> 디버그 — 세워둔 것 전부의 사거리를 한 번에 보여준다/감춘다. </summary>
		public void ToggleAllRanges()
		{
			showAllRanges = showAllRanges == false;

			for (int index = rangeRings.Count - 1; index >= 0; index--)
			{
				if (rangeRings[index] == null)
				{
					rangeRings.RemoveAt(index);
					continue;
				}
				rangeRings[index].SetVisible(showAllRanges);
			}

			highlightedRing = null;
			Debug.Log($"{nameof(TowerDefenseMatch)}: 전체 사거리 표시 {(showAllRanges ? "켜짐" : "꺼짐")}");
		}

		/// <summary> 전체 사거리 표시 중인가 — 화면 버튼이 상태를 보여준다. </summary>
		public bool ShowAllRanges => showAllRanges;

		/// <summary> 그 자리 인형의 이름표(없으면 null) — 승급 단계 표시 갱신에 쓴다. </summary>
		private TowerDefenseDollLabel FindDollLabel(Transform anchor)
		{
			foreach (TowerDefenseDollLabel label in dollLabels)
			{
				if (label.Anchor == anchor)
					return label;
			}
			return null;
		}

		/// <summary> 이번 판의 씨앗 — 같은 값이면 같은 판이 나온다(재현·신고용). 고정 판이면 0. </summary>
		/// <summary> 이번 판의 배치도 — 지도가 지형을 그리려면 이게 있어야 한다(읽기 전용). </summary>
		public TowerDefenseMapLayout MapLayout => mapLayout;

		public int MapSeed => mapLayout != null ? mapLayout.Seed : 0;

		private int? nextMatchSeed;

		/// <summary>
		/// 다음 판에 쓸 씨앗을 지정한다 — 남이 준 씨앗으로 *같은 땅*을 여는 유일한 문.
		/// 다음 판 하나에만 걸린다(계속 걸리면 그건 공유가 아니라 고정이다).
		/// </summary>
		public void SetNextMatchSeed(int seed)
		{
			nextMatchSeed = seed;
		}

		/// <summary> 이번 판의 암반 칸 수 — 0 이면 지형 없는 빈 판. </summary>
		public int ObstacleCount => mapLayout != null ? mapLayout.ObstacleCells.Count : 0;

		// ── 시간 조작 ────────────────────────────────────────────────────────────
		// ★ 왜 필요한가: 판이 커지고(44칸) 화면이 말하는 정보가 늘었는데(예고·사거리·시야·길) 정작
		//   *볼 시간*이 없으면 그 정보는 없는 것과 같다. 멈추고 보는 것은 편의가 아니라 전술의 일부다.
		private static readonly float[] SpeedSteps = { 0f, 1f, 2f, 3f };
		private int speedStep = 1;

		/// <summary> 지금 시간 배속(0 = 멈춤). </summary>
		public float SpeedScale => SpeedSteps[Mathf.Clamp(speedStep, 0, SpeedSteps.Length - 1)];

		/// <summary> 지금 멈춰 있나 — 메뉴가 「내가 멈춘 것인지」 가려낼 때 쓴다(사용자가 직접 멈춘 판을 풀면 안 된다). </summary>
		public bool IsPaused => speedStep == 0;

		/// <summary> 멈춤 ↔ 직전 배속 토글. 멈춘 채로 배치·관찰할 수 있어야 정보가 쓸모를 갖는다. </summary>
		public void TogglePause()
		{
			speedStep = speedStep == 0 ? lastRunningStep : 0;
			ApplySpeed();
		}

		/// <summary> 배속 한 단계 올림(끝에서 처음으로 순환). 멈춤 상태는 건너뛴다. </summary>
		public void CycleSpeed()
		{
			speedStep = speedStep >= SpeedSteps.Length - 1 ? 1 : speedStep + 1;
			lastRunningStep = speedStep;
			ApplySpeed();
		}

		private int lastRunningStep = 1;

		private void ApplySpeed()
		{
			// 개척 안에서는 이 모드가 곧 게임 전부라 전역 시간을 그대로 쓴다 —
			// 매치 전용 시계를 따로 두면 물리·이펙트가 따로 놀아 화면이 갈라진다.
			Time.timeScale = SpeedScale;
		}

		/// <summary> 모드를 나가거나 매치가 끝나면 반드시 원래 속도로 — 안 되돌리면 본편이 멈춘 채 남는다. </summary>
		public void RestoreTimeScale()
		{
			Time.timeScale = 1f;
		}

		/// <summary> 세운 연구 인형 수 — 늘어날수록 모든 포탑이 강해진다. </summary>
		public int LabCount { get; private set; }

		/// <summary>
		/// 지금의 포탑 피해 배수. 포탑이 매 발사 때 *읽어가므로* 나중에 세운 연구 인형이
		/// 이미 서 있던 포탑에도 즉시 반영된다(세운 뒤에야 효과가 오면 강화가 아니라 벌칙이다).
		/// </summary>
		// 연구(판 안 건물)와 드래프트(웨이브 사이 선택)는 서로 다른 층이라 곱해진다 — 둘 다 쌓은 판이
		// 눈에 띄게 세지는 것이 「이 판은 화력으로 갔다」의 실체다.
		/// <summary> 연구로 늘어난 포탑 사거리 배수 — 무기가 사거리를 물을 때마다 읽는다. </summary>
		/// <summary>
		/// 이 대상을 때릴 때의 피해 배수 — 「둥지에 더 아프게」 카드가 여기서 걸린다.
		/// 카드는 뽑히는데 걸릴 자리가 없으면 화면엔 「둥지↑」라 적히고 실제로는 똑같이 때린다.
		/// </summary>
		private float DamageMultiplierFor(ICombatant target)
		{
			float multiplier = TowerDamageMultiplier;
			if (target is MatchCombatant combatant && IsNest(combatant))
				multiplier *= boons.NestDamageMultiplier;
			return multiplier;
		}

		public float TowerRangeMultiplier => 1f + ResearchBonus(TowerDefenseResearchEffect.TowerRange);

		public float TowerDamageMultiplier =>
			(1f + LabCount * (stage != null ? stage.LabDamageBonus : 0f)) * boons.DamageMultiplier
			* (1f + ResearchBonus(TowerDefenseResearchEffect.TowerDamage));

		// 연구 성좌에서 찍어 모은 것 — 효과 종류별 누적 비율. 화면(성좌)이 고르고, 값은 여기 쌓인다.
		private readonly Dictionary<TowerDefenseResearchEffect, float> researchBonus = new();

		// ★ 아래 셋을 듣는 것은 *성좌 화면이 아니라 판 진행자*다. 화면은 사람이 처음 열 때야 생기는데
		//   이어하기는 그보다 먼저 일어나므로, 화면이 들고 있으면 되돌릴 곳이 없어 저장에 적힌
		//   연구가 통째로 조용히 사라진다. 규칙은 화면 유무와 무관해야 한다.

		/// <summary> 새 판 — 찍은 마디도 처음으로 되돌리라는 신호. </summary>
		public event System.Action ResearchReset = delegate { };

		/// <summary> 저장할 때 「지금 찍혀 있는 마디들」을 받아 적는 통로. </summary>
		public event System.Action<List<int>> CollectResearch = delegate { };

		/// <summary> 이어할 때 「이 마디들을 다시 찍은 것으로 하라」는 신호. </summary>
		public event System.Action<List<int>> RestoreResearch = delegate { };

		// 셋을 부르는 자리는 저장·이어하기·새 판뿐이라 밖에서 부를 일이 없지만, 검사기가
		// 「화면 없이도 되돌아오나」를 재려면 저장 경로와 *똑같은 문*으로 들어와야 한다
		// (검사 전용 뒷문을 따로 내면 그 문만 멀쩡하고 진짜 경로는 썩어도 모른다).
		public void ClearResearch()
		{
			researchBonus.Clear();
			ResearchReset();
			if (core != null)
				core.IncomeMultiplier = boons.IncomeMultiplier;
			RefreshRangeRings();
			RefreshSupplyRings();
		}

		/// <summary>
		/// 지어놓은 것들의 사거리 원을 다시 그린다.
		/// ★ 반지름을 여기서 *다시 계산하지 않는다* — 실제로 쏘는 거리를 쥔 무기에게 묻는다.
		///   (원형 반지름을 따로 들고 곱하는 방식도 써봤지만, 그건 무기의 셈을 베낀 두 번째 정본이라
		///   승급·강화가 끼는 순간 또 갈라진다. 배수를 아는 곳은 한 곳이어야 한다.)
		/// </summary>
		private void RefreshRangeRings()
		{
			for (int index = rangeRings.Count - 1; index >= 0; index--)
			{
				if (rangeRings[index] == null)
				{
					rangeRings.RemoveAt(index);
					continue;
				}

				Transform owner = rangeRings[index].transform.parent;
				if (owner != null)
					RefreshTowerRing(owner.gameObject);
			}
		}

		/// <summary> 화면에 실제로 그려진 보급 원의 반지름 — 규칙이 아니라 *사람이 보는 것*을 잰다. </summary>
		public float DrawnSupplyReach
		{
			get
			{
				foreach (TowerDefenseRing ring in supplyRings)
				{
					if (ring != null)
						return ring.Radius;
				}

				return 0f;
			}
		}

		/// <summary> 보급 원점의 원을 지금 보급 거리로 다시 그린다 — 안 그리면 「어디까지 지어지나」가 거짓말한다. </summary>
		private void RefreshSupplyRings()
		{
			float reach = EffectiveSupplyReach;
			for (int index = supplyRings.Count - 1; index >= 0; index--)
			{
				if (supplyRings[index] == null)
				{
					supplyRings.RemoveAt(index);
					continue;
				}

				supplyRings[index].SetRadius(reach);
			}
		}

		public void CollectResearchInto(List<int> into) => CollectResearch(into);

		public void RestoreResearchFrom(List<int> ids) => RestoreResearch(ids);

		/// <summary> 그 종류로 지금까지 얼마나 세졌나(0.2 = +20%). </summary>
		public float ResearchBonus(TowerDefenseResearchEffect effect)
		{
			return researchBonus.TryGetValue(effect, out float amount) ? amount : 0f;
		}

		/// <summary>
		/// 성좌에서 마디 하나를 찍는다 — 값을 치르고 효과를 쌓는다.
		/// 값이 모자라면 아무 일도 안 일어난다(화면이 찍힌 척하면 안 되므로 false 를 돌려준다).
		/// </summary>
		/// <summary>
		/// 정수가 모자랄 때 하는 말 — **어디서 버는지까지** 한 곳에서 만든다.
		///
		/// ★ 사용자 실증: "정수 어떻게 얻어? 강화를 할 수가 없는데?" — 화면은 「부족」만 말하고
		///   *버는 법*을 어디서도 말하지 않았다. 값이 모자라다는 것은 이미 눈에 보이는 사실이고,
		///   사람이 막히는 지점은 「그럼 어떻게 벌지」다. 세 갈래를 그 자리에서 말한다.
		/// ★ 한 곳에서 만드는 이유: 정수를 쓰는 자리가 넷(승급·연구 인형·성좌·전초기지)인데
		///   따로 적으면 하나만 고쳐도 나머지가 옛말을 한다.
		/// </summary>
		/// <summary> 정수를 깎는다(검증 전용) — 「모자랄 때 뭐라고 하나」는 모자라게 만들어야 잴 수 있다. </summary>
		public void SpendEssenceForVerification(int amount)
		{
			if (core != null && amount > 0)
				core.TrySpendEssence(amount);
		}

		private string EssenceShortText(int cost)
		{
			return $"정수 부족 {core.Essence}/{cost} — 바깥 광맥 채집 · 둥지 부수기 · 서식지 소탕";
		}

		public bool TryTakeResearchNode(TowerDefenseResearchEffect effect, float amount, int cost, bool usesEssence)
		{
			if (core == null)
				return false;
			// ★ 「연구값 할인」 카드를 여기 태운다. 그 카드는 옛 연구(단추 한 번에 한 단계)의 값에만
			//   걸려 있었는데, 연구가 성좌로 옮겨오면서 **걸릴 곳이 없어져 아무 효과도 없는 카드**가 됐다.
			//   화면엔 「연구값↓」이라 적히는데 실제로는 한 푼도 안 깎이는 상태였다 — 카드가 거짓말한다.
			cost = Mathf.Max(0, Mathf.RoundToInt(cost * boons.ResearchCostMultiplier));

			// ★ 안쪽 고리는 일반 자원으로 산다 (사용자 실증: "연구 자원이 정수면 초반에 연구 어떻게
			//   하라는 겁니까"). 정수는 바깥으로 나가야 나는 것이라, 그걸 첫 마디의 통로로 두면
			//   판 시작에 연구가 통째로 잠긴다. 개척을 강요하는 자리는 바깥 고리다.
			if (cost > 0)
			{
				bool paid = usesEssence ? core.TrySpendEssence(cost) : core.TrySpend(cost);
				if (paid == false)
				{
					string lack = usesEssence ? EssenceShortText(cost) : $"자원 부족 {core.Resource}/{cost}";
					if (coreCombatant != null)
						Reject(lack, coreCombatant.Position);
					Debug.Log($"{nameof(TowerDefenseMatch)}: 연구 거절 — {lack}.");
					return false;
				}
			}

			researchBonus.TryGetValue(effect, out float current);
			researchBonus[effect] = current + amount;

			// ★ 채집 수입 배수는 *카드를 뽑을 때만* 다시 계산되고 있었다 — 연구로 올려도 다음 카드가
			//   나올 때까지 판은 그대로였다(라이브 검증에서 40 → 40 으로 잡힘).
			//   여기서 같이 갱신한다. 「물을 때마다 읽는」 다른 갈래와 달리 이건 한 번 써두는 값이라,
			//   바뀌는 자리마다 다시 써주지 않으면 조용히 옛 값으로 돈다.
			if (core != null)
				core.IncomeMultiplier = boons.IncomeMultiplier * (1f + ResearchBonus(TowerDefenseResearchEffect.HarvestYield));

			// 같은 병 — 원은 지을 때 한 번 그려진다. 다시 안 그리면 총만 멀리 나가고 보급만 멀리 닿는다.
			if (effect == TowerDefenseResearchEffect.TowerRange)
				RefreshRangeRings();
			if (effect == TowerDefenseResearchEffect.SupplyReach)
				RefreshSupplyRings();

			// ★ 코어 방어만 *찍는 순간* 몸에 새긴다. 다른 갈래는 「물을 때마다 읽는」 배수라 저절로
			//   반영되지만, 체력은 이미 정해진 값이라 아무도 다시 묻지 않는다 — 여기서 안 올리면
			//   찍어도 아무 일이 안 일어난다(코어 방어만 조용히 죽은 갈래가 된다).
			if (effect == TowerDefenseResearchEffect.CoreArmor && coreCombatant != null
				&& coreCombatant.UnitObject != null)
			{
				UnitStat stat = coreCombatant.UnitObject.UnitStat;
				int added = Mathf.Max(1, Mathf.RoundToInt(stat[UnitStatType.HP_MAX] * amount));
				stat[UnitStatType.HP_MAX] += added;
				stat[UnitStatType.HP_CUR] += added; // 늘린 만큼 실제로 채워준다 — 최대치만 늘면 체감이 0이다.
				PopWorldText("코어 +" + added, coreCombatant.Position, TextType.Heal);
			}
			Debug.Log($"{nameof(TowerDefenseMatch)}: 연구 {TowerDefenseResearchGraph.NameOf(effect)} "
				+ $"+{amount:P0} → 누적 {researchBonus[effect]:P0}");
			return true;
		}

		/// <summary>
		/// 지금까지 내가 쓴 수단의 누적 — 세워둔 포탑들이 각자 센 것을 모은다.
		/// 「무엇을 많이 썼나」가 곧 마수가 무엇에 익숙해지는가다.
		/// </summary>
		public TowerDefenseAdaptationState Adaptation
		{
			get
			{
				if (stage == null)
					return default;

				int slowUses = 0;
				int splashHits = 0;
				int pierceHits = 0;
				foreach (GameObject unit in spawnedUnits)
				{
					if (unit == null)
						continue;
					TowerDefenseWeapon weapon = unit.GetComponent<TowerDefenseWeapon>();
					if (weapon == null)
						continue;
					slowUses += weapon.SlowApplied;
					splashHits += weapon.SplashHits;
					pierceHits += weapon.PierceHits;
				}

				return TowerDefenseAdaptation.From(slowUses, splashHits, pierceHits, stage.AdaptationSensitivity);
			}
		}

		/// <summary> waveIndex 파의 성격 — 예고와 스폰이 같은 함수를 본다. </summary>
		public TowerDefenseWaveEventKind WaveEventAt(int waveIndex)
		{
			return stage != null
				? TowerDefenseWaveEvent.For(waveIndex, stage.WaveEventEvery)
				: TowerDefenseWaveEventKind.None;
		}

		/// <summary> 성격까지 반영한 그 웨이브의 마수 수(떼거리는 배로, 정예는 절반). </summary>
		public int ScaledEnemyCount(int waveIndex)
		{
			if (stage == null)
				return 0;

			float scaled = stage.Rules.EnemiesInWave(waveIndex)
				* TowerDefenseWaveEvent.CountScale(WaveEventAt(waveIndex));
			return Mathf.Max(1, Mathf.RoundToInt(scaled));
		}

		/// <summary> 지금 웨이브의 시야 배수 — 어스름이면 좁아진다. </summary>
		private float CurrentVisionScale()
		{
			return TowerDefenseWaveEvent.VisionScale(WaveEventAt(core != null ? core.WaveIndex : 0));
		}

		// 웨이브 성격을 마수 스탯에 얹는다 — 종류(archetype) 배수 *위에* 곱해지므로 둘이 겹쳐 쌓인다.
		private static void ApplyWaveEventStats(UnitObject unitObject, TowerDefenseWaveEventKind kind)
		{
			if (unitObject == null || kind == TowerDefenseWaveEventKind.None)
				return;

			float healthScale = TowerDefenseWaveEvent.HealthScale(kind);
			if (Mathf.Approximately(healthScale, 1f) == false)
			{
				int scaledMax = Mathf.Max(1, Mathf.RoundToInt(unitObject.UnitStat[UnitStatType.HP_MAX] * healthScale));
				unitObject.UnitStat[UnitStatType.HP_MAX_STAT] = scaledMax;
				unitObject.UnitStat[UnitStatType.HP_MAX] = scaledMax;
				unitObject.UnitStat[UnitStatType.HP_CUR] = scaledMax;
			}

			float speedScale = TowerDefenseWaveEvent.SpeedScale(kind);
			if (Mathf.Approximately(speedScale, 1f) == false)
			{
				int scaledSpeed = Mathf.Max(1, Mathf.RoundToInt(unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] * speedScale));
				unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] = scaledSpeed;
			}
		}

		/// <summary> 등록된 포탑 종류 수(0 이면 기존 단일 포탑). </summary>
		public int TowerArchetypeCount => stage != null && stage.TowerArchetypes != null ? stage.TowerArchetypes.Length : 0;

		/// <summary> 그 종류가 몇 번 칸인가 — 저장이 「무엇을 세웠는지」를 적으려면 번호가 필요하다. </summary>
		private int TowerArchetypeIndexOf(TowerDefenseTowerArchetype archetype)
		{
			if (archetype == null || stage == null || stage.TowerArchetypes == null)
				return 0;

			for (int index = 0; index < stage.TowerArchetypes.Length; index++)
			{
				if (stage.TowerArchetypes[index] == archetype)
					return index;
			}
			return 0;
		}

		/// <summary> index 번 포탑 종류(범위 밖이면 null). </summary>
		public TowerDefenseTowerArchetype TowerArchetypeAt(int index)
		{
			if (index < 0 || index >= TowerArchetypeCount)
				return null;
			return stage.TowerArchetypes[index];
		}

		/// <summary> index 번 포탑의 건설 비용 — 종류가 없으면 스테이지 기본값. </summary>
		/// <summary>
		/// 그 종류를 *지금* 세우는 데 드는 값 — 화면과 규칙이 같은 창구에 묻는다.
		///
		/// ★ 왜 하나로 모으나: 핫바는 스테이지 원값을 보여주고 배치는 할인값을 뗐다.
		///   건설 할인 카드를 고른 순간 **화면은 40 이라 말하고 지갑에선 34 가 빠졌다** — 화면이 거짓말한다.
		/// ★ 게다가 할인이 경로마다 다르게 걸려 있었다(포탑·채집·발전만, 함정·벽은 안 걸림).
		///   카드에는 「건설 비용 할인」이라 적혀 있는데 절반한테만 걸리면 그건 규칙이 아니라 사고다.
		/// 정수로 사는 것(전초기지·연구)은 자원 할인과 다른 통장이라 여기서 갈라 답한다.
		/// </summary>
		public int CostOf(TowerDefensePlaceableKind kind, int towerIndex = 0)
		{
			if (stage == null)
				return 0;

			switch (kind)
			{
				case TowerDefensePlaceableKind.Tower:
					return Discounted(TowerCostAt(towerIndex));
				case TowerDefensePlaceableKind.Harvester:
					return Discounted(stage.HarvesterCost);
				case TowerDefensePlaceableKind.Wall:
					return Discounted(stage.WallCost);
				case TowerDefensePlaceableKind.Trap:
					return Discounted(stage.TrapCost);
				case TowerDefensePlaceableKind.Generator:
					return Discounted(stage.GeneratorCost);
				// 정수로 산다 — 자원 할인은 안 걸린다(다른 통장).
				case TowerDefensePlaceableKind.Outpost:
					return stage.OutpostEssenceCost;
				// 영웅은 짓는 게 아니라 보내는 것 — 값이 없다.
				default:
					return 0;
			}
		}

		/// <summary> 카드 할인이 걸린 실제 값 — 화면의 값과 실제 차감이 같은 곳을 읽는다. </summary>
		public int Discounted(int cost) => Mathf.Max(1, Mathf.RoundToInt(cost * boons.CostMultiplier));

		public int TowerCostAt(int index)
		{
			TowerDefenseTowerArchetype archetype = TowerArchetypeAt(index);
			return archetype != null ? archetype.Cost : stage.TowerCost;
		}

		/// <summary> 등록된 마수 종류 수(0 이면 기반 유닛 한 종류로 동작). </summary>
		public int EnemyArchetypeCount => stage != null && stage.EnemyArchetypes != null ? stage.EnemyArchetypes.Length : 0;

		/// <summary> index 번 마수 종류(범위 밖이면 null). HUD 범례·예고가 이름·색을 읽는다. </summary>
		public TowerDefenseEnemyArchetype EnemyArchetypeAt(int index)
		{
			if (index < 0 || index >= EnemyArchetypeCount)
				return null;
			return stage.EnemyArchetypes[index];
		}

		/// <summary>
		/// waveIndex 파의 구성을 계산해 result 에 담는다 — *예고*와 *실제 스폰*이 같은 함수를 쓰므로
		/// 화면이 거짓말할 수 없다(예고용 별도 계산을 두면 언젠가 반드시 어긋난다).
		/// </summary>
		public void ComposeWave(int waveIndex, List<int> result)
		{
			result.Clear();
			if (stage == null || core == null)
				return;

			int enemyCount = stage.Rules.EnemiesInWave(waveIndex);
			int archetypeCount = EnemyArchetypeCount;
			if (archetypeCount <= 0)
			{
				for (int index = 0; index < enemyCount; index++)
					result.Add(0);
				return;
			}

			int[] unlockWaves = new int[archetypeCount];
			int[] weights = new int[archetypeCount];
			for (int index = 0; index < archetypeCount; index++)
			{
				TowerDefenseEnemyArchetype archetype = stage.EnemyArchetypes[index];
				unlockWaves[index] = archetype != null ? archetype.UnlockWave : 0;
				weights[index] = archetype != null ? archetype.Weight : 0;
			}

			TowerDefenseWaveComposer.Compose(unlockWaves, weights, waveIndex, enemyCount, result);
		}

		/// <summary>
		/// 목표에 닿은 마수 처리 — 유출(leak). 그 마수는 *사라지고* 목숨이 하나 준다.
		///
		/// ★ 코어를 갉는 방식과 다른 점: 「아직 얼마 남았나」가 아니라 「한 마리라도 새면 아프다」가 된다.
		///   길목 하나가 뚫리는 순간의 무게가 여기서 정해진다. 새 놈이 코어에 눌어붙어 화면에서
		///   사라지던 옛 문제도 같이 없어진다(닿는 즉시 치우므로).
		/// </summary>
		/// <summary>
		/// 실제로 「샜다」로 치는 반경 — 설정값과 *마수가 멈춰 서는 거리* 중 큰 쪽.
		///
		/// ★ 왜 이게 필요한가 (사용자 실증: "몬스터가 멈춰서 안올때가 있음", 라이브 재현 170초):
		///   유출제에서 마수는 코어에 「닿으면」 사라진다. 그런데 마수는 코어를 *때리는 무기*를 갖고 있어서
		///   자기 사거리에 들어오는 순간 **거기서 멈춰 선다**. 그 사거리가 유출 반경보다 크면 마수는
		///   영원히 닿지 않고, 살아있는 마수가 0이 안 되니 **웨이브가 영영 안 끝난다**.
		///   「닿았다」의 기준을 마수가 실제로 멈추는 거리에서 뽑으면 두 숫자가 갈라질 수 없다.
		/// </summary>
		private float EffectiveLeakRadius
		{
			get
			{
				float stopDistance = 0f;
				if (stage != null && stage.EnemyTactic.Rules != null)
				{
					foreach (TacticRule rule in stage.EnemyTactic.Rules)
					{
						if (rule.Target.MaxRange > stopDistance)
							stopDistance = rule.Target.MaxRange;
					}
				}

				// 마수가 실제로 멈추는 자리는 둘 중 더 먼 쪽이다: 「사거리에 들어와서」 또는 「고리로 둘러싸서」.
				// 둘 다 덮지 않으면 바깥에 선 마수가 영영 안 닿아 웨이브가 끝나지 않는다(실측 2회).
				stopDistance = Mathf.Max(stopDistance, enemyMaxStopDistance);
				return Mathf.Max(stage.LeakRadius, stopDistance + stage.LeakRangeMargin);
			}
		}

		// 이번 매치 마수들이 목표에서 멈춰 서는 최대 거리 — 스폰 때 드라이버가 알려준 값.
		private float enemyMaxStopDistance;

		private void CullLeakedEnemies()
		{
			if (core == null || core.UsesLives == false || coreCombatant == null)
				return;

			float leakRadius = EffectiveLeakRadius;
			float leakRadiusSqr = leakRadius * leakRadius;

			for (int index = waveEnemies.Count - 1; index >= 0; index--)
			{
				MatchCombatant enemy = waveEnemies[index];
				if (enemy == null || enemy.IsAlive == false)
					continue;
				if (IsAtAnyGoal(enemy.Position, leakRadiusSqr) == false)
					continue;

				PopWorldText("-1", enemy.Position, TextType.Warning);
				LeakedCount++;
				core.RegisterLeak();

				targeting.Unregister(enemy);
				registeredCombatants.Remove(enemy);
				waveEnemies.RemoveAt(index);

				TacticDriver driver = enemy.GetComponent<TacticDriver>();
				if (driver != null)
					driver.StopDriving();

				ReleaseUnit(pool, enemy.gameObject);
				spawnedUnits.Remove(enemy.gameObject);
			}
		}

		/// <summary>
		/// 보급 다시 계산 + 수입 반영. 건물이 서거나 사라질 때마다, 그리고 매 틱 부른다.
		/// 끊긴 채집은 수입이 0 — 「넓히면 번다」가 「넓히면 지킬 것이 는다」로 바뀌는 지점.
		/// </summary>
		/// <summary>
		/// 확인 도구 전용 — 지금 당장 다시 세게 한다. **판이 매 틱 부르는 그 셈 그대로**라
		/// 검사 전용 셈이 따로 생기지 않는다(그러면 그쪽만 멀쩡하고 진짜 경로가 썩어도 모른다).
		/// </summary>
		public void RefreshSupplyForVerification() => RefreshSupply();

		private void RefreshSupply()
		{
			if (core == null || coreCombatant == null || stage == null)
				return;

			// 「누가 이어졌나」는 사슬이 답한다 — 여기 남는 것은 「그래서 얼마 버나」뿐이다.
			supplyChain.Compute(coreCombatant.Position, outposts, EffectiveSupplyReach);

			float resourceWeight = 0f;
			float essenceWeight = 0f;
			DisconnectedHarvesters = 0;
			WorkingHarvesters = 0;
			OuterHarvesters = 0;
			SuppliedOuterHarvesters = 0;
			PoweredOuterHarvesters = 0;

			IReadOnlyList<Transform> chain = supplyChain.Buildings;
			for (int index = 0; index < chain.Count; index++)
			{
				Transform building = chain[index];
				bool connected = supplyChain.IsConnected(index);
				bool outer = harvesterIsOuter.TryGetValue(building, out bool isOuter) && isOuter;

				if (outer)
				{
					OuterHarvesters++;
					if (connected)
					{
						SuppliedOuterHarvesters++;
						// 보급과 전기는 *다른 관문*이다 — 위 벌이 계산은 전기도 요구하는데 여기서 안 세면
						// 「이어졌는데 정수가 0」이라는 거짓 실패가 찍히고 진짜 이유(전기 없음)가 안 보인다.
						if (IsPowered(building))
							PoweredOuterHarvesters++;
					}
				}

				if (harvesterTransforms.Contains(building) == false)
					continue;

				// 끊긴 사실을 그 인형 머리 위에 붙인다 — 수입이 왜 안 오는지가 숫자가 아니라 *자리*로 보여야 한다.
				TowerDefenseDollLabel label = FindDollLabel(building);
				if (label != null)
					label.Disconnected = connected == false;

				if (connected == false)
				{
					DisconnectedHarvesters++;
					continue;
				}

				if (IsPowered(building) == false)
					continue; // 전기가 끊긴 채집은 캐지 못한다.

				WorkingHarvesters++; // 여기까지 온 것만 실제로 번다 — 화면이 이 수를 말해야 정직하다.

				float multiplier = HarvesterMultiplierOf(building);
				if (outer)
					essenceWeight += multiplier;
				else
					resourceWeight += multiplier;
			}

			core.SetHarvesterWeights(resourceWeight, essenceWeight);
		}

		/// <summary> 보급이 끊긴 채집 인형 수 — 화면이 「왜 수입이 줄었나」를 말해줘야 한다. </summary>
		public int DisconnectedHarvesters { get; private set; }

		/// <summary>
		/// *실제로 버는* 채집 인형 수 — 보급도 이어졌고 전기도 들어온 것만.
		/// ★ 화면이 「채집 N기」라며 지은 수를 말하면, 다섯 채 중 둘만 일해도 다섯이라고 한다.
		///   그러면 「왜 수입이 이것밖에 안 되지」가 영영 안 풀린다.
		/// </summary>
		public int WorkingHarvesters { get; private set; }

		/// <summary> 코어까지 이어진 건물 수 — 검증·진단용. </summary>
		public int SuppliedBuildings => supplyChain.ConnectedCount;

		/// <summary> 보급 사슬 후보 건물 수 — 「사슬이 비었나 / 안 닿나」를 가르는 진단값. </summary>
		public int SupplyBuildingCount => supplyChain.Buildings.Count;

		/// <summary>
		/// 바깥 노드에 선 채집 수 / 그중 보급이 이어진 수.
		/// ★ 정수가 0일 때 원인이 셋 중 어느 것인지 갈라준다: ① 바깥에 안 세웠다 ② 세웠는데 안 이어졌다
		///   ③ 둘 다 됐는데 안 들어온다(진짜 결함). 이 구분이 없으면 「바깥 노드인데 정수가 안 나온다」 같은
		///   *거짓 실패*가 계속 찍힌다(실측: 실제로는 바깥에 세운 적이 없었다).
		/// </summary>
		/// <summary>
		/// 이 판에 *바깥 등급* 광맥이 몇 개나 있나 — 정수가 날 수 있는 자리의 총수.
		/// ★ 이걸 안 보면 「바깥에 세운 게 없음」이 사람 탓인지 판 탓인지 갈리지 않는다(실측에서 갈렸다).
		/// </summary>
		/// <summary>
		/// 바깥 등급 광맥의 자리들 — 「멀다」와 「바깥 등급이다」는 다르다(거리로 고르면 매번 안쪽을 집는다).
		/// ★ **무대 기준 좌표**다(월드 아님). 월드 좌표와 섞어 재면 거리가 1900 같은 헛수가 나온다 — 실측.
		/// </summary>
		public void CollectOuterNodeLocalPositions(List<Vector3> into)
		{
			if (into == null)
				return;

			into.Clear();
			for (int index = 0; index < activeNodeIsOuter.Count && index < activeNodePositions.Count; index++)
			{
				if (activeNodeIsOuter[index])
					into.Add(activeNodePositions[index]);
			}
		}

		public int OuterNodeCount
		{
			get
			{
				int count = 0;
				foreach (bool isOuter in activeNodeIsOuter)
				{
					if (isOuter)
						count++;
				}

				return count;
			}
		}

		public int OuterHarvesters { get; private set; }
		public int SuppliedOuterHarvesters { get; private set; }

		/// <summary> 그중 전기까지 들어온 수 — 벌이는 보급*과* 전기를 둘 다 요구한다. </summary>
		public int PoweredOuterHarvesters { get; private set; }

		/// <summary>
		/// 그 채집 인형의 벌이 — **처리 범위 안의 광맥 자리 수**로 정해진다(사용자 지시: "자원 건물이
		/// 처리할 수 있는 타일 범위를 만들던지").
		///
		/// ★ 왜 「한 자리 = 한 기」가 아닌가: 자원이 광맥으로 뭉치면서 「어디에 세우나」가 판단이 됐다.
		///   덩어리 한가운데 세우면 여러 자리를 한꺼번에 물고, 가장자리에 세우면 조금만 문다.
		///   자리를 하나만 세는 옛 방식이면 광맥을 만든 의미가 사라진다.
		/// 벌이 배수는 *물고 있는 자리들의 배수 합* — 멀리 있는 큰 광맥일수록 크게 번다.
		/// </summary>
		private float HarvesterMultiplierOf(Transform harvester)
		{
			float reach = stage != null ? stage.HarvesterWorkRadius : 1f;
			float reachSqr = reach * reach;

			float total = 0f;
			for (int index = 0; index < activeNodePositions.Count; index++)
			{
				Vector3 nodeWorld = stageRoot.TransformPoint(activeNodePositions[index]);
				if ((nodeWorld - harvester.position).sqrMagnitude <= reachSqr)
					total += NodeIncomeMultiplierAt(index);
			}

			return (total > 0f ? total : 1f) * boons.HarvestYieldMultiplier;
		}

		// 유출 지점은 코어만이 아니다 — 전초기지도 지켜야 할 곳이다(넓힌 만큼 늘어난다).
		private bool IsAtAnyGoal(Vector3 position, float radiusSqr)
		{
			if ((position - coreCombatant.Position).sqrMagnitude <= radiusSqr)
				return true;

			foreach (Transform outpost in outposts)
			{
				if (outpost != null && (position - outpost.position).sqrMagnitude <= radiusSqr)
					return true;
			}
			return false;
		}

		/// <summary>
		/// 전초기지 세우기 — 정수로만. 세우는 순간 ① 마수가 향하는 목표가 하나 늘고
		/// ② 보급의 새 원점이 생기고 ③ 시야가 넓어진다. 「넓히면 벌지만 지킬 곳이 는다」가 한 건물에 들어있다.
		/// </summary>
		public bool TryPlaceOutpost(Vector3 worldPosition)
		{
			if (core == null || mapLayout == null || stageRoot == null)
				return false;

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey))
				return Reject("여긴 이미 찼다", worldPosition);
			if (ValidateSite(worldPosition) == false)
				return false;
			if (core.TrySpendEssence(stage.OutpostEssenceCost) == false)
				return Reject(EssenceShortText(stage.OutpostEssenceCost), worldPosition);

			occupiedCells.Add(cellKey);

			GameObject outpostObject = TowerDefenseVisuals.Primitive(PrimitiveType.Cube);
			outpostObject.name = "Outpost";
			outpostObject.transform.SetParent(stageRoot, false);
			outpostObject.transform.position = worldPosition + new Vector3(0f, 0.6f, 0f);
			outpostObject.transform.localScale = new Vector3(1.1f, 1.2f, 1.1f);

			Renderer outpostRenderer = outpostObject.GetComponent<Renderer>();
			if (outpostRenderer != null)
			{
				Material material = new Material(outpostRenderer.sharedMaterial);
				material.color = stage.OutpostTint;
				if (material.HasProperty("_BaseColor"))
					material.SetColor("_BaseColor", stage.OutpostTint);
				outpostRenderer.sharedMaterial = material;
			}

			// ★ 전초기지 자체 방어 — 무기는 *유닛 프리팹으로 세운 수비대*가 든다.
			//   앞서 도형(큐브)에 바로 무기를 달았더니 몸(UnitObject)이 없어 라이브에서 널 참조로 터졌다.
			//   전초기지 표식(큐브)은 길·보급의 앵커로 두고, 그 자리에 *지키는 인형*을 한 기 세운다 —
			//   기존 배치 경로를 그대로 재사용하므로 새로 만드는 것이 없고, 그 인형은 맞을 수도 있다
			//   (「넓힌 곳도 지켜야 한다」가 규칙으로 성립한다).
			if (stage.OutpostWeapon != null && stage.TowerUnit != null && stage.TowerUnit.Prefab != null)
			{
				StartCoroutine(SpawnDefensiveUnitRoutine(
					stage.TowerUnit, null, worldPosition, isHarvester: false, incomeMultiplier: 1f,
					towerArchetype: stage.OutpostWeapon));
			}

			outposts.Add(outpostObject.transform);
			supplyChain.Add(outpostObject.transform);
			ShowSupplyReachRing(outpostObject.transform); // 새 원점이므로 새 사거리 원.
			AddVisionSource(worldPosition, stage.OutpostVisionRadius);
			RebuildPathing(); // 목표가 늘었으므로 마수의 길이 통째로 바뀐다.
			RefreshSupply();
			return true;
		}

		/// <summary> 세운 전초기지 수 — 지킬 곳의 개수. </summary>
		public int OutpostCount => outposts.Count;

		/// <summary> 남은 목숨(유출제 아니면 0). </summary>
		public int Lives => core != null ? core.Lives : 0;

		/// <summary> 이 판이 유출제인가 — 화면이 목숨을 보여줄지 결정한다. </summary>
		public bool UsesLives => core != null && core.UsesLives;

		/// <summary>
		/// 시야 밖 마수는 안 그린다 — 포탑이 못 쏘는데 화면에는 보이면, 「왜 안 쏘지」가 버그로 읽힌다.
		/// 규칙(못 쏨)과 그림(안 보임)이 같은 사실을 말해야 한다.
		/// </summary>
		private void ApplyEnemyVisibility()
		{
			if (vision == null)
				return;

			foreach (MatchCombatant enemy in waveEnemies)
			{
				if (enemy == null || enemy.UnitObject == null)
					continue;

				bool seen = IsVisibleAt(enemy.Position);
				foreach (Renderer enemyRenderer in enemy.UnitObject.GetComponentsInChildren<Renderer>(true))
				{
					if (enemyRenderer.enabled != seen)
						enemyRenderer.enabled = seen;
				}
			}
		}

		// 굳은 마수 감시 — CombatantId → (마지막 자리, 그 자리에 머문 시간).
		private readonly Dictionary<int, (Vector3 Position, float Seconds)> enemyStillness = new();

		/// <summary>
		/// 굳은 마수를 풀어준다(사용자 실증: "몬스터가 멈춰서 안올때가 있음").
		///
		/// ★ 왜 이게 치명적인가: 웨이브 종료 조건이 「살아있는 마수 0」이라 **한 마리만 굳어도 판이 영영 안 끝난다**.
		///   무대 밖 이탈(CullEscapedEnemies)은 이미 막고 있지만, *무대 안에서 제자리에 붙는* 경우는 안 잡혔다.
		/// ★ 왜 굳는가: 스폰 분산이 마수를 암반 칸 위/뒤에 떨궈 흐름장이 「거기서는 갈 수 없다」고 답하면
		///   안내가 끊기고, 그 자리에서 직선으로 벽을 밀며 영원히 버틴다.
		/// ★ 그래서 두 겹으로 막는다: ① 스폰 자체를 갈 수 있는 칸으로 스냅(SnapToReachable) ② 그래도 굳으면
		///   가장 가까운 갈 수 있는 칸으로 옮겨준다. 옮긴 사실은 로그로 남긴다 — 조용히 순간이동시키면
		///   다음에 같은 원인이 생겨도 아무도 모른다.
		/// </summary>
		private void UnstickEnemies()
		{
			if (mapLayout == null || flowField == null || stageRoot == null)
				return;

			float threshold = stage.StuckRelocateSeconds;
			if (threshold <= 0f)
				return;

			float moveEpsilonSqr = stage.StuckMoveEpsilon * stage.StuckMoveEpsilon;

			foreach (MatchCombatant enemy in waveEnemies)
			{
				if (enemy == null || enemy.IsAlive == false)
					continue;
				if (IsNest(enemy))
					continue; // 둥지는 원래 안 움직인다 — 「굳었다」로 세면 매 틱 헛되이 옮기려 든다.

				Vector3 position = enemy.Position;
				if (enemyStillness.TryGetValue(enemy.CombatantId, out (Vector3 Position, float Seconds) tracked) == false)
				{
					enemyStillness[enemy.CombatantId] = (position, 0f);
					continue;
				}

				if ((position - tracked.Position).sqrMagnitude > moveEpsilonSqr)
				{
					enemyStillness[enemy.CombatantId] = (position, 0f);
					continue;
				}

				float stillSeconds = tracked.Seconds + TimeManager.TICK;
				if (stillSeconds < threshold)
				{
					enemyStillness[enemy.CombatantId] = (tracked.Position, stillSeconds);
					continue;
				}

				Vector2Int cell = mapLayout.WorldToCell(stageRoot.InverseTransformPoint(position));
				bool blocked = IsPathBlocked(cell);
				bool reachable = flowField.IsReachable(cell);

				// ★ 밀어주지 않는다 (사용자 지시: "밀어주는게 어딨어... 밀어주는거 제거하세요").
				//   예전엔 굳은 마수를 다음 칸으로 *순간이동*시켜 길을 뚫었다 — 그건 길찾기가 답을 못 준
				//   자리를 손으로 메운 것이고, 벽을 지나가는 것처럼 보이는 원인이기도 했다.
				//   이제 길찾기가 목표가 무엇이든 답하므로, 굳었다는 것은 *진짜 막혔다*는 뜻이다 —
				//   그 사실을 남기기만 하고 판은 마수가 앞을 부수도록 둔다.
				Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 마수가 {stillSeconds:F1}s 째 못 나아감 — cell={cell} "
					+ $"blocked={blocked} reachable={reachable} (길이 막혔으면 앞을 부순다)");

				enemyStillness[enemy.CombatantId] = (enemy.Position, 0f);
			}
		}

		/// <summary> 그 칸에서 가장 가까운 「갈 수 있는」 칸 — 나선으로 넓혀 찾는다(없으면 false). </summary>
		private bool TrySnapToReachable(Vector2Int cell, out Vector2Int result)
		{
			result = cell;
			if (flowField.IsReachable(cell))
				return false; // 이미 갈 수 있는 자리 — 굳은 원인이 길이 아니다(옮겨도 소용 없다).

			for (int radius = 1; radius <= stage.StuckSearchRadius; radius++)
			{
				for (int offsetX = -radius; offsetX <= radius; offsetX++)
				{
					for (int offsetY = -radius; offsetY <= radius; offsetY++)
					{
						if (Mathf.Abs(offsetX) != radius && Mathf.Abs(offsetY) != radius)
							continue; // 테두리만 — 안쪽은 이전 반경에서 이미 봤다.

						Vector2Int candidate = new(cell.x + offsetX, cell.y + offsetY);
						if (flowField.IsReachable(candidate) == false)
							continue;

						result = candidate;
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// 격파 보상 지급 — 마수가 죽은 것을 처음 본 틱에 1회. 「잡는 맛」이 이 경로 하나에 달려 있다:
		/// 웨이브 정산만 있으면 교전 20초 동안 화면에서 아무 일도 안 일어나고, 잘 맞췄는지도 알 수 없다.
		/// 이탈(무대 밖) 제거는 목록에서 먼저 빠지므로 보상 대상이 아니다 — 사고에 상을 주지 않는다.
		/// </summary>
		private void PayKillBounties()
		{
			if (core == null)
				return;

			foreach (MatchCombatant enemy in waveEnemies)
			{
				if (enemy == null || enemy.IsAlive)
					continue;
				if (bountyPaidEnemyIds.Add(enemy.CombatantId) == false)
					continue;

				int bounty = enemyBountyById.TryGetValue(enemy.CombatantId, out int recorded)
					? recorded
					: core.BountyPerKill;
				bounty = Mathf.RoundToInt(bounty * boons.BountyMultiplier); // 드래프트로 고른 「사냥의 값」.
				if (bounty <= 0)
					continue;

				KilledCount++;
				core.AddResource(bounty);
				PopWorldText("+" + bounty, enemy.Position, TextType.Exp);
				AwardKillExperience(enemy.Position);
				AwardCoreExperience(Mathf.RoundToInt(stage.KillExperience * boons.EnemyRewardMultiplier)); // 코어도 판이 잘 굴러가는 만큼 자란다.

				// 죽은 자리에 잔해 — 많이 죽인 곳이 저절로 늪이 되어 다음 무리가 느려진다.
				TowerDefenseDebris.Spawn(stageRoot, enemy.Position, waveEnemies,
					stage.DebrisSeconds, stage.DebrisSlowFactor, stage.GroundCellSize * 0.8f, stage.DebrisTint);
			}
		}

		/// <summary>
		/// 웨이브 정산 내역을 *번 자리에* 띄운다 — 코어에 기본 수입, 채집 인형 각자 머리 위에 자기 몫.
		/// 총액만 HUD 숫자로 올리면 「채집 인형이 무슨 역할인지」가 영원히 안 읽힌다(사용자 실증).
		/// </summary>
		private void ShowIncomeBreakdown()
		{
			if (core == null || stage == null)
				return;

			if (coreCombatant != null && stage.Rules.BaseWaveIncome > 0)
				PopWorldText("+" + stage.Rules.BaseWaveIncome, coreCombatant.Position, TextType.Heal);

			if (stage.Rules.IncomePerHarvester <= 0)
				return;

			for (int index = harvesterTransforms.Count - 1; index >= 0; index--)
			{
				Transform harvester = harvesterTransforms[index];
				if (harvester == null)
				{
					harvesterTransforms.RemoveAt(index);
					continue;
				}
				// ★ 그 인형이 *실제로 번 만큼*을 띄운다.
				//   예전엔 전부 같은 숫자(정액)를 띄웠다 — 그러면 두 가지가 동시에 거짓말이 된다:
				//   ① 먼 큰 광맥에 세운 인형이 훨씬 많이 버는데 화면은 옆 인형과 같은 수를 보여준다
				//      (「자리를 잘 잡았다」를 배울 유일한 피드백인데 그게 안 보인다)
				//   ② 보급이 끊겼거나 전기가 없어 *한 푼도 못 번* 인형 위에도 숫자가 떴다.
				TowerDefenseDollLabel harvesterLabel = FindDollLabel(harvester);
				if (harvesterLabel != null && (harvesterLabel.Disconnected || harvesterLabel.Unpowered))
					continue; // 멈춘 채집은 아무것도 안 벌었다 — 아무 숫자도 띄우지 않는다.

				int earned = Mathf.RoundToInt(
					stage.Rules.IncomePerHarvester * HarvesterMultiplierOf(harvester) * core.IncomeMultiplier);
				if (earned <= 0)
					continue;

				PopWorldText("+" + earned, harvester.position, TextType.Heal);

				// ★ 바깥 채집은 정수를 낸다 — 그게 「멀리 나간」 보상인데 들어와도 화면이 한 마디도 안 했다.
				//   보이지 않는 보상은 배울 수가 없다(왜 위험을 무릅쓰는지가 안 남는다).
				if (harvesterIsOuter.TryGetValue(harvester, out bool outerNode) == false || outerNode == false)
					continue;

				// 규칙이 쓰는 것과 같은 식으로 — 정수는 자원과 달리 정산 배수가 아니라 채집 가중치만 탄다.
				int essence = Mathf.RoundToInt(
					stage.Rules.EssencePerHarvester * HarvesterMultiplierOf(harvester));
				if (essence > 0)
					PopWorldText("정수 +" + essence, harvester.position, TextType.Exp);
			}
		}

		/// <summary>
		/// 마지막으로 배치가 거절된 이유 — 화면·로그가 같은 문장을 쓴다.
		///
		/// ★ 왜 필요한가 (사용자 실증: "전초지기랑 연구소 설치 안됨"): 거절이 전부 조용한 false 였다.
		///   정수가 없어서인지, 이미 뭐가 서 있어서인지, 암반 위라서인지 화면이 한 마디도 안 하면
		///   플레이어에게는 「그 칸이 고장났다」로 보인다. 못 짓는 것보다 *이유를 모르는 것*이 더 나쁘다.
		/// </summary>
		public string LastRejectReason { get; private set; } = string.Empty;

		// 거절 = 이유를 그 자리에 띄우고 false. 모든 거절 경로가 이 하나를 지난다(조용한 false 금지).
		private bool Reject(string reason, Vector3 worldPosition)
		{
			LastRejectReason = reason;
			PopWorldText(reason, worldPosition, TextType.Warning);
			// 로그에도 남긴다 — 화면 글자는 흘러가고, 「왜 안 지어졌나」는 나중에 되짚어야 할 때가 온다.
			Debug.Log($"{nameof(TowerDefenseMatch)}: 배치 거절 — {reason} @ {worldPosition}");
			return false;
		}

		/// <summary>
		/// 웨이브를 넘길 때마다 내 편(코어·인형·영웅)을 최대 체력의 일정 비율만큼 회복시킨다(사용자 요청).
		///
		/// ★ 왜 필요한가: 지금은 한 번 긁힌 인형이 판이 끝날 때까지 그 체력으로 산다. 그러면 「버텼다」의
		///   보상이 없고, 앞줄에 세운 인형은 필연적으로 죽으니 앞에 세우는 선택 자체가 손해가 된다.
		///   웨이브 사이 회복이 있으면 「이번엔 버틸 수 있나」가 매 웨이브의 계산이 된다.
		/// ★ 완전 회복이 아닌 이유: 그러면 피해가 아무 의미가 없어져 방어선의 소모전이 사라진다.
		/// </summary>
		private void HealDefenders()
		{
			if (stage == null || stage.DefenderHealPerWave <= 0f)
				return;

			foreach (ICombatant combatant in registeredCombatants)
			{
				if (combatant is not MatchCombatant defender || defender.IsAlive == false)
					continue;
				if (defender.TeamId != DEFENDER_TEAM || defender.UnitObject == null)
					continue;

				UnitHealth health = defender.UnitObject.GetComponent<UnitHealth>();
				if (health == null)
					continue;

				int maxHp = defender.UnitObject.UnitStat[UnitStatType.HP_MAX];
				int currentHp = defender.UnitObject.UnitStat[UnitStatType.HP_CUR];
				if (currentHp >= maxHp)
					continue;

				int healAmount = Mathf.Max(1, Mathf.RoundToInt(maxHp * stage.DefenderHealPerWave));
				health.ReceiveHeal(healAmount);
				PopWorldText("+" + Mathf.Min(healAmount, maxHp - currentHp), defender.Position, TextType.Heal);
			}
		}

		/// <summary> 월드 좌표 위 뜨는 글자 — UI 매니저가 아직 없으면(부팅 전/헤드리스) 조용히 넘어간다. </summary>
		private static void PopWorldText(string message, Vector3 worldPosition, TextType textType)
		{
			if (UIManager.TryGetExistingInstance(out UIManager uiManager) == false)
				return;

			uiManager.PopText(message, textType, worldPosition);
		}

		/// <summary>
		/// 죽었거나 풀에 반납된 마수만 목록에서 걷어낸다 — 살아있는 것은 남긴다.
		/// 실시간이라 앞 무리와 상시 마수가 겹쳐 존재하므로, 새 무리를 낼 때 목록을 비우면 안 된다.
		/// </summary>
		private void PruneDeadEnemies()
		{
			for (int index = waveEnemies.Count - 1; index >= 0; index--)
			{
				MatchCombatant enemy = waveEnemies[index];
				if (enemy == null || enemy.IsAlive == false)
					waveEnemies.RemoveAt(index);
			}
		}

		/// <summary> 살아있는 웨이브 적 수 — 죽었거나 풀 반환된(null) 엔트리는 조회 겸 정리(멱등). </summary>
		private int CountAliveEnemies()
		{
			int count = 0;
			for (int index = waveEnemies.Count - 1; index >= 0; index--)
			{
				MatchCombatant combatant = waveEnemies[index];
				if (combatant == null)
				{
					waveEnemies.RemoveAt(index);
					continue;
				}
				if (combatant.IsAlive)
					count++;
			}
			return count;
		}

		private void Conclude(TowerDefenseOutcome outcome)
		{
			ticking = false;

			if (TimeManager.TryGetExistingInstance(out TimeManager existingTimeManager))
				existingTimeManager.RemoveCallback(Tick);

			// 종료 = 브레인(core)이 actuation 정지 권한 행사 — 전 드라이버 정지(좀비 틱 방지). ArenaMatch 와 동형.
			foreach (TacticDriver driver in drivers)
			{
				if (driver != null)
					driver.StopDriving();
			}

			if (matchEndedFired == false)
			{
				matchEndedFired = true;
				MatchEnded(outcome);
			}
		}

		/// <summary>
		/// (배치 증분 진입점) 건설 페이즈에 타워 배치 — 자원 부족 시 즉시 false(배치 거절, 상태 무변경).
		/// 유닛데이터/프리팹 유효성은 TrySpend *전* 검증(스펙#E — 자원 뗀 뒤 스폰 실패로 자원만 날리는 것 방지).
		/// 스폰 자체는 트랩#4 준수 위해 코루틴으로 지연되지만 자원 차감은 이 호출에서 동기 확정.
		/// </summary>
		public bool TryPlaceTower(Vector3 worldPosition, int towerIndex = 0)
		{
			if (core == null || pool == null || timeManager == null || targeting == null)
				return false;
			if (stage.TowerUnit == null || stage.TowerUnit.Prefab == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: stage.TowerUnit/Prefab 미할당 — 배치 불가(자원 미차감).");
				return false;
			}

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey))
			{
				// ★ 같은 자리에 같은 종류를 다시 지으면 = 승급. 별도 선택 UI 없이 「한 번 더 짓는다」는
				//   손동작 그대로라 배울 게 없고, 세로 깊이(같은 포탑을 키운다)가 생긴다.
				return TryUpgradeTowerAt(cellKey, towerIndex);
			}

			if (ValidateSite(worldPosition) == false)
				return false;
			int towerCost = CostOf(TowerDefensePlaceableKind.Tower, towerIndex);
			if (core.TrySpend(towerCost) == false)
				return Reject($"자원 부족 {core.Resource}/{towerCost}", worldPosition);

			occupiedCells.Add(cellKey);
			// 짓는 소리 — 「멀리 조용히 크는 것」과 「둥지 옆에 세우는 것」이 달라야 개척이 결정이 된다.
			EmitNoise(worldPosition, stage.NoiseFromBuild);
			// 종류가 정의돼 있으면 개척 전용 무기로, 없으면 기존 전술 경로로(하위 호환).
			TowerDefenseTowerArchetype archetype = TowerArchetypeAt(towerIndex);
			StartCoroutine(SpawnDefensiveUnitRoutine(
				stage.TowerUnit,
				archetype != null ? null : stage.TowerTactic,
				worldPosition,
				isHarvester: false,
				incomeMultiplier: 1f,
				towerArchetype: archetype));
			return true;
		}

		/// <summary>
		/// (배치 증분 진입점) 건설 페이즈에 채집건물 배치 — 반드시 미점유 자원 노드 반경 내에만 성립
		/// (개척 리스크 = 설계 긴장: 코어 바로 옆에 쌓아 무위험 수입을 얻는 것 차단). 노드 없으면 자원 무변경 false.
		/// 성공 시 core.AddHarvester() 로 다음 정산부터 수입 증가 + 스폰 위치를 노드 좌표로 스냅.
		/// </summary>
		public bool TryPlaceHarvester(Vector3 worldPosition)
		{
			if (core == null || pool == null || timeManager == null || targeting == null)
				return false;
			if (stage.HarvesterUnit == null || stage.HarvesterUnit.Prefab == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: stage.HarvesterUnit/Prefab 미할당 — 배치 불가(자원 미차감).");
				return false;
			}
			if (TryFindPlaceableNode(worldPosition, out int nodeIndex, out Vector3 nodeWorldPosition) == false)
				return Reject("자원 노드 위에만 선다", worldPosition); // 자원 무변경(스펙#C).

			Vector3Int cellKey = ToCellKey(nodeWorldPosition);
			if (occupiedCells.Contains(cellKey))
				return Reject("이 노드는 이미 잡혔다", nodeWorldPosition);

			if (IsInBuildableRange(nodeWorldPosition) == false)
				return Reject("보급이 닿는 곳에만 지을 수 있다", nodeWorldPosition);
			int harvesterCost = CostOf(TowerDefensePlaceableKind.Harvester);
			if (core.TrySpend(harvesterCost) == false)
				return Reject($"자원 부족 {core.Resource}/{harvesterCost}", nodeWorldPosition);

			claimedNodes.Add(nodeIndex); // TrySpend 성공 후에만 점유 확정(스펙 지시 — 실패 시 점유 안 남김).
			occupiedCells.Add(cellKey);
			float incomeMultiplier = nodeIndex < activeNodeIncomeMultipliers.Count ? activeNodeIncomeMultipliers[nodeIndex] : 1f;
			bool outerNode = nodeIndex < activeNodeIsOuter.Count && activeNodeIsOuter[nodeIndex];
			// 등급은 인형이 실제로 생긴 뒤 *그 인형에* 붙인다(스폰이 코루틴이라 지금은 아직 없다).
			StartCoroutine(SpawnDefensiveUnitRoutine(stage.HarvesterUnit, null, nodeWorldPosition, isHarvester: true, incomeMultiplier,
				towerArchetype: null, isOuterNode: outerNode));
			return true;
		}

		/// <summary> 마지막 판매의 판 값 / 실제 돌려준 액수(진단용). </summary>
		public int LastSoldValue { get; private set; }
		public int LastSellRefund { get; private set; }

		/// <summary>
		/// 그 칸에 세운 것을 판다(환불). 「실수가 되돌려지는가」 — 이게 없으면 배치가 실험이 아니라 도박이다.
		/// 코어는 못 판다(그건 자해다). 판 자리는 다시 비워져 새로 지을 수 있다.
		///
		/// ★ 파는 것은 *내가 세운 것*뿐이다 — 그 칸에 서 있는 아무 유닛이 아니라.
		///   그 칸을 지나가던 마수도 같은 칸에 서 있을 수 있는데, 예전엔 목록에서 먼저 잡히는 쪽을 팔아
		///   **마수를 공짜로 지워 없애고**(값 0) 정작 건물은 그대로 둔 채 자리만 비워졌다
		///   (실측: soldValue=0 · cellFreed=True · 건물 생존). 세운 것은 전부 보급 사슬 목록에 들어가므로
		///   그 목록이 「내 것인가」의 기준이 된다.
		/// </summary>
		public bool TrySell(Vector3 worldPosition, float refundRatio)
		{
			if (core == null || pool == null)
				return false;
			if (IsMatchOver)
				return false; // 끝난 판에선 팔 수도 없다 — 짓기와 같은 이유(끝은 끝이다).

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey) == false)
				return false;

			GameObject sold = null;
			foreach (GameObject unit in spawnedUnits)
			{
				if (unit == null || unit.activeInHierarchy == false)
					continue;
				if (ToCellKey(unit.transform.position) != cellKey)
					continue;
				if (coreCombatant != null && unit == coreCombatant.gameObject)
					return false; // 코어는 못 판다.
				if (supplyChain.Contains(unit.transform) == false)
					continue; // 내가 세운 것이 아니다(지나가던 마수·영웅 등) — 팔 수 없다.
				sold = unit;
				break;
			}

			if (sold == null)
				return false;

			// 판 인형은 「잃음」이 아니라고 표시해둔다 — 다음 정리에서 그 둘을 갈라 센다.
			TowerDefenseDollLabel soldDoll = FindDollLabel(sold.transform);
			if (soldDoll != null)
				soldDolls.Add(soldDoll);

			int soldValue = SoldValue(sold);
			int refund = Mathf.Max(0, Mathf.RoundToInt(soldValue * refundRatio));
			// 환불이 0 으로 나올 때 「값이 0 이라」인지 「비율이 0 이라」인지 갈라 말할 수 있게 남긴다 —
			// 이게 없으면 확인 도구가 「판매가 안 된다」까지만 말하고 이유를 못 댄다.
			LastSoldValue = soldValue;
			LastSellRefund = refund;
			core.AddResource(refund);
			PopWorldText("+" + refund, sold.transform.position, TextType.Heal);

			ReleaseSoldUnit(sold);
			occupiedCells.Remove(cellKey);
			return true;
		}

		// 판 값 = 그 자리에 무엇이 서 있었나. 채집이면 노드 점유도 함께 푼다(다시 잡을 수 있어야 한다).
		private int SoldValue(GameObject sold)
		{
			for (int index = harvesterTransforms.Count - 1; index >= 0; index--)
			{
				if (harvesterTransforms[index] == null || harvesterTransforms[index] != sold.transform)
					continue;

				harvesterTransforms.RemoveAt(index);
				ReleaseNodeAt(sold.transform.position);
				return stage.HarvesterCost;
			}

			TowerDefenseWeapon weapon = sold.GetComponent<TowerDefenseWeapon>();
			if (weapon != null)
				return weapon.Cost;

			// ★ 발전 인형 — 무기도 없고 채집도 아니다. 이걸 안 갈라내면 아래 「연구」로 흘러들어가
			//   *발전기를 팔았는데 연구 단계가 깎이는*(= 모든 포탑이 약해지는) 무음 손해가 난다.
			//   전기가 끊기는 것은 팔았으니 당연하지만, 연구가 깎이는 건 아무도 시키지 않은 일이다.
			if (powerGrid.RemoveGenerator(sold.transform))
			{
				RefreshPower(); // 공급원이 사라졌으니 누가 멈추는지 즉시 다시 계산한다.
				return stage.GeneratorCost;
			}

			// ★ 여기까지 왔다 = 채집도 포탑도 발전기도 아닌 것이 내 보급 사슬에 들어 있다.
			//   예전엔 이 자리에서 「연구 인형이겠지」 하고 연구 단계를 깎았는데, 연구소가 사라진 지금
			//   그 짐작은 *아무도 시키지 않은 손해*만 남긴다. 값을 0 으로 돌리고 소리 내어 알린다 —
			//   조용히 넘어가면 다음 사람이 또 같은 짐작을 한다.
			Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 정체를 모르는 건물을 팔았다({sold.name}) — 환불 0. 새 건물 종류를 넣고 판매 값을 안 정한 것이다.");
			return 0;
		}

		// 판 채집 인형이 잡고 있던 노드를 놓아준다(못 놓으면 그 노드는 영영 못 쓴다).
		private int ReleaseNodeAt(Vector3 worldPosition)
		{
			for (int index = 0; index < activeNodePositions.Count; index++)
			{
				if (claimedNodes.Contains(index) == false)
					continue;
				Vector3 nodeWorld = stageRoot.TransformPoint(activeNodePositions[index]);
				if ((nodeWorld - worldPosition).sqrMagnitude > 1f)
					continue;

				claimedNodes.Remove(index);
				return index;
			}
			return -1;
		}

		private void ReleaseSoldUnit(GameObject sold)
		{
			MatchCombatant combatant = sold.GetComponent<MatchCombatant>();
			if (combatant != null && targeting != null)
			{
				targeting.Unregister(combatant);
				registeredCombatants.Remove(combatant);
			}

			TacticDriver driver = sold.GetComponent<TacticDriver>();
			if (driver != null)
				driver.StopDriving();

			supplyChain.Remove(sold.transform);
			// 판 것은 더 이상 전기를 먹지 않는다 — 안 지우면 없는 건물이 계속 전력을 물고 있어
			// 「분명 발전기를 지었는데 왜 모자라지」가 된다(무음 누수).
			powerGrid.RemoveConsumer(sold.transform);
			harvesterIsOuter.Remove(sold.transform);
			ReleaseUnit(pool, sold);
			spawnedUnits.Remove(sold);
			RefreshSupply(); // 사슬 중간이 사라지면 그 너머가 통째로 끊긴다.
			RefreshPower();  // 먹는 입이 줄었으니 누가 다시 돌아가는지도 즉시 반영.
		}

		/// <summary>
		/// worldPosition 반경 NodeCaptureRadius 내 가장 가까운 *미점유* 자원 노드를 찾는다.
		/// 배치 UI 가 유효/무효 프리뷰를 보여줄 때도 이 메서드로 규칙 중복 없이 재사용(TryPlaceHarvester 와 동일 판정).
		/// </summary>
		public bool TryFindPlaceableNode(Vector3 worldPosition, out int nodeIndex, out Vector3 nodeWorldPosition)
		{
			nodeIndex = -1;
			nodeWorldPosition = Vector3.zero;

			if (stage == null || stageRoot == null)
				return false;

			float captureRadiusSqr = stage.NodeCaptureRadius * stage.NodeCaptureRadius;
			int bestIndex = -1;
			float bestSqrDistance = float.MaxValue;

			for (int index = 0; index < activeNodePositions.Count; index++)
			{
				if (claimedNodes.Contains(index))
					continue;

				Vector3 candidateWorldPosition = stageRoot.TransformPoint(activeNodePositions[index]);
				float sqrDistance = (candidateWorldPosition - worldPosition).sqrMagnitude;
				if (sqrDistance > captureRadiusSqr)
					continue;
				if (sqrDistance < bestSqrDistance)
				{
					bestSqrDistance = sqrDistance;
					bestIndex = index;
				}
			}

			if (bestIndex < 0)
				return false;

			nodeIndex = bestIndex;
			nodeWorldPosition = stageRoot.TransformPoint(activeNodePositions[bestIndex]);
			return true;
		}

		/// <summary>
		/// worldPosition 이 속한 셀이 이미 배치물로 점유됐는지 — 배치 UI 프리뷰가 유효/무효 색을
		/// 이 메서드로 판정(TryPlaceTower/TryPlaceHarvester 내부 점유 판정과 동일 규칙 재사용).
		/// </summary>
		public bool IsCellOccupied(Vector3 worldPosition)
		{
			if (occupiedCells.Contains(ToCellKey(worldPosition)))
				return true;

			// 암반 위에는 못 짓는다 — 화면에 바위가 보이는데 그 위에 세워지면 규칙과 그림이 어긋난다.
			return IsObstacleAt(worldPosition);
		}

		/// <summary> 그 자리가 암반인지(무대 로컬 환산 후 판정). 고정 판이면 항상 false. </summary>
		public bool IsObstacleAt(Vector3 worldPosition)
		{
			if (mapLayout == null || stageRoot == null)
				return false;

			return mapLayout.IsBlocked(stageRoot.InverseTransformPoint(worldPosition));
		}

		// 셀 키 = FloorToInt(worldPosition), y 는 0 고정(층 무관 단일 격자 — 위로 쌓기 원천 차단).
		private static Vector3Int ToCellKey(Vector3 worldPosition)
		{
			Vector3Int cell = Vector3Int.FloorToInt(worldPosition);
			cell.y = 0;
			return cell;
		}

		private IEnumerator SpawnDefensiveUnitRoutine(Unit unitData, TacticProgram tactic, Vector3 worldPosition, bool isHarvester, float incomeMultiplier = 1f, TowerDefenseTowerArchetype towerArchetype = null, bool isOuterNode = false, bool isGenerator = false)
		{
			if (unitData == null || unitData.Prefab == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: 배치 유닛 데이터/Prefab 미할당 — 스폰 불가(자원은 이미 차감됨).");
				yield break;
			}

			// 어떤 인형이냐에 따라 색·덩치가 갈린다 — 세우기 전에 정해야 문에 넘길 수 있다.
			Color tint = isGenerator ? stage.GeneratorTint
				: isHarvester ? stage.HarvesterTint
				: (towerArchetype != null ? towerArchetype.Tint : stage.TowerTint);

			SpawnedUnit spawned = new();
			yield return SpawnUnitRoutine(unitData, worldPosition, DEFENDER_TEAM,
				tint, isHarvester ? stage.HarvesterScale : stage.TowerScale, spawned);
			if (spawned.Ok == false)
				yield break; // 자원은 이미 차감됐지만 좀비 스폰은 막는다.

			GameObject unitGameObject = spawned.GameObject;
			UnitObject unitObject = spawned.UnitObject;
			MatchCombatant combatant = spawned.Combatant;


			if (tactic != null)
			{
				TacticDriver driver = unitObject.GetComponent<TacticDriver>();
				if (driver == null)
					driver = unitObject.gameObject.AddComponent<TacticDriver>();
				driver.Initialize(tactic, targeting, timeManager);
				drivers.Add(driver);
			}

			// 표적 등록은 세우는 문이 이미 했다.

			// 세워둔 포탑의 사거리를 옅게 늘 보여준다 — 「어디가 비었나」는 기존 커버리지가 보여야 알 수 있다.
			if (isHarvester == false && isGenerator == false)
			{
				if (towerArchetype != null)
				{
					TowerDefenseWeapon weapon = unitObject.GetComponent<TowerDefenseWeapon>();
					if (weapon == null)
						weapon = unitObject.gameObject.AddComponent<TowerDefenseWeapon>();
					weapon.Configure(towerArchetype, targeting, combatant, waveEnemies, IsVisibleAt, DamageMultiplierFor, () => Adaptation, () => TowerRangeMultiplier);
				}

				// 지어놓은 포탑의 원도 연구를 따라 자란다 — 원형 그대로 그리면 총과 원이 갈라진다.
				// ★ 여기는 이미 위에서 채집·발전을 걸러낸 안쪽이다 — 물건별로 원을 갈라 그리는 분기를
				//   여기 두면 절대 안 도는 죽은 코드가 된다(그렇게 넣었다가 라이브에서 「잴 것이 0개」로 드러났다).
				float towerRange = (towerArchetype != null ? towerArchetype.Range : RawTowerRange())
					* TowerRangeMultiplier;
				if (towerRange > 0f)
				{
					// ★ 사거리 원은 *묻는 순간에만* 뜬다(사용자 지시: "계속 보이니까 정신없어").
					//   수십 개가 상시로 겹치면 원이 정보가 아니라 노이즈가 된다 — 마우스를 얹거나
					//   설치 미리보기 중일 때만 켠다. 전부 보고 싶으면 디버그 토글(ShowAllRanges).
					Color ringColor = towerArchetype != null ? towerArchetype.Tint : new Color(0.45f, 0.72f, 1f, 1f);
					ringColor.a = 0.55f;
					TowerDefenseRing ring = TowerDefenseRing.Create(
						unitGameObject.transform, "RangeRing", ringColor, 0.08f, 0.05f);
					ring.SetRadius(towerRange);
					ring.SetVisible(showAllRanges);
					rangeRings.Add(ring);
				}
			}

			AddVisionSource(worldPosition,
				isGenerator ? stage.GeneratorVisionRadius
					: isHarvester ? stage.HarvesterVisionRadius
					: (towerArchetype != null ? towerArchetype.VisionRadius : stage.CoreVisionRadius));

			// 세운 인형에게 이름 — 벽·함정은 물건이지만 인형은 아이다(이 경로로 오는 것은 전부 인형).
			BuiltCount++;
			RegisterDoll(unitGameObject.transform,
				isGenerator ? stage.GeneratorTint
					: isHarvester ? stage.HarvesterTint
					: (towerArchetype != null ? towerArchetype.Tint : stage.TowerTint),
				isHarvester,
				// ★ 저장은 *내가 세운 것*만 되살려야 한다 — 영웅처럼 판이 스스로 만드는 것을 건물로 적으면
				//   이어할 때마다 유령 포탑이 한 채씩 는다(실측: 3채 저장 → 4채 복원).
				isPlacedBuilding: true,
				// ★ 종류를 안 적으면 4종을 세워놨어도 전부 기본형으로 되살아난다.
				variant: TowerArchetypeIndexOf(towerArchetype));

			// 모든 내 건물이 보급 사슬의 징검다리 — 포탑을 늘어놓는 것이 곧 보급선을 잇는 일이 된다.
			supplyChain.Add(unitGameObject.transform);

			if (isGenerator)
				powerGrid.AddGenerator(unitGameObject.transform);

			// 포탑·채집은 전기를 먹는다(발전은 안 먹는다 — 발전이 전기를 먹으면 자기 꼬리를 문다).
			if (isGenerator == false)
				powerGrid.AddConsumer(unitGameObject.transform);

			if (isHarvester)
			{
				harvesterTransforms.Add(unitGameObject.transform);
				harvesterIsOuter[unitGameObject.transform] = isOuterNode;
			}

			RefreshSupply(); // 수입은 「지을 때 더한다」가 아니라 「지금 몇 개가 이어져 있나」로 정해진다.
		}

		/// <summary>
		/// 매치 생명주기 정리 — 단일 경로 + *재진입 가능*(ArenaMatch.Dispose 와 동형): 틱 콜백 해제 +
		/// 드라이버 정지/클리어 + targeting unregister + 스폰 유닛 풀 반환 + 지면 파괴 + 진입 상태 리셋.
		/// 컬렉션 비움으로 멱등(Dispose→Destroy→OnDestroy 이중 호출 무해).
		/// StopAllCoroutines 를 최우선 호출 — 진행 중이던 스폰 코루틴(웨이브/배치)이 pool/targeting/core 필드
		/// null화 이후 재개돼 NRE 나는 것을 원천 차단(스펙#A belt). 코루틴 내부 yield 직후 null 가드는 braces.
		/// </summary>
		public void Dispose()
		{
			StopAllCoroutines();
			ticking = false;

			if (TimeManager.TryGetExistingInstance(out TimeManager existingTimeManager))
				existingTimeManager.RemoveCallback(Tick);

			foreach (TacticDriver driver in drivers)
			{
				if (driver != null)
					driver.StopDriving();
			}
			drivers.Clear();

			if (targeting != null)
			{
				foreach (ICombatant combatant in registeredCombatants)
					targeting.Unregister(combatant);
			}
			matchGeneration++; // 판을 접는다 — 진행 중이던 소환은 전부 남의 판 것이 된다.
			registeredCombatants.Clear();
			waveEnemies.Clear();
			claimedNodes.Clear();
			occupiedCells.Clear();

			if (ObjectPoolManager.TryGetExistingInstance(out ObjectPoolManager existingPool))
			{
				foreach (GameObject unit in spawnedUnits)
					ReleaseUnit(existingPool, unit);
			}
			spawnedUnits.Clear();

			if (stageRoot != null)
			{
				for (int childIndex = stageRoot.childCount - 1; childIndex >= 0; childIndex--)
					Destroy(stageRoot.GetChild(childIndex).gameObject);
			}

			// ★ 신호장 그림도 여기서 놓는다. 무대 자식은 위에서 전부 파괴하지만 **파괴는 프레임 끝에**
			//   일어나므로 그 사이 이 참조는 아직 살아 있다 — 같은 프레임에 새 판이 시작하면 *죽을 예정인*
			//   그림에 계속 그리게 된다. 참조를 여기서 끊으면 그런 틈이 아예 없다.
			signalView = null;

			// 재진입 — 다음 Begin() 이 새 매치를 돌릴 수 있게 진입 상태 리셋.
			core = null;
			coreCombatant = null;
			targeting = null;
			pool = null;
			timeManager = null;
			started = false;
		}

		private void OnDestroy()
		{
			Dispose();
		}
	}
}
