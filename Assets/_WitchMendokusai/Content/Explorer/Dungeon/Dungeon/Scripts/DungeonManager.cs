using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace WitchMendokusai
{
	public class DungeonManager : Singleton<DungeonManager>
	{
		public Dungeon CurDungeon { get; private set; }
		public DungeonContext Context { get; private set; }
		public DungeonRecord Result => dungeonRecorder.CaptureResultRecord();

		public bool IsDungeon { get; private set; }

		[SerializeField] private CardManager cardManager;
		[SerializeField] private MonsterSpawner monsterSpawner;
		[SerializeField] private ExpManager expChecker;

		private UIDungeon dungeonUI = null;

		private DungeonRecorder dungeonRecorder = null;
		private DungeonObjectiveStrategy dungeonStrategy = null;
		private IDisposable dungeonLoopSubscription;

		protected override void Awake()
		{
			base.Awake();

			dungeonUI = FindFirstObjectByType<UIDungeon>(FindObjectsInactive.Include);
		}

		private void Start()
		{
			// 당장 게임 이벤트 변화가 많아서, 인스펙터에서 GameEventListener 넣는 것보다, 이렇게 하드 코딩하는게 나은 듯
			GameEventManager.Instance.RegisterCallback(GameEventType.OnPlayerDied, EndDungeon);
		}

		public void StartDungeon(Dungeon dungeon)
		{
			Debug.Log($"{nameof(StartDungeon)} ({dungeon.Name})");

			CurDungeon = dungeon;
			dungeonStrategy = DungeonObjectiveStrategyFactory.Create(dungeon);

			Stage stage = dungeon.Stages[0];

			// TODO: 던전 Transition
			UIManager.Instance.Transition.Transition(
				aDuringTransition: () =>
				{
					StageManager.Instance.LoadStage(stage);
					InitDungeonAndPlayer();
				},
				aWhenEnd: () =>
				{
					UIManager.Instance.StagePopup(stage);
					// TODO: 던전 Intro?
				}).Forget();

			void InitDungeonAndPlayer()
			{
				GameManager.Instance.Init();
				GameManager.Instance.InitEquipment();
				expChecker.Init();
				cardManager.Reset();

				Context = new DungeonContext
				(
					initialDungeonTime: new TimeSpan(0, 0, dungeon.TimeBySecond),
					constraints: dungeon.Constraints
				);

				dungeonRecorder = new DungeonRecorder();

				IsDungeon = true;

				// Create Dungeon Quest
				{
					RuntimeQuest runtimeQuest = dungeonStrategy.CreateRuntimeQuest(dungeon);
					QuestManager.Instance.AddQuest(runtimeQuest);
				}

				monsterSpawner.transform.position = Player.Instance.transform.position;
				monsterSpawner.InitWaves(dungeon);

				// StartDungeonLoop();
				{
					// RuntimeQuest를 통해 DungeonClear 수치가 1 오르면 던전 종료
					int targetClearCount = DataManager.Instance.DungeonStat[DungeonStatType.DUNGEON_CLEAR] + 1;
					bool IsClear()
					{
						int curClearCount = DataManager.Instance.DungeonStat[DungeonStatType.DUNGEON_CLEAR];
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
				// UIDungeon.UpdateUI(); 에서 Context를 사용합니다.
				dungeonUI.SetPanel(DungeonPanelType.None);
				dungeonUI.StartLoop();

				GameEventManager.Instance.Raise(GameEventType.OnDungeonStart);
			}
		}

		public void EndDungeon()
		{
			Debug.Log($"{nameof(EndDungeon)}");

			// Stop DungeonLoop
			if (dungeonLoopSubscription != null)
			{
				dungeonLoopSubscription.Dispose();
				dungeonLoopSubscription = null;
			}
			monsterSpawner.StopWave();

			dungeonRecorder.CaptureResultRecord();

			IsDungeon = false;

			dungeonUI.SetPanel(DungeonPanelType.DungeonResult);
		}

		public void Continue()
		{
			// 집으로 돌아가기
			StageManager.Instance.LoadStage
			(
				StageManager.Instance.LastStage,
				isBackToLastStage: true
			);

			ResetDungeonAndPlayer();

			void ResetDungeonAndPlayer()
			{
				dungeonUI.StopLoop();
				dungeonUI.ClosePanel();
				CameraManager.Instance.SetCamera(CameraType.Normal);

				GameManager.Instance.Init();
				expChecker.Init();
				cardManager.Reset();
			}
		}
	}
}