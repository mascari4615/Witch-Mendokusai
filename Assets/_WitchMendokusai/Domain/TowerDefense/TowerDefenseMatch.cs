using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ★ 이 파일의 좌표는 「판정 쪽」이다 (TASK-WM-214).
//   개척 판의 셈은 거의 전부 시뮬이고(Vector3 118 · Vector2Int 27 · Vector3Int 13),
//   엔진을 실제로 만지는 자리는 스무 곳 남짓((Vector3)transform.position 등)이다.
//   그래서 이 파일에서 Vector* 는 SDK 타입을 뜻하고, 엔진으로 나갈 때만 자동으로 변환된다.
//   반대로 엔진 값을 받아올 때는 캐스트가 필요하다 — 그 자리가 곧 경계다.
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) 매치 오케스트레이터 — ArenaMatch 와 동형 셸(맵 생성 → 유닛 스폰(기존 풀, 자동 DI)
	/// → MatchCombatant/TacticDriver 부착 → TargetingSystem 등록 → TimeManager 틱으로 TowerDefenseCore 폴).
	/// 규칙 판단은 전부 순수 코어(TowerDefenseCore)에 있고 본 셸은 그 신호(TowerDefenseSignal)를 받아
	/// 스폰/자원차감/정리 같은 actuation 만 수행 — Arena 아키텍처의 "코어=브레인, 셸=손발" 원칙 그대로 재사용.
	/// 배치 UI/입력전략/게임모드진입/카메라는 별도 증분(본 셸은 매치 진행 자체만 담당).
	/// </summary>
	public partial class TowerDefenseMatch : MonoBehaviour
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

		// ★ 몇 번째 판인가. 인형을 세우는 일은 한 프레임 쉬었다 이어지는데, 그 사이에 판이 통째로
		//   갈릴 수 있다(다시 시작). 그때 「판이 사라졌나」만 보면 *새 판이 이미 서 있어서* 검사를
		//   통과하고, 지난 판이 부른 인형이 새 판에 세워진다.
		//   실측: 아무것도 안 지은 무방비 판에 지난 판 영웅이 서서 세 웨이브를 막았고, 코어가 안 죽어
		//   승리도 패배도 없는 판이 됐다. 「사라졌나」가 아니라 **「그 판이 맞나」**를 물어야 한다.
		private int matchGeneration;
		private readonly List<ICombatant> registeredCombatants = new();
		private readonly List<TacticDriver> drivers = new();
		// 판을 *그리는* 층 — 바닥·암반·길 표시·표식. 규칙과 그림을 갈라둔다.
		private readonly TowerDefenseTerrainView terrainView = new();

		// 신호장 그림 — 덮인 땅의 테두리와 퍼져 나가는 파동. 무대가 서는 순간 만들어진다.
		private TowerDefenseSignalView signalView;

		// ★ 코어도 자란다(사용자 지시: "코어 건물 자체의 레벨도 있어서 그것도 선택지 있으면 좋을듯").
		//   새 선택지 체계를 하나 더 만들지 않고 *이미 있는 드래프트 카드*를 코어 레벨업에 붙였다 —
		//   웨이브가 부르던 카드를 코어 성장이 부르게 바꾼 것뿐이다. 체계가 둘로 갈리면 같은 선택이
		//   두 곳에서 다른 규칙으로 살게 된다.
		//   성장 곡선은 스테이지가 정한다(Begin 에서 세운다) — 코드에 박아 두면 스테이지에서 아무리
		//   만져도 코어만 옛 속도로 자란다.
		private TowerDefenseBuildingProgress coreProgress;
		private TowerDefenseRing highlightedRing;

		// 이번 매치의 판 — 절차 생성이면 layout 이 정본, 끄면 null 이고 스테이지 SO 의 고정 레이아웃을 쓴다.
		// 아래 active* 목록이 *둘을 하나로 합친 단일 출처* — 매치 본문은 어느 쪽인지 신경 쓰지 않는다.
		// 시야 — 내 건물이 밝힌 만큼만 보인다. 건물은 안 움직이므로 *지어질 때만* 다시 계산한다.
		private TowerDefenseVision vision;
		private TowerDefenseFogView fogView;
		private readonly List<TowerDefenseVision.Source> visionSources = new();
		private readonly List<TowerDefenseVision.Source> scaledVisionSources = new();

		private TowerDefenseMapLayout mapLayout;
		private TowerDefenseFlowField flowField;
		private ITacticNavigator flowNavigator;
		private Vector3 activeCorePosition;

		public event Action<TowerDefenseOutcome> MatchEnded = delegate { };

		/// <summary>
		/// 이번 판의 난이도 — 다음 판에도 유지된다(매번 다시 고르게 하면 그건 설정이 아니라 잔소리다).
		/// 판이 도는 중에 바꿔도 이미 시작한 판에는 안 걸린다(시작 조건이므로).
		/// </summary>
		public TowerDefenseDifficultyKind Difficulty { get; set; } = TowerDefenseDifficultyKind.Normal;

		private TowerDefenseDifficulty difficulty = TowerDefenseDifficulty.For(TowerDefenseDifficultyKind.Normal);

		/// <summary>
		/// 진행 중인 스테이지 데이터(읽기 전용) — 검증 하네스가 좌표·수치를 **정본에서 읽게** 한다.
		/// 하네스에 좌표를 박아두면 레이아웃을 옮기는 순간 검사가 조용히 무의미해진다(항상 거절만 확인).
		/// </summary>
		public TowerDefenseStageSO Stage => stage;

		/// <summary> 코어 참가자(진단용) — 적이 코어를 실제로 때리고 있는지 체력으로 확인한다. </summary>
		public MatchCombatant CoreCombatant => coreCombatant;

		/// <summary> 매치에 등록된 전 참가자(진단용) — 수비 유닛 생존 여부 확인. </summary>
		public IReadOnlyList<ICombatant> RegisteredCombatants => registeredCombatants;
		public TowerDefensePhase Phase => core != null ? core.Phase : TowerDefensePhase.Prepare;
		public TowerDefenseOutcome Outcome => core != null ? core.Outcome : TowerDefenseOutcome.InProgress;
		public float PrepareRemaining => core != null ? core.PrepareRemaining : 0f;

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
				// 스테이지 SO 는 디자이너가 인스펙터에서 적는 자리(엔진 쪽)라 값을 들일 때 캐스트한다 (TASK-WM-214).
			activeCorePosition = stage.CorePosition.ToSim();
				activeGroundWidth = stage.GroundWidth;
				activeGroundLength = stage.GroundLength;

				if (stage.EnemySpawnPoints != null)
					for (int spawnIndex = 0; spawnIndex < stage.EnemySpawnPoints.Length; spawnIndex++)
			{
				activeSpawnPoints.Add(stage.EnemySpawnPoints[spawnIndex].ToSim());
			}
				if (stage.ResourceNodePositions != null)
				{
					foreach (UnityEngine.Vector3 nodeWorldPosition in stage.ResourceNodePositions)
					{
						activeNodePositions.Add(nodeWorldPosition.ToSim());
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
		/// 그 자리를 밝힌다(검증 전용) — 「밝힌 서식지만 지도에 뜬다」는 규칙 때문에, 밝히지 않으면
		/// 그 표시를 영영 못 잰다(못 잰 것을 통과로 세면 검사가 있으나 마나다).
		/// </summary>
		public void RevealForVerification(Vector3 worldPosition, float radius)
		{
			AddVisionSource(worldPosition, radius);
		}

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

		/// <summary> 판 크기(칸) — 상한이 판에 비해 충분한지 함께 봐야 판단이 된다. </summary>
		public int MapCellCount => mapLayout != null ? mapLayout.Width * mapLayout.Length : 0;

		// ── 라이브 검증용 창 ─────────────────────────────────────────────────────────
		// ★ 「돌아간다」를 사람 눈에만 맡기면 영영 안 재게 된다. 하네스가 판을 돌리며 직접 물어볼 수
		//   있어야 신호·서식지·침공이 *실제로* 살아 있는지 매번 확인된다(안 그러면 컴파일만 초록).
		public float CoreSignalCharge => powerGrid.Field.ChargeAt(0);
		public float CoreSignalRadius => powerGrid.Field.LiveRadiusAt(0);
		public int SignalNodeCount => powerGrid.Field.NodeCount;

		/// <summary>
		/// 이 판의 점수 재료 — 실시간이 되면서 「몇 웨이브를 넘겼나」는 척도가 아니게 됐다.
		/// 웨이브는 이제 시계가 40초마다 자동으로 부르므로, 오래 버틴 것이 곧 잘한 것이다.
		/// 둥지를 부순 수는 「버텼다」와 다른 축 — *밀어냈다*를 센다.
		/// </summary>
		public int SurvivedSeconds => core != null ? Mathf.FloorToInt(core.ElapsedSeconds) : 0;

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

		/// <summary> 첫 웨이브를 사람이 부르길 기다리는 중인가 — 화면이 「시계가 돈다」고 거짓말하지 않게. </summary>
		public bool IsWaitingForFirstCall =>
			core != null
			&& core.Phase == TowerDefensePhase.Prepare
			&& core.WaveIndex < core.FirstAutoWave
			&& core.IsNextWaveRequested == false;

		/// <summary> 그 자리가 지금 보이는가 — 안 보이면 포탑도 못 쏘고 마수도 안 그려진다. </summary>
		public bool IsVisibleAt(Vector3 worldPosition)
		{
			if (vision == null || mapLayout == null || stageRoot == null)
				return true; // 시야 없는 판(고정 레이아웃) = 전부 보임.

			return vision.IsVisible(mapLayout.WorldToCell(stageRoot.InverseTransformPoint(worldPosition.ToUnity()).ToSim()));
		}

		/// <summary> 한 번이라도 밝혔던 자리인가 — 기억한 지형·노드는 계속 보여준다. </summary>
		public bool IsExploredAt(Vector3 worldPosition)
		{
			if (vision == null || mapLayout == null || stageRoot == null)
				return true;

			return vision.IsExplored(mapLayout.WorldToCell(stageRoot.InverseTransformPoint(worldPosition.ToUnity()).ToSim()));
		}

		/// <summary> 시야원 하나 추가 + 즉시 반영 — 건물을 세운 그 순간 밝아져야 「넓혔다」가 읽힌다. </summary>
		private void AddVisionSource(Vector3 worldPosition, float radius)
		{
			if (vision == null || mapLayout == null || stageRoot == null || radius <= 0f)
				return;

			visionSources.Add(new TowerDefenseVision.Source(
				mapLayout.WorldToCell(stageRoot.InverseTransformPoint(worldPosition.ToUnity()).ToSim()), radius));
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

		/// <summary> 무대 루트 — 화면 표시가 로컬 좌표를 월드로 옮길 때 쓴다. </summary>
		public Transform StageRoot => stageRoot;

		/// <summary> 신호장을 화면에 그린다. 무대가 있어야 그릴 자리가 생기므로 여기서 늦게 만든다. </summary>
		private void TickSignalView()
		{
			if (stageRoot == null || stage == null)
				return;

			if (signalView == null)
				signalView = TowerDefenseSignalView.Create(stageRoot);

			signalView.Tick(powerGrid.Field, stage, Time.deltaTime);
		}

		/// <summary> 그 대상이 코어인가 — 화면이 「연구」 패널을 띄울지 정한다. </summary>
		public bool IsCore(MatchCombatant combatant) => combatant != null && combatant == coreCombatant;

		public int BuiltCount { get; private set; }
		public int LostCount { get; private set; }
		public int KilledCount { get; private set; }
		public int PeakEnemies { get; private set; }

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
		public int CorePendingChoices => coreProgress.PendingChoices;

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

		/// <summary>
		/// 지금까지 각 수단을 몇 번 썼나 — 적응이 0 일 때 「안 쐈다」와 「골고루 썼다」를 가르는 유일한 값.
		///
		/// ★ 적응은 총량이 아니라 *편중*으로 붙는다(한 수단이 1/3 을 넘게 차지해야 저항이 생긴다).
		///   그래서 「둔화 포탑을 세웠는데 저항이 0」은 결함일 수도, 규칙대로일 수도 있다 —
		///   이 숫자 없이는 그 둘을 못 가른다(실측에서 멀쩡한 것을 두 번 실패로 찍었다).
		/// </summary>
		public (int Slow, int Splash, int Pierce) AdaptationUseCounts
		{
			get
			{
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
				return (slowUses, splashHits, pierceHits);
			}
		}

		/// <summary> 지금 웨이브의 시야 배수 — 어스름이면 좁아진다. </summary>
		private float CurrentVisionScale()
		{
			return TowerDefenseWaveEvent.VisionScale(WaveEventAt(core != null ? core.WaveIndex : 0));
		}

		// 유출 지점은 코어만이 아니다 — 전초기지도 지켜야 할 곳이다(넓힌 만큼 늘어난다).
		private bool IsAtAnyGoal(Vector3 position, float radiusSqr)
		{
			if ((position - coreCombatant.Position).sqrMagnitude <= radiusSqr)
				return true;

			foreach (Transform outpost in outposts)
			{
				if (outpost != null && (position - outpost.position.ToSim()).sqrMagnitude <= radiusSqr)
					return true;
			}
			return false;
		}

		/// <summary>
		/// 이 판에서 마수가 굳었던 *자리* 수 — 「지형에 막힘 / 서로 막음」으로 갈라 센다.
		///
		/// ★ 왜 자리로 세나: 경고 줄 수는 같은 마수가 4초마다 다시 찍혀 부풀고 판 길이에 휘둘린다.
		///   자리 수는 「판의 어디가 막히는가」를 세므로 판끼리 견줄 수 있다(이 값 없이 한 판씩 비교하다
		///   두 번 헛짚었다 — 좋아진 줄 알았던 것이 그냥 다른 판이었다).
		/// </summary>
		public (int Total, int ByTerrain, int ByUnit) StuckCellSummary =>
			(stuckCells.Count, stuckByTerrainCells.Count, stuckByUnitCells.Count);

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
				TowerDefenseDebris.Spawn(stageRoot, enemy.Position.ToUnity(), waveEnemies,
					stage.DebrisSeconds, stage.DebrisSlowFactor, stage.GroundCellSize * 0.8f, stage.DebrisTint);
			}
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
