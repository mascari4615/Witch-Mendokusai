using System;
using System.Collections;
using PlayFab.EconomyModels;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class DungeonManager : Singleton<DungeonManager>
	{
		public Dungeon CurDungeon { get; private set; }
		public DungeonContext Context { get; private set; }
		public DungeonRecord Result { get; private set; }

		public bool IsDungeon { get; private set; }

		private CardManager cardManager;
		private MonsterSpawner monsterSpawner;
		private ExpManager expChecker;
		private DungeonRecorder dungeonRecorder;

		protected override void Awake()
		{
			base.Awake();

			cardManager = FindFirstObjectByType<CardManager>(FindObjectsInactive.Include);
			monsterSpawner = FindFirstObjectByType<MonsterSpawner>(FindObjectsInactive.Include);

			expChecker = FindFirstObjectByType<ExpManager>(FindObjectsInactive.Include);
		}

		private void Start()
		{
			// 당장 게임 이벤트 변화가 많아서, 인스펙터에서 GameEventListener 넣는 것보다, 이렇게 하드 코딩하는게 나은 듯
			GameEventManager.Instance.RegisterCallback(GameEventType.OnPlayerDied, EndDungeon);
		}

		// TODO: 던전 인트로
		public void CombatIntro()
		{
		}

		public void StartDungeon(Dungeon dungeon)
		{
			Debug.Log($"{nameof(StartDungeon)}");

			CurDungeon = dungeon;

			// TODO: 던전
			StageManager.Instance.LoadStage(dungeon.Stages[0], action: InitDungeonAndPlayer);

			void InitDungeonAndPlayer()
			{
				GameManager.Instance.Init();
				GameManager.Instance.InitEquipment();
				expChecker.Init();
				cardManager.Init();

				Context = new DungeonContext
				(
					initialDungeonTime: new TimeSpan(0, 0, dungeon.TimeBySecond),
					constraints: dungeon.Constraints
				);

				dungeonRecorder = new DungeonRecorder();

				IsDungeon = true;

				CreateDungeonQuest(dungeon);

				monsterSpawner.transform.position = Player.Instance.transform.position;
				monsterSpawner.InitWaves(dungeon);

				StartCoroutine(DungeonLoop());

				// Context 생성 이후 UI 설정
				// UIDungeon.UpdateUI(); 에서 Context를 사용합니다.
				UIManager.Instance.SetOverlay(MPanelType.None);
				UIManager.Instance.SetCanvas(MCanvasType.Dungeon);
		
				GameEventManager.Instance.Raise(GameEventType.OnDungeonStart);
			}
		}

		private IEnumerator DungeonLoop()
		{
			Debug.Log(nameof(DungeonLoop));
			WaitForSeconds ws01 = new(.1f);

			// HACK:
			int dungeonClear = DataManager.Instance.DungeonStat[DungeonStatType.DUNGEON_CLEAR];
			Debug.Log($"DungeonClear: {dungeonClear}");

			while (true)
			{
				Context.UpdateTime();
				Context.UpdateDifficulty();
				monsterSpawner.UpdateWaves();

				// Debug.Log($"DungeonClear: {dungeonClear}");
				if (dungeonClear < DataManager.Instance.DungeonStat[DungeonStatType.DUNGEON_CLEAR])
				{
					EndDungeon();
					yield break;
				}

				yield return ws01;
			}
		}

		public void EndDungeon()
		{
			Debug.Log($"{nameof(EndDungeon)}");

			// Stop DungeonLoop
			StopAllCoroutines();
			monsterSpawner.StopWave();

			Result = dungeonRecorder.GetResultRecord();

			IsDungeon = false;

			UIManager.Instance.SetOverlay(MPanelType.DungeonResult);
		}

		public void Continue()
		{
			// 집으로 돌아가기
			StageManager.Instance.LoadStage
			(
				StageManager.Instance.LastStage,
				isBackToLastStage: true,
				action: ResetDungeonAndPlayer
			);

			void ResetDungeonAndPlayer()
			{
				UIManager.Instance.SetOverlay(MPanelType.None);
				UIManager.Instance.SetCanvas(MCanvasType.None);

				GameManager.Instance.Init();
				expChecker.Init();
				cardManager.ClearCardEffect();
			}
		}

		private void CreateDungeonQuest(Dungeon dungeon)
		{
			// TEST:
			QuestInfo questInfo = new()
			{
				Type = QuestType.Dungeon,
				GameEvents = new()
				{
					GameEventType.OnTick,
				},
				Criteria = new(),
				CompleteEffects = new()
				{
					// HACK:
					new EffectInfo()
					{
						Type = EffectType.DungeonStat,
						Data = GetDungeonStatData(DungeonStatType.DUNGEON_CLEAR),
						ArithmeticOperator = ArithmeticOperator.Add,
						Value = 1,
					}
				},
				RewardEffects = new(),
				Rewards = dungeon.Rewards,

				WorkTime = 0,
				AutoWork = false,
				AutoComplete = true,
			};

			string questName = string.Empty;

			DungeonRecord curRecord = dungeonRecorder.GetResultRecord();
			DungeonType dungeonType = dungeon.Type;

			switch (dungeonType)
			{
				case DungeonType.TimeSurvival:
					questName = "시간 동안 생존";
					questInfo.Criteria = new()
					{
						new CriteriaInfo()
						{
							Type = CriteriaType.DungeonStat,
							Data = GetDungeonStatData(DungeonStatType.DUNGEON_TIME),
							ComparisonOperator = ComparisonOperator.GreaterThanOrEqualTo,
							Value = (int)Context.InitialDungeonTime.TotalSeconds,
							JustOnce = true,
						}
					};
					break;
				case DungeonType.Domination:
					// TODO:
					break;
				case DungeonType.KillCount:
					questName = "몬스터 처치";
					questInfo.Criteria = new()
					{
						new CriteriaInfo()
						{
							Type = CriteriaType.DungeonStat,
							Data = GetDungeonStatData(DungeonStatType.MONSTER_KILL),
							ComparisonOperator = ComparisonOperator.GreaterThanOrEqualTo,
							Value = CurDungeon.ClearValue,
							JustOnce = true,
						}
					};
					break;
				case DungeonType.Boss:
					questName = "보스 처치";
					questInfo.Criteria = new()
					{
						new CriteriaInfo()
						{
							Type = CriteriaType.DungeonStat,
							Data = GetDungeonStatData(DungeonStatType.BOSS_KILL),
							ComparisonOperator = ComparisonOperator.GreaterThanOrEqualTo,
							Value = CurDungeon.ClearValue,
							JustOnce = true,
						}
					};
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			RuntimeQuest runtimeQuest = new(questInfo, questName);
			QuestManager.Instance.AddQuest(runtimeQuest);
		}
	}
}