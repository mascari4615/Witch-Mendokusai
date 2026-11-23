using System;
using System.Collections.Generic;
using System.Linq;
using KarmoLabs;
using UnityEngine;
using VContainer;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public enum SaveLoadOutcome
	{
		/// <summary> 세이브가 없어 새 게임. 알릴 것 없음 </summary>
		FirstRun = 0,
		Loaded = 1,
		/// <summary> 본 파일이 깨져 앞 판으로 불러옴 </summary>
		RestoredFromBackup = 2,
		/// <summary> 세이브가 있었는데 못 읽어 새 게임으로 시작 </summary>
		ResetUnreadable = 3,
		/// <summary> 다른 프로세스가 같은 세이브를 쓰는 중. 불러오기는 했지만 이번 실행은 저장하지 않음 </summary>
		ReadOnlyOtherOwner = 4,
	}

	public class SaveManager
	{
		public const string SAVE_FILE_NAME = "WM.json";
		// 저장 형식 번호. 형식을 바꿀 때 올리고, 옛 번호를 읽는 이주를 LoadData 에 둠. 0 은 이 칸이 생기기 전 세이브
		public const int SAVE_VERSION = 1;

		public bool IsDataLoaded { get; private set; }
		/// <summary> 마지막 불러오기 결과. 로비가 읽어 복구나 새 게임 시작을 알림 </summary>
		public SaveLoadOutcome LastLoadOutcome { get; private set; }
		/// <summary> 다른 프로세스가 세이브의 주인이라 이번 실행은 저장하지 않음 </summary>
		public bool IsReadOnly { get; private set; }

		// 프로세스에 하나. 플레이어는 꺼질 때 OS 가 놓고, 에디터는 플레이 모드가 끝나면 놓음
		private static SaveOwnerLock heldOwnerLock;
		private bool isLockUnavailable;

		private SOManager soManager;
		// DataManager → SaveManager → DataManager 순환 — 소유자 push (TASK-WM-120 γ).
		// WM-078 γ P3-K 의 lazy `DataManager.Instance` 폐기: DataManager 가 이미
		// SaveManager 를 DI 로 받아 소유(Construct) → WM-107 의 QuestManager·
		// IEffectRunner 와 동일한 BindDataManager 소유자-push 로 통일 (static
		// back-ref 제거, [Inject] pull X — 생성자 순환 미도입).
		private DataManager DataManager;
		public void BindDataManager(DataManager dataManager) => DataManager = dataManager;

		private AchievementManager achievementManager;

		[Inject]
		public void Construct(SOManager soManager, AchievementManager achievementManager)
		{
			this.soManager = soManager;
			this.achievementManager = achievementManager;
		}

		public void CreateNewGameData()
		{
			GameData newGameData = new()
			{
				curDollIndex = 0,
				dummyDollCount = 1,
				inventoryItems = new(),
				hotbarItems = new(),
				dolls = new(),
				works = new()
				{
					{ WorkListType.DollWork, new() },
					{ WorkListType.DummyWork, new() },
					{ WorkListType.VQuestWork, new() }
				},
				questStates = new(),
				hasRecipe = new(),
				hasSpecimen = new(),
				runtimeQuests = new(),
				gameStats = new(),
				dungeons = new(),
				worldStages = new(),
				upgrades = new(),
				windowLayouts = new()
			};

			// 인형, 인형 아이템(장비) 초기화
			{
				Inventory inventory = soManager.ItemInventory;
				inventory.Load(newGameData.inventoryItems);

				ForEach<Doll>(doll =>
				{
					// newGameData.dolls.Add(doll.Save());
					InitDoll(doll.ID);
				});

				void InitDoll(int dollID)
				{
					Doll doll = GetDoll(dollID);
					doll.Load(doll.CreateDefaultSaveData());
					newGameData.dolls.Add(doll.Save());
				}

				newGameData.inventoryItems = inventory.Save();
			}

			// 레시피 초기화
			// 모든 아이템 ID에 대해 bool
			DataManager.IsRecipeUnlocked = soManager.DataSOs[typeof(ItemData)].Values.ToDictionary(itemData => itemData.ID, itemData => false);

			// 마도 온실 표본(TASK-WM-167) 초기화 — 새 게임 = 채집 0(빈 dict, 봐준 게 없음). 작물 .asset 없어도 안전.
			DataManager.SpecimenCollected = new();

			// 갈래 조각 초기화. 새 게임은 기록 없음
			foreach (IFeatureSaveSlice slice in DataManager.FeatureSaves)
			{
				slice.Reset();
			}

			// 퀘스트 상태 초기화 이후 저장
			Dictionary<int, QuestState> questStates = new();
			ForEach<QuestSO>(questData => questStates.Add(questData.ID, QuestState.Locked));
			DataManager.QuestManager.LoadQuestState(questStates);

			// 초기 퀘스트 추가 — 정사 챕터 첫 노드만. prototype 챕터 (Q_6000+) 는 별도 trigger (dev menu / NPC / 챕터 진입 이벤트) 로 unlock.
			DataManager.QuestManager.Init(new());
			DataManager.QuestManager.UnlockQuest(GetQuestSO(5000));

			// 던전 초기화
			ForEach<Dungeon>(dungeon => { dungeon.Init(); });

			// 통계 초기화
			DataManager.GameStat.Init();
			DataManager.GameStat[GameStatType.NYANG] = 1000;

			// 업그레이드 초기화
			ForEach<UpgradeData>(upgradeData => { upgradeData.CurLevel = 0; });

			// 주인이 아니면 파일은 그대로 두고 메모리에만 새 게임을 올림. 남의 세이브를 지우지 않음
			if (EnsureOwner() == false)
			{
				// 파일을 거치는 주인 경로와 같게 직렬화 왕복으로 떼어 냄. 살아 있는 참조를 그대로 넘기면
				// Load 가 자기 사전을 제자리에서 비우고 다시 읽어 통계가 기본값이 됨 (GameStat.Save 가 내부 사전을 돌려줌)
				LoadData(SaveTool.CloneThroughJson(CaptureGameData()));
				return;
			}

			// 새 게임은 앞 판도 비움. 남기면 지운 진행이 앞 판으로 되살아남
			SaveTool.DeleteSaveFile(SAVE_FILE_NAME);
			WriteSaveData();
			LoadLocalData();
		}

		public void LoadLocalData()
		{
			bool isOwner = EnsureOwner();
			// 주인이 아니면 못 읽은 파일도 옮기지 않음. 그 파일은 주인 것
			if (SaveTool.TryLoadFile(SAVE_FILE_NAME, out GameData gameData, out SaveReadSource source, quarantineUnreadable: isOwner))
			{
				LoadData(gameData);
				LastLoadOutcome = source == SaveReadSource.Backup ? SaveLoadOutcome.RestoredFromBackup : SaveLoadOutcome.Loaded;
				if (isOwner == false)
				{
					LastLoadOutcome = SaveLoadOutcome.ReadOnlyOtherOwner;
					return;
				}
				// 앞 판에서 읽었으면 바로 본 파일로 다시 씀. 안 쓰고 꺼지면 다음 실행에도 같은 알림이 뜸
				if (LastLoadOutcome == SaveLoadOutcome.RestoredFromBackup)
					SaveData();
			}
			else
			{
				// 새 게임 만들기가 안에서 다시 불러오며 Loaded 로 덮으므로 끝난 뒤에 적음
				CreateNewGameData();
				LastLoadOutcome = source == SaveReadSource.Unreadable ? SaveLoadOutcome.ResetUnreadable : SaveLoadOutcome.FirstRun;
				if (isOwner == false)
					LastLoadOutcome = SaveLoadOutcome.ReadOnlyOtherOwner;
			}
		}

		/// <summary>
		/// 이 프로세스가 세이브의 주인인지. 처음 부를 때 잠금을 잡고, 못 잡으면 이번 실행 내내 읽기만 함
		/// 다시 시도하지 않음. 실행 도중 주인이 되면 그사이 남이 쓴 파일을 옛 메모리로 덮게 됨
		/// </summary>
		private bool EnsureOwner()
		{
			if (heldOwnerLock is { IsHeld: true } || isLockUnavailable)
				return true;
			if (IsReadOnly)
				return false;

			SaveOwnerLockResult result = SaveOwnerLock.TryAcquire(SaveTool.GetSaveFilePath(SAVE_FILE_NAME), out heldOwnerLock);
			if (result == SaveOwnerLockResult.Acquired)
				return true;
			// 잠금 파일을 못 다루는 환경은 주인을 가릴 수 없음. 저장을 막으면 진행이 다 사라지므로 잠금 없이 예전처럼 씀
			if (result == SaveOwnerLockResult.Unavailable)
			{
				isLockUnavailable = true;
				return true;
			}

			IsReadOnly = true;
			Debug.LogError($"[{nameof(SaveManager)}] 다른 WM 이 이 세이브를 쓰는 중이라 이번 실행은 저장하지 않음: {SaveTool.GetSaveFilePath(SAVE_FILE_NAME)}");
			return false;
		}

#if UNITY_EDITOR
		// 에디터는 플레이 모드를 끝내도 프로세스가 남음. 잠금을 놓아야 플레이어나 다음 플레이 모드가 주인이 됨
		// 모든 OnApplicationQuit 저장이 끝난 뒤인 EnteredEditMode 에서 놓음
		[UnityEditor.InitializeOnLoadMethod]
		private static void ReleaseOwnerLockAfterPlay()
		{
			UnityEditor.EditorApplication.playModeStateChanged += playModeStateChange =>
			{
				if (playModeStateChange != UnityEditor.PlayModeStateChange.EnteredEditMode)
					return;
				heldOwnerLock?.Dispose();
				heldOwnerLock = null;
			};
		}
#endif

		public void LoadData(GameData saveData)
		{
			// 불러오다 터지면 반쯤 채운 상태로 저장하지 않게 먼저 내림
			IsDataLoaded = false;

			if (saveData.saveVersion > SAVE_VERSION)
				Debug.LogWarning($"[{nameof(SaveManager)}] 이 빌드보다 새 형식의 세이브: {saveData.saveVersion} > {SAVE_VERSION}");

			DataManager.CurDollID = saveData.curDollIndex;
			DataManager.DummyDollCount = saveData.dummyDollCount;

			// 통계 초기화
			DataManager.GameStat.Load(saveData.gameStats);

			// 아이템 초기화
			soManager.ItemInventory.Load(saveData.inventoryItems);
			soManager.Hotbar.Load(saveData.hotbarItems);

			// 인형 초기화
			soManager.DollBuffer.Clear();
			foreach (DollSaveData dollData in saveData.dolls)
			{
				GetDoll(dollData.DollID).Load(dollData);
				soManager.DollBuffer.Add(GetDoll(dollData.DollID));
			}
			// 세이브 이후 추가된 인형은 기본값
			HashSet<int> savedDollIds = new(saveData.dolls.Select(dollData => dollData.DollID));
			ForEach<Doll>(doll =>
			{
				if (savedDollIds.Contains(doll.ID))
					return;
				doll.Load(doll.CreateDefaultSaveData());
				soManager.DollBuffer.Add(doll);
			});
			for (int i = 0; i < saveData.dummyDollCount - 1; i++)
				soManager.DollBuffer.Add(GetDoll(Doll.DUMMY_ID));

			// 퀘스트 초기화
			Dictionary<int, QuestState> questStates = new();
			foreach ((int id, int state) in saveData.questStates)
			{
				questStates.Add(id, (QuestState)state);
				if ((QuestState)state >= QuestState.Unlocked)
					soManager.QuestDataBuffer.Add(GetQuestSO(id));
			}
			// 세이브 이후 추가된 신규 퀘스트는 Locked로 등록
			ForEach<QuestSO>(questData => { if (questStates.ContainsKey(questData.ID) == false) questStates.Add(questData.ID, QuestState.Locked); });
			DataManager.QuestManager.LoadQuestState(questStates);

			// 레시피 초기화
			DataManager.IsRecipeUnlocked = saveData.hasRecipe;

			// 마도 온실 표본(TASK-WM-167) — 옛 세이브엔 필드 부재 → null 가드(빈 dict).
			DataManager.SpecimenCollected = saveData.hasSpecimen ?? new();

			// 갈래 조각. 조각이 없는 옛 세이브는 갈래가 옛 필드를 읽음
			foreach (IFeatureSaveSlice slice in DataManager.FeatureSaves)
			{
				slice.Reset();
				if (saveData.featureSaves != null && saveData.featureSaves.TryGetValue(slice.Key, out string json))
				{
					slice.Restore(json);
				}
				else
				{
					slice.RestoreLegacy(saveData);
				}
			}

			// 작업 초기화
			DataManager.WorkManager.Init(saveData.works);
			CriteriaContext criteriaContext = DataManager.QuestManager.CreateCriteriaContext();
			List<RuntimeQuestSaveData> savedQuests = ModQuestInstaller.NormalizeSavedQuests(saveData.runtimeQuests, ModLoader.Content.HiddenQuestIds);
			// 업적이 퀘스트였던 시절 진행 중이던 것은 저장된 타입이 Achievement. 자산은 일반으로 바뀌었으므로 맞춤 (업적은 AchievementManager 가 따로)
			for (int i = 0; i < savedQuests.Count; i++)
			{
				if (savedQuests[i].Type == QuestType.Achievement)
				{
					RuntimeQuestSaveData normalized = savedQuests[i];
					normalized.Type = QuestType.Normal;
					savedQuests[i] = normalized;
				}
			}
			if (savedQuests.Count < saveData.runtimeQuests.Count)
			{
				Debug.Log($"[SaveManager] 중복 모드 퀘스트 정리: {saveData.runtimeQuests.Count} -> {savedQuests.Count}");
			}
			DataManager.QuestManager.Init(savedQuests.ConvertAll((RuntimeQuestSaveData questSaveData) => RuntimeQuestFactory.FromSaveData(questSaveData, criteriaContext)));

			// 업적 판정 시작. 조각과 통계가 다 들어온 뒤라 첫 판정이 맞음
			achievementManager.Begin(DataManager.FeatureSave<AchievementSaveSlice>(), criteriaContext);

			// 던전 초기화
			ForEach<Dungeon>(dungeon => { dungeon.Init(); }); // 먼저 전부 Init (새로 추가된 던전 포함)
			foreach ((int id, DungeonSaveData dungeonData) in saveData.dungeons)
			{
				if (TryGetSaved(id, out Dungeon dungeon))
					dungeon.Load(dungeonData);
			}
			// 저장된 최고 기록을 기록판에 알림 (앞선 실행에서 플랫폼에 못 올린 기록 포함)
			ForEach<Dungeon>(dungeon => { dungeon.ReportRecords(); });

			// WorldStage 초기화
			foreach ((int worldStageID, WorldStageSaveData worldStageData) in saveData.worldStages)
			{
				if (TryGetSaved(worldStageID, out WorldStage worldStage))
					worldStage.Load(worldStageData);
			}

			// 업그레이드 초기화
			foreach ((int upgradeID, UpgradeSaveData upgradeData) in saveData.upgrades)
			{
				if (TryGetSaved(upgradeID, out UpgradeData upgrade))
					upgrade.Load(upgradeData);
			}

			// 대화 이력 복원. 로비에서 불러오면 대화 시스템이 아직 없어 보관했다가 월드의 대화 시스템 등록 때 넘김
			DialogueHistoryBridge.RestoreOrHold(saveData.dialogueHistory);

			// 윈도우 레이아웃 복원
			soManager.WindowLayoutData.Load(saveData.windowLayouts);

			IsDataLoaded = true;
		}

		// 세이브에는 있는데 이 빌드에 없는 자료 (지웠거나 번호를 바꾼 것) 는 건너뜀. 저장 파일은 바깥 입력이라 읽는 경계에서만 관대
		private static bool TryGetSaved<T>(int id, out T data) where T : DataSO
		{
			if (TryGet(id, out data))
				return true;
			Debug.LogWarning($"[{nameof(SaveManager)}] 세이브의 {typeof(T).Name} {id} 가 이 빌드에 없어 건너뜀");
			return false;
		}

		private Dictionary<string, string> CaptureFeatureSaves()
		{
			Dictionary<string, string> captured = new();
			foreach (IFeatureSaveSlice slice in DataManager.FeatureSaves)
			{
				captured[slice.Key] = slice.Capture();
			}
			return captured;
		}

		public void SaveData()
		{
			// 로드 전의 빈 메모리로 저장 파일을 덮지 않음
			if (IsDataLoaded == false)
			{
				Debug.LogWarning("데이터 로드 전이라 저장 생략");
				return;
			}

			// 주인이 아니면 쓰지 않음. 쓰면 주인이 그사이 저장한 진행을 이 메모리로 되돌림
			if (EnsureOwner() == false)
				return;

			WriteSaveData();
		}

		private void WriteSaveData() => SaveTool.SaveFile(SAVE_FILE_NAME, CaptureGameData());

		private GameData CaptureGameData()
		{
			GameData gameData = new()
			{
				saveVersion = SAVE_VERSION,
				curDollIndex = DataManager.CurDollID,
				dummyDollCount = DataManager.DummyDollCount,
				inventoryItems = soManager.ItemInventory.Save(),
				hotbarItems = soManager.Hotbar.Save(),
				dolls = new(),
				works = DataManager.WorkManager.Works,
				questStates = DataManager.QuestManager.GetQuestStates().ToDictionary(pair => pair.Key, pair => (int)pair.Value),
				hasRecipe = DataManager.IsRecipeUnlocked,
				hasSpecimen = DataManager.SpecimenCollected,
				featureSaves = CaptureFeatureSaves(),
				runtimeQuests = DataManager.QuestManager.Quests.Data.Where(quest => quest.Type != QuestType.Dungeon).ToList().ConvertAll(quest => quest.Save()),
				gameStats = DataManager.GameStat.Save(),
				dungeons = new(),
				worldStages = new(),
				upgrades = new()
			};

			ForEach<Doll>(doll => gameData.dolls.Add(doll.Save()));
			ForEach<Dungeon>(dungeon => gameData.dungeons.Add(dungeon.ID, dungeon.Save()));
			ForEach<WorldStage>(worldStage => gameData.worldStages.Add(worldStage.ID, worldStage.Save()));
			ForEach<UpgradeData>(upgradeData => gameData.upgrades.Add(upgradeData.ID, upgradeData.Save()));

			gameData.dialogueHistory = DialogueHistoryBridge.CaptureSaveData();
			gameData.windowLayouts = soManager.WindowLayoutData.Save();

			return gameData;
		}
	}
}