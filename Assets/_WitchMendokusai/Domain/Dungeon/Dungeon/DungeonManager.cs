using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public partial class DungeonManager : MonoBehaviour
	{
		public static DungeonManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out DungeonManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		public Dungeon CurDungeon { get; private set; }
		public DungeonContext Context { get; private set; }
		public DungeonRecord Result => dungeonRecorder.ResultRecord;

		public bool IsDungeon { get; private set; }
		// 승패 연출 중 (결과창 지연 대기). 이 동안 새 레벨업 카드와 상자 보상은 열지 않음
		public bool IsEnding => clearDelayRoutine != null;
		public RunLoadout RunLoadout => dataManager.RunLoadout;
		public SurvivalRunRules RunRules => resourceManager.SurvivalRunRules;
		public int RunEquipmentSlotCount => resourceManager.SurvivalRunRules.RandomModeSlotCount;
		// 위협 단계에 켠 제약의 물량과 정예를 얹은 값
		public DungeonThreatEffect CurThreat { get; private set; } = DungeonThreatEffect.Identity;
		// 켠 제약의 판 규칙 효과 (회복, 선택지, 시작 체력, 난이도 간격). 판 시작 때 정함
		public DungeonConstraintRunEffect ConstraintEffect { get; private set; } = DungeonConstraintRunEffect.Identity;
		// 제약 고유 규칙 (회복 감소의 냥, 처치 상자 등). 판 시작 Begin, 판 끝 End
		public DungeonConstraintSideRules SideRules { get; private set; }

		// 판 안 냥. 이번 판에 주운 냥만 맵 오브젝트 비용으로 씀, 들고 온 냥은 못 씀. 안 쓴 냥은 기존대로 가져감
		// 판 안에서만 부름. 판 밖이면 기록이 없어 예외
		public int RunNyang => IsDungeon ? Mathf.Max(0, dungeonRecorder.NyangSinceStart) : throw new InvalidOperationException("판 밖에서 판 안 냥 조회");

		public bool TrySpendRunNyang(int amount)
		{
			if (amount < 0)
				throw new ArgumentOutOfRangeException(nameof(amount), amount, "냥 비용은 0 이상");
			if (RunNyang < amount)
				return false;
			dataManager.GameStat[GameStatType.NYANG] -= amount;
			return true;
		}

		public int GetMaxThreatLevel(Dungeon dungeon) => dungeon.HasThreatLevels ? DungeonThreatRules.MaxLevel(resourceManager.SurvivalRunRules.ThreatLevels) : 0;
		public DungeonThreatEffect ResolveThreat(Dungeon dungeon, int level) => dungeon.HasThreatLevels ? DungeonThreatRules.Resolve(resourceManager.SurvivalRunRules.ThreatLevels, level) : DungeonThreatEffect.Identity;

		[SerializeField] private CardManager cardManager;
		[SerializeField] private MonsterSpawner monsterSpawner;
		// 개발자 창 맵 오브젝트 배치 (mapobj)
		public MonsterSpawner MonsterSpawner => monsterSpawner;
		[SerializeField] private ResourceNodeSpawner resourceNodeSpawner;
		[SerializeField] private ExpManager expChecker;

		private UIDungeon dungeonUI = null;

		private DungeonRecorder dungeonRecorder = null;
		private DungeonObjectiveStrategy dungeonStrategy = null;
		private IDisposable dungeonLoopSubscription;
		private bool isTransitioning;
		private Vector3 dungeonSpawnPosition;

		private SOManager soManager;
		private UIManager uiManager;
		private CameraManager cameraManager;
		private StageManager stageManager;
		private GameEventManager gameEventManager;
		private PlayerProvider playerProvider;
		private DataManager dataManager;
		private GameManager gameManager;
		private ResourceManager resourceManager;
		private Coroutine clearDelayRoutine;
		private int targetClearCount;
		private float hpRegenElapsed;
		private Action stopFlowFx;
		private string objectiveText;
		private RuntimeQuest dungeonQuest;
		// 판 시작부터 귀환 초기화까지. 결과창이 떠 있는 동안도 참 (IsDungeon 은 거짓)
		private bool isRunActive;

		[Inject]
		public void Construct(SOManager soManager, UIManager uiManager, CameraManager cameraManager, StageManager stageManager, GameEventManager gameEventManager, PlayerProvider playerProvider, DataManager dataManager, GameManager gameManager, ResourceManager resourceManager, IObjectResolver container)
		{
			this.resourceManager = resourceManager;
			this.soManager = soManager;
			this.uiManager = uiManager;
			this.cameraManager = cameraManager;
			this.stageManager = stageManager;
			this.gameEventManager = gameEventManager;
			this.playerProvider = playerProvider;
			this.dataManager = dataManager;
			this.gameManager = gameManager;
			SideRules = new DungeonConstraintSideRules(dataManager, playerProvider, container);
			DungeonManagerBridge.Register(this);
			monsterSpawner.SetDungeonManager(this);
			container.Inject(monsterSpawner);
			container.Inject(resourceNodeSpawner);
		}

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			// TASK-WM-115 R5 — dungeonUI(=UIDungeon) 는 UIManager.Start 가 Instantiate.
			// Awake 는 모든 Start *전* → 여기서 FindAnyObjectByType<UIDungeon> = 항상 null
			// → StartDungeon 의 dungeonUI.SetPanel(:166) 항상 NRE (deterministic pre-existing).
			// 근본: eager(Awake)→lazy(사용시점, 던전 진입 = UIManager.Start 후). R1 동형.
		}

		// dungeonUI lazy 확정 — 던전 생명주기 진입 시(UIManager.Start 이후) 1회 resolve. 멱등.
		// 던전-time 에도 진짜 없으면 dungeonUI.SetPanel 이 자연 FastFail (마스킹 X).
		private void EnsureDungeonUI()
		{
			if (dungeonUI == null)
				dungeonUI = FindAnyObjectByType<UIDungeon>(FindObjectsInactive.Include);
		}

		private void OnBossKilled(BossKilledEvent killed)
		{
			if (IsDungeon)
				CurDungeon.RecordBossKill(killed.Monster.ID);
		}

		private void OnDestroy()
		{
			EventBusBridge.Unsubscribe<BossKilledEvent>(OnBossKilled);
			SideRules?.End();
			if (Instance == this)
				Instance = null;
			DungeonManagerBridge.Register(null);
			SkyTimeOverride.Clear();
			StopFlowFx();
			// 씬이 내려가거나 Play 가 끝나도 도는 판 타이머 해제
			dungeonLoopSubscription?.Dispose();
			dungeonLoopSubscription = null;
		}

		// 판의 시작과 끝 연출 (입장, 승리, 패배). 한 번에 하나, 새로 틀면 앞의 것은 걷음
		private void PlayFlowFx(CombatFx fx, string title, string subtitle)
		{
			StopFlowFx();
			if (fx == null)
				return;
			Transform playerTransform = playerProvider.CurrentObject.transform;
			stopFlowFx = fx.Play(new CombatFxContext
			{
				Position = playerTransform.position,
				Direction = playerTransform.forward,
				Follow = playerTransform,
				Title = title,
				Subtitle = subtitle,
			});
		}

		private void StopFlowFx()
		{
			stopFlowFx?.Invoke();
			stopFlowFx = null;
		}

		private void Start()
		{
			// 당장 게임 이벤트 변화가 많아서, 인스펙터에서 GameEventListener 넣는 것보다, 이렇게 하드 코딩하는게 나은 듯
			gameEventManager.RegisterCallback(GameEventType.OnPlayerDied, EndDungeonByDefeat);
			// 보스 처치 기록 (제약 해금 조건). 패배한 판의 처치도 기록
			EventBusBridge.Subscribe<BossKilledEvent>(OnBossKilled);
		}

		public void StartDungeon(Dungeon dungeon, bool restart = false, DungeonRunMode runMode = DungeonRunMode.Equipped, int threatLevel = 0)
		{
			if (IsDungeon || isTransitioning)
				return;
			dungeon.ValidateWaves();
			dungeon.ValidateMapObjects();
			// 해금 다시 판정 (다시 하기는 입장 창을 안 거침). 제약 데이터가 잘못이면 여기서 실패, 전환 표시 전이라 다음 입장이 막히지 않음
			dungeon.RefreshConstraintUnlocks(dataManager.QuestManager.CreateCriteriaContext());
			DungeonConstraintRunEffect constraintEffect = DungeonConstraintRules.RunEffect(dungeon.SelectedConstraints());
			isTransitioning = true;
			Debug.Log($"{nameof(StartDungeon)} ({dungeon.Name})");

			EnsureDungeonUI();
			CurDungeon = dungeon;
			ConstraintEffect = constraintEffect;
			CurThreat = ConstraintEffect.ApplyTo(ResolveThreat(dungeon, threatLevel));
			dungeonStrategy = DungeonObjectiveStrategyFactory.Create(dungeon);

			Stage stage = dungeon.Stages[0];

			// 재도전은 결과창에서 바로 옴. 남은 승패 연출을 덮기 전에 걷음
			StopFlowFx();
			SurvivalFeelConfig feel = resourceManager.SurvivalFeel;

			// 던전 전용 전환. 칸이 비면 기본 (portalWipe)
			uiManager.Transition.Transition(
				aDuringTransition: () =>
				{
					if (restart == false)
					{
						stageManager.LoadStage(stage);
						dungeonSpawnPosition = playerProvider.CurrentObject.transform.position;
					}
					else
					{
						playerProvider.CurrentObject.UnitMovement.Teleport(dungeonSpawnPosition);
					}
					InitDungeonAndPlayer();
				},
				aWhenEnd: () =>
				{
					isTransitioning = false;
					// 도입 연출이 있으면 그 띠가 던전 이름을 보이므로 스테이지 팝업은 생략
					if (feel.DungeonIntroFx != null)
						PlayFlowFx(feel.DungeonIntroFx, dungeon.Name, objectiveText);
					else
						uiManager.StagePopup(stage);
				},
				overrideWipe: feel.DungeonEnterWipe).Forget();

			void InitDungeonAndPlayer()
			{
				RunSeed.Begin();
				gameManager.Init();
				dataManager.RunLoadout.Begin(runMode, resourceManager.SurvivalRunRules);
				gameManager.InitEquipment();
				gameManager.ApplyUpgradeEffects(); // [VamsurLike-Upgrade] - KarmoDDrine 2026-01-12
				ApplyStartHealth();

				expChecker.SetRequirementMultiplier(dungeon.WaveOptions.ExpRequirementMultiplier(CurThreat.WaveRate));
				expChecker.Init();
				cardManager.Reset();
				soManager.DungeonItemBuffer.Clear();
				hpRegenElapsed = 0f;

				Context = new DungeonContext
				(
					initialDungeonTime: new TimeSpan(0, 0, dungeon.TimeBySecond),
					constraints: dungeon.SelectedConstraints(),
					dungeonStat: dataManager.DungeonStat,
					difficultyStepSeconds: ConstraintEffect.ScaleDifficultyStep(resourceManager.SurvivalRunRules.DifficultyStepSeconds)
				);

				dungeonRecorder = new DungeonRecorder(this, dataManager);
				SideRules.Begin(DungeonConstraintRules.SideEffect(Context.Constraints));

				IsDungeon = true;
				isRunActive = true;
				GamePresence.EnterDungeon(dungeon.Name);

				// Create Dungeon Quest
				{
					RuntimeQuest runtimeQuest = dungeonStrategy.CreateRuntimeQuest(dungeon, dataManager.QuestManager.CreateCriteriaContext());
					dataManager.QuestManager.AddQuest(runtimeQuest);
					dungeonQuest = runtimeQuest;
					objectiveText = runtimeQuest.Name;
				}

				monsterSpawner.transform.position = playerProvider.Current.transform.position;
				monsterSpawner.InitWaves(dungeon);
				resourceNodeSpawner.InitWaves(dungeon);

				// StartDungeonLoop();
				{
					// RuntimeQuest를 통해 DungeonClear 수치가 1 오르면 던전 종료
					targetClearCount = dataManager.DungeonStat[DungeonStatType.DUNGEON_CLEAR] + 1;

					dungeonLoopSubscription = Observable.Interval(TimeSpan.FromSeconds(0.1f))
						.TakeWhile(_ => IsClearReached() == false)
						.Subscribe(_ =>
						{
							Context.UpdateTime();
							Context.UpdateDifficulty();
							monsterSpawner.UpdateWaves();

						}, HandleClear);
				}

				// Context 생성 이후 UI 설정
				// UIDungeonRuntime.UpdateUI(); 에서 Context를 사용합니다.
				dungeonUI.SetPanel(DungeonPanelType.DungeonRuntime);
				cameraManager.SetContentCameraMode(ContentCameraMode.Dungeon);

				gameEventManager.Raise(GameEventType.OnDungeonStart);
			}
		}

		private void Update()
		{
			if (IsDungeon == false)
				return;

			UpdateRunSky();
			GamePresence.SetLevel((int)playerProvider.Current.UnitStat[UnitStatType.LEVEL_CUR]);

			float interval = resourceManager.SurvivalFeel.Upgrade.HpRegenInterval;
			hpRegenElapsed += Time.deltaTime;
			if (hpRegenElapsed < interval)
				return;
			hpRegenElapsed -= interval;

			PlayerObject player = playerProvider.CurrentObject;
			int regenAmount = player.UnitStat[UnitStatType.HP_REGENERATION];
			if (regenAmount > 0 && player.UnitStat[UnitStatType.HP_CUR] < player.UnitStat[UnitStatType.HP_MAX])
				player.ReceiveHeal(regenAmount);
		}

		// 판 진행률에 맞춰 하늘 시각을 시작에서 끝 시각으로. 결과창 동안은 마지막 값 유지, 귀환 때 해제
		private void UpdateRunSky()
		{
			SurvivalFeelConfig feel = resourceManager.SurvivalFeel;
			if (feel.UseRunSky == false || Context == null)
			{
				SkyTimeOverride.Clear();
				return;
			}
			double total = Context.InitialDungeonTime.TotalSeconds;
			float progress = total > 0d ? Mathf.Clamp01((float)(Context.ElapsedTime.TotalSeconds / total)) : 0f;
			SkyTimeOverride.Set(Mathf.Lerp(feel.RunSkyStartHour, feel.RunSkyEndHour, progress));
		}

		// 원래 목표와 별개로 판의 목표 퀘스트를 완료. 보상과 클리어 집계는 퀘스트 완료 경로 그대로
		public void CompleteObjective()
		{
			if (IsDungeon == false || dungeonQuest.State == RuntimeQuestState.Completed)
				return;
			dungeonQuest.Complete();
		}

		// 개발 확인용. 진행 중인 판에 웨이브 패턴 하나를 지금 발동
		public void TriggerWavePattern(WavePattern pattern) => monsterSpawner.TriggerPattern(pattern);

		private bool IsClearReached()
		{
			int curClearCount = dataManager.DungeonStat[DungeonStatType.DUNGEON_CLEAR];
			if (curClearCount > targetClearCount)
			{
				Debug.LogWarning($"Dungeon Clear Count is over target: {curClearCount} > {targetClearCount}");
			}
			return curClearCount >= targetClearCount;
		}

		// 사망 연출을 보인 뒤 결과창으로, 같은 판에서 클리어가 뒤따르면 클리어 우선
		private void EndDungeonByDefeat()
		{
			if (IsDungeon == false || clearDelayRoutine != null)
				return;
			SurvivalFeelConfig feel = resourceManager.SurvivalFeel;
			cardManager.CancelSelection();
			CombatSfx.Play(CombatSfxEvent.Defeat);
			PlayFlowFx(feel.DefeatFx, feel.DefeatTitle, CurDungeon.Name);
			clearDelayRoutine = StartCoroutine(EndDungeonAfterDelay(feel.DefeatResultDelay, false));
		}

		// 클리어는 승리 연출을 보인 뒤 결과창으로. 보스가 아닌 목표 (시간 생존 등) 도 같음
		private void HandleClear()
		{
			if (clearDelayRoutine != null)
			{
				StopCoroutine(clearDelayRoutine);
				clearDelayRoutine = null;
			}
			cardManager.CancelSelection();
			SurvivalFeelConfig feel = resourceManager.SurvivalFeel;
			float delay = feel.BossClearResultDelay;
			if (delay <= 0f)
			{
				EndDungeon(isCleared: true);
				return;
			}
			// 패배 연출 중에 클리어가 뒤따르면 여기서 승리 연출로 바뀜
			CombatSfx.Play(CombatSfxEvent.Victory);
			PlayFlowFx(feel.VictoryFx, feel.VictoryTitle, CurDungeon.Name);
			playerProvider.CurrentObject.GrantInvincibility(delay);
			clearDelayRoutine = StartCoroutine(EndDungeonAfterDelay(delay, true));
		}

		private IEnumerator EndDungeonAfterDelay(float delay, bool isCleared)
		{
			yield return new WaitForSecondsRealtime(delay);
			clearDelayRoutine = null;
			EndDungeon(isCleared);
		}

		public void EndDungeon(bool isCleared = true)
		{
			if (IsDungeon == false)
				return;
			IsDungeon = false;
			// 폴링이 보기 전에 죽어도 클리어 보상을 이미 받았으면 클리어
			if (IsClearReached())
				isCleared = true;
			if (clearDelayRoutine != null)
			{
				StopCoroutine(clearDelayRoutine);
				clearDelayRoutine = null;
			}
			cardManager.CancelSelection();
			Debug.Log($"{nameof(EndDungeon)}");

			// Stop DungeonLoop
			dungeonLoopSubscription?.Dispose();
			dungeonLoopSubscription = null;
			monsterSpawner.StopWave();
			resourceNodeSpawner.StopWave();
			SideRules.End();

			if (isCleared == false)
				dataManager.GameStat[GameStatType.NYANG] += CurDungeon.CalculateDefeatNyang(dungeonRecorder.KillCountSinceStart);
			else if (CurDungeon.HasThreatLevels)
				CurDungeon.RecordThreatClear(CurThreat.Level);
			// 위협 배율과 냥 보너스, 클리어 보상과 패배 보상과 레벨업 전환을 포함한 이번 판 획득분 기준
			dataManager.GameStat[GameStatType.NYANG] += DungeonThreatRules.BonusNyang(dungeonRecorder.NyangSinceStart, CurThreat.RewardMultiplier);
			dataManager.GameStat[GameStatType.NYANG] += dungeonRecorder.NyangSinceStart * playerProvider.Current.UnitStat[UnitStatType.GOLD_BONUS] / UnitStat.PERCENT;
			// 위험도 단계 첫 클리어 보상. 위협 배율과 냥 보너스에 안 섞이게 그 뒤, 결과창 합계에는 들어가게 기록 전
			if (isCleared)
				GrantRiskRewards();
			DungeonRecord result = dungeonRecorder.CaptureResultRecord((int)playerProvider.Current.UnitStat[UnitStatType.LEVEL_CUR], isCleared);
			RecordRunResult(result);

			// 결과창을 가리지 않게 남은 승패 연출 (물들임, 띠, 카메라 당김) 을 걷음
			StopFlowFx();
			dungeonUI.SetPanel(DungeonPanelType.DungeonResult);
		}

		// 시작 체력 제약. 영구 강화로 정한 최대 체력 기준
		private void ApplyStartHealth()
		{
			PlayerObject player = playerProvider.CurrentObject;
			int target = ConstraintEffect.StartHealth(player.UnitStat[UnitStatType.HP_MAX]);
			int spend = player.UnitStat[UnitStatType.HP_CUR] - target;
			if (spend > 0)
				player.Health.SpendHealth(spend);
		}

		private void GrantRiskRewards()
		{
			foreach (DungeonConstraintRewardRule.RiskMilestone milestone in CurDungeon.RecordClear(Context.Constraints, dataManager.CurDollID))
			{
				Debug.Log($"위험도 {milestone.Risk} 첫 클리어 보상 ({CurDungeon.name})");
				Reward.GetReward(milestone.Rewards, soManager.ItemInventory, dataManager.GameStat);
			}
		}
	}
}

