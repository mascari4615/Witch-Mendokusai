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

		private SOManager soManager;
		// DataManager → SaveManager → DataManager 순환 — 소유자 push (TASK-WM-120 γ).
		// WM-078 γ P3-K 의 lazy `DataManager.Instance` 폐기: DataManager 가 이미
		// SaveManager 를 DI 로 받아 소유(Construct) → WM-107 의 QuestManager·
		// IEffectRunner 와 동일한 BindDataManager 소유자-push 로 통일 (static
		// back-ref 제거, [Inject] pull X — 생성자 순환 미도입).
		private DataManager DataManager;
		public void BindDataManager(DataManager dataManager) => DataManager = dataManager;

		private AchievementManager achievementManager;
		private MonsterLogManager monsterLogManager;

		[Inject]
		public void Construct(SOManager soManager, AchievementManager achievementManager, MonsterLogManager monsterLogManager)
		{
			this.soManager = soManager;
			this.achievementManager = achievementManager;
			this.monsterLogManager = monsterLogManager;
		}

		/// <summary>
		/// 세이브 초기화. 저장 파일, 내 안의 세계 (world.json), 파고 심은 지형을 모두 새 게임으로. 로비에서만
		/// 다른 프로세스가 주인이면 파일은 두고 메모리만 새 게임
		/// </summary>
		public void ResetToNewGame()
		{
			bool isOwner = EnsureOwner();
			// 저장 파일과 세계의 주인이 다를 수 있음 (먼저 켠 창은 로비, 나중 창이 세계). 반만 지우지 않게 손대기 전에 봄
			if (isOwner && WitchMendokusai.Net.WorldDoor.CanResetSolo == false)
				throw new InvalidOperationException($"{nameof(ResetToNewGame)}: 내 안의 세계를 지울 수 없음 (세계에 들어가 있거나 다른 프로세스가 쓰는 중). 아무것도 안 지움");

			StartNewGame();
			// 세계와 지형은 새 게임이 선 뒤에. 먼저 지우면 새 게임을 못 만들었을 때 옛 진행에 세계만 사라짐. 여기서 터지면 다시 누르면 됨
			if (isOwner)
			{
				WitchMendokusai.Net.WorldDoor.ResetSolo();
				ChunkStorage.DeleteAll(Application.persistentDataPath);
			}
			LastLoadOutcome = IsReadOnly ? SaveLoadOutcome.ReadOnlyOtherOwner : SaveLoadOutcome.Loaded;
		}

		/// <summary>
		/// 새 게임. 빈 자료를 불러오는 것이라 불러오기와 같은 길을 탐. 살아 있는 메모리를 떠서 쓰지 않으므로 앞 게임 값이 섞이지 않음
		/// 파일을 먼저 씀. 못 쓰면 메모리도 그대로, 쓴 뒤 터지면 다음 실행은 새 게임
		/// </summary>
		private void StartNewGame()
		{
			GameData newGame = NewGameData.Create(soManager.WindowLayoutData.Save());
			// 주인이 아니면 파일은 그대로 두고 메모리에만 새 게임을 올림. 남의 세이브를 지우지 않음
			bool isOwner = EnsureOwner();
			if (isOwner)
			{
				// 앞 판도 비움. 남기면 지운 진행이 앞 판으로 되살아남
				SaveTool.DeleteSaveFile(SAVE_FILE_NAME);
				SaveTool.SaveFile(SAVE_FILE_NAME, newGame);
			}

			// 파일을 거친 것과 같게 사본으로 읽음. 그대로 넘기면 자료와 메모리가 컬렉션을 나눠 가짐
			LoadData(SaveTool.CloneThroughJson(newGame));
			// 불러오며 연 첫 퀘스트들까지 저장
			SaveData();
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
				// 내 안의 세계와 지형은 안 지움. 저장 파일만 잃었을 수 있음
				StartNewGame();
				LastLoadOutcome = source == SaveReadSource.Unreadable ? SaveLoadOutcome.ResetUnreadable : SaveLoadOutcome.FirstRun;
				if (isOwner == false)
					LastLoadOutcome = SaveLoadOutcome.ReadOnlyOtherOwner;
			}
		}

		/// <summary>
		/// 이 프로세스가 세이브의 주인인지. 처음 부를 때 잠금을 잡고, 못 잡으면 이번 실행 내내 읽기만 함 (SaveOwnerLock.ClaimForProcess)
		/// 잠금 파일을 못 다루는 환경은 주인을 가릴 수 없어 잠금 없이 예전처럼 씀
		/// </summary>
		private bool EnsureOwner()
		{
			bool isOwner = SaveOwnerLock.ClaimForProcess(SaveTool.GetSaveFilePath(SAVE_FILE_NAME), out SaveOwnerLockResult _);
			IsReadOnly = isOwner == false;
			return isOwner;
		}

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
			soManager.QuestDataBuffer.Clear();
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
			// 작업 초기화. 퀘스트를 만들면 자동 작업이 여기에 더해지므로 먼저
			DataManager.WorkManager.Init(saveData.works, new HashSet<Guid?>(savedQuests.Select(quest => quest.Guid)));
			DataManager.QuestManager.Init(savedQuests.ConvertAll((RuntimeQuestSaveData questSaveData) => RuntimeQuestFactory.FromSaveData(questSaveData, criteriaContext)));
			// 새 게임은 첫 퀘스트가 잠겨 있음. 불러올 때 엶
			DataManager.QuestManager.UnlockFirstStoryQuest();
			// 마도서 페이지가 생기기 전에 시작한 세이브는 첫 페이지가 잠겨 있음. 불러올 때 한 번 열어 줌
			DataManager.QuestManager.StartMagicBook(GetQuestSO(QuestManager.FIRST_MAGIC_BOOK_PAGE_ID));

			// 업적 판정 시작. 조각과 통계가 다 들어온 뒤라 첫 판정이 맞음
			achievementManager.Begin(DataManager.FeatureSave<AchievementSaveSlice>(), criteriaContext);
			monsterLogManager.Begin(DataManager.FeatureSave<MonsterLogSaveSlice>());

			// 던전 초기화
			ForEach<Dungeon>(dungeon => { dungeon.Init(); }); // 먼저 전부 Init (새로 추가된 던전 포함)
			foreach ((int id, DungeonSaveData dungeonData) in saveData.dungeons)
			{
				if (TryGetSaved(id, out Dungeon dungeon))
					dungeon.Load(dungeonData);
			}
			// 저장된 최고 기록을 기록판에 알림 (앞선 실행에서 플랫폼에 못 올린 기록 포함)
			ForEach<Dungeon>(dungeon => { dungeon.ReportRecords(); });

			// WorldStage 초기화. 먼저 전부 비움 (세이브에 없는 스테이지에 앞 게임 것이 남지 않게)
			ForEach<WorldStage>(worldStage => worldStage.Load(default));
			foreach ((int worldStageID, WorldStageSaveData worldStageData) in saveData.worldStages)
			{
				if (TryGetSaved(worldStageID, out WorldStage worldStage))
					worldStage.Load(worldStageData);
			}

			// 업그레이드 초기화. 먼저 전부 비움
			ForEach<UpgradeData>(upgradeData => upgradeData.Load(default));
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