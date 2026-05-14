using System;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class DungeonManager : MonoBehaviour
	{
		public static DungeonManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out DungeonManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		public Dungeon CurDungeon { get; private set; }
		public DungeonContext Context { get; private set; }
		public DungeonRecord Result => dungeonRecorder.CaptureResultRecord();

		public bool IsDungeon { get; private set; }

		[SerializeField] private CardManager cardManager;
		[SerializeField] private MonsterSpawner monsterSpawner;
		[SerializeField] private ResourceNodeSpawner resourceNodeSpawner;
		[SerializeField] private ExpManager expChecker;

		private UIDungeon dungeonUI = null;

		private DungeonRecorder dungeonRecorder = null;
		private DungeonObjectiveStrategy dungeonStrategy = null;
		private IDisposable dungeonLoopSubscription;

		private SOManager soManager;
		private UIManager uiManager;
		private CameraManager cameraManager;
		private StageManager stageManager;
		private GameEventManager gameEventManager;
		private PlayerProvider playerProvider;
		private DataManager dataManager;
		private GameManager gameManager;

		[Inject]
		public void Construct(SOManager soManager, UIManager uiManager, CameraManager cameraManager, StageManager stageManager, GameEventManager gameEventManager, PlayerProvider playerProvider, DataManager dataManager, GameManager gameManager, IObjectResolver container)
		{
			this.soManager = soManager;
			this.uiManager = uiManager;
			this.cameraManager = cameraManager;
			this.stageManager = stageManager;
			this.gameEventManager = gameEventManager;
			this.playerProvider = playerProvider;
			this.dataManager = dataManager;
			this.gameManager = gameManager;
			DungeonManagerBridge.Register(this);
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

			dungeonUI = FindAnyObjectByType<UIDungeon>(FindObjectsInactive.Include);
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
			DungeonManagerBridge.Register(null);
		}

		private void Start()
		{
			// 당장 게임 이벤트 변화가 많아서, 인스펙터에서 GameEventListener 넣는 것보다, 이렇게 하드 코딩하는게 나은 듯
			gameEventManager.RegisterCallback(GameEventType.OnPlayerDied, EndDungeon);
		}

		public void StartDungeon(Dungeon dungeon)
		{
			Debug.Log($"{nameof(StartDungeon)} ({dungeon.Name})");

			CurDungeon = dungeon;
			dungeonStrategy = DungeonObjectiveStrategyFactory.Create(dungeon);

			Stage stage = dungeon.Stages[0];

			// TODO: 던전 Transition
			uiManager.Transition.Transition(
				aDuringTransition: () =>
				{
					stageManager.LoadStage(stage);
					InitDungeonAndPlayer();
				},
				aWhenEnd: () =>
				{
					uiManager.StagePopup(stage);
					// TODO: 던전 Intro?
				}).Forget();

			void InitDungeonAndPlayer()
			{
				gameManager.Init();
				gameManager.InitEquipment();
				gameManager.ApplyUpgradeEffects(); // [VamsurLike-Upgrade] - KarmoDDrine 2026-01-12

				expChecker.Init();
				cardManager.Reset();
				soManager.DungeonItemBuffer.Clear();

				Context = new DungeonContext
				(
					initialDungeonTime: new TimeSpan(0, 0, dungeon.TimeBySecond),
					constraints: dungeon.Constraints
				);

				dungeonRecorder = new DungeonRecorder(this, dataManager);

				IsDungeon = true;

				// Create Dungeon Quest
				{
					RuntimeQuest runtimeQuest = dungeonStrategy.CreateRuntimeQuest(dungeon);
					dataManager.QuestManager.AddQuest(runtimeQuest);
				}

				monsterSpawner.transform.position = playerProvider.Current.transform.position;
				monsterSpawner.InitWaves(dungeon);
				resourceNodeSpawner.InitWaves(dungeon);

				// StartDungeonLoop();
				{
					// RuntimeQuest를 통해 DungeonClear 수치가 1 오르면 던전 종료
					int targetClearCount = dataManager.DungeonStat[DungeonStatType.DUNGEON_CLEAR] + 1;
					bool IsClear()
					{
						int curClearCount = dataManager.DungeonStat[DungeonStatType.DUNGEON_CLEAR];
						if (curClearCount > targetClearCount)
						{
							Debug.LogWarning($"Dungeon Clear Count is over target: {curClearCount} > {targetClearCount}");
						}
						return curClearCount >= targetClearCount;
					};

					dungeonLoopSubscription = Observable.Interval(TimeSpan.FromSeconds(0.1f))
						.TakeWhile(_ => IsClear() == false)
						.Subscribe(_ =>
						{
							Context.UpdateTime();
							Context.UpdateDifficulty();
							monsterSpawner.UpdateWaves();

						}, () => EndDungeon());
				}

				// Context 생성 이후 UI 설정
				// UIDungeonRuntime.UpdateUI(); 에서 Context를 사용합니다.
				dungeonUI.SetPanel(DungeonPanelType.DungeonRuntime);
				cameraManager.SetContentCameraMode(ContentCameraMode.Dungeon);

				gameEventManager.Raise(GameEventType.OnDungeonStart);
			}
		}

		public void EndDungeon()
		{
			Debug.Log($"{nameof(EndDungeon)}");

			// Stop DungeonLoop
			dungeonLoopSubscription?.Dispose();
			dungeonLoopSubscription = null;
			monsterSpawner.StopWave();
			resourceNodeSpawner.StopWave();

			dungeonRecorder.CaptureResultRecord();

			IsDungeon = false;

			dungeonUI.SetPanel(DungeonPanelType.DungeonResult);
		}

		public void Continue()
		{
			// 집으로 돌아가기
			uiManager.Transition.Transition(
				aDuringTransition: () =>
				{
					stageManager.LoadStage(stageManager.LastStage, isBackToLastStage: true);
					ResetDungeonAndPlayer();
				},
				aWhenEnd: () =>
				{
					gameEventManager.Raise(GameEventType.OnDungeonReturn);
				}).Forget();

			void ResetDungeonAndPlayer()
			{
				dungeonUI.ClosePanel();
				cameraManager.SetContentCameraMode(ContentCameraMode.Normal);

				gameManager.Init();
				expChecker.Init();
				cardManager.Reset();
			}
		}
	}
}