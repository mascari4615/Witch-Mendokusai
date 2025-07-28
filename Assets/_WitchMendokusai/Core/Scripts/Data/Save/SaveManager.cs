using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class SaveManager
	{
		public bool IsDataLoaded { get; private set; }

		private SOManager SOManager => SOManager.Instance;
		private DataManager DataManager => DataManager.Instance;

		public void CreateNewGameData()
		{
			GameData newGameData = new()
			{
				curDollIndex = 0,
				dummyDollCount = 1,
				inventoryItems = new(),
				dolls = new(),
				works = new()
				{
					{ WorkListType.DollWork, new() },
					{ WorkListType.DummyWork, new() },
					{ WorkListType.VQuestWork, new() }
				},
				questStates = new(),
				hasRecipe = new(),
				runtimeQuests = new(),
				gameStats = new(),
				dungeons = new(),
				worldStages = new()
			};

			// 인형, 인형 아이템(장비) 초기화
			{
				Inventory inventory = SOManager.ItemInventory;
				inventory.Load(newGameData.inventoryItems);

				ForEach<Doll>(doll =>
				{
					// newGameData.dolls.Add(doll.Save());
					InitDoll(doll.ID);
				});

				void InitDoll(int dollID)
				{
					Doll doll = GetDoll(dollID);
					DollSaveData newDollData = new()
					{
						DollID = doll.ID,
						Level = 1,
						Exp = 0,
						EquipmentGuids = new()
					};

					foreach (EquipmentData equipmentData in doll.DefaultEquipments)
					{
						inventory.Add(equipmentData);
						Guid? guid = inventory.GetItem(inventory.FindItemIndex(equipmentData)).Guid;
						newDollData.EquipmentGuids.Add(guid);
					}

					doll.Load(newDollData);
					newGameData.dolls.Add(doll.Save());
				}
				newGameData.inventoryItems = inventory.Save();
			}

			// 레시피 초기화
			// 모든 아이템 ID에 대해 bool
			DataManager.IsRecipeUnlocked = SOManager.DataSOs[typeof(ItemData)].Values.ToDictionary(itemData => itemData.ID, itemData => false);

			// 퀘스트 상태 초기화 이후 저장
			Dictionary<int, QuestState> questStates = new();
			ForEach<QuestSO>(questData => questStates.Add(questData.ID, QuestState.Locked));
			DataManager.QuestManager.LoadQuestState(questStates);

			// 초기 퀘스트 추가
			DataManager.QuestManager.Init(new());
			// DataManager.QuestManager.AddQuest(new RuntimeQuest(GetQuestSO(0)));
			new RuntimeQuest(GetQuestSO(0));

			// 던전 초기화
			ForEach<Dungeon>(dungeon => { dungeon.Init(); });

			// 통계 초기화
			DataManager.GameStat.Init();
			DataManager.GameStat[GameStatType.NYANG] = 1000;

			SaveData();
			LoadLocalData();
		}

		public void LoadLocalData()
		{
			string path = Path.Combine(Application.dataPath, "WM.json");

			if (File.Exists(path))
			{
				string json = File.ReadAllText(path);
				LoadData(JsonConvert.DeserializeObject<GameData>(json, new JsonSerializerSettings
				{
					TypeNameHandling = TypeNameHandling.Auto,
				}));
			}
			else
			{
				CreateNewGameData();
			}
		}

		public void LoadData(GameData saveData)
		{
			DataManager.CurDollID = saveData.curDollIndex;
			DataManager.DummyDollCount = saveData.dummyDollCount;

			// 통계 초기화
			DataManager.GameStat.Load(saveData.gameStats);

			// 아이템 초기화
			SOManager.ItemInventory.Load(saveData.inventoryItems);

			// 인형 초기화
			SOManager.DollBuffer.Clear();
			foreach (DollSaveData dollData in saveData.dolls)
			{
				GetDoll(dollData.DollID).Load(dollData);
				SOManager.DollBuffer.Add(GetDoll(dollData.DollID));
			}
			for (int i = 0; i < saveData.dummyDollCount - 1; i++)
				SOManager.DollBuffer.Add(GetDoll(Doll.DUMMY_ID));

			// 퀘스트 초기화
			Dictionary<int, QuestState> questStates = new();
			foreach ((int id, int state) in saveData.questStates)
			{
				questStates.Add(id, (QuestState)state);
				if ((QuestState)state >= QuestState.Unlocked)
					SOManager.QuestDataBuffer.Add(GetQuestSO(id));
			}
			DataManager.QuestManager.LoadQuestState(questStates);

			// 레시피 초기화
			DataManager.IsRecipeUnlocked = saveData.hasRecipe;

			// 작업 초기화
			DataManager.WorkManager.Init(saveData.works);
			DataManager.QuestManager.Init(saveData.runtimeQuests.ConvertAll(questData => new RuntimeQuest(questData)));

			// 던전 초기화
			foreach ((int id, DungeonSaveData dungeonData) in saveData.dungeons)
			{
				Dungeon dungeon = GetDungeon(id);
				dungeon.Load(dungeonData);
			}

			// WorldStage 초기화
			foreach ((int worldStageID, WorldStageSaveData worldStageData) in saveData.worldStages)
			{
				WorldStage worldStage = Get<WorldStage>(worldStageID);
				worldStage.Load(worldStageData);
			}

			IsDataLoaded = true;
		}

		public void SaveData()
		{
			GameData gameData = new()
			{
				curDollIndex = DataManager.CurDollID,
				dummyDollCount = DataManager.DummyDollCount,
				inventoryItems = SOManager.ItemInventory.Save(),
				dolls = new(),
				works = DataManager.WorkManager.Works,
				questStates = DataManager.QuestManager.GetQuestStates().ToDictionary(pair => pair.Key, pair => (int)pair.Value),
				hasRecipe = DataManager.IsRecipeUnlocked,
				runtimeQuests = DataManager.QuestManager.Quests.Data.Where(quest => quest.Type != QuestType.Dungeon).ToList().ConvertAll(quest => quest.Save()),
				gameStats = DataManager.GameStat.Save(),
				dungeons = new(),
				worldStages = new()
			};

			ForEach<Doll>(doll => gameData.dolls.Add(doll.Save()));
			ForEach<Dungeon>(dungeon => gameData.dungeons.Add(dungeon.ID, dungeon.Save()));
			ForEach<WorldStage>(worldStage => gameData.worldStages.Add(worldStage.ID, worldStage.Save()));

			if (AppSetting.Data.UseLocalData)
			{
				string json = JsonConvert.SerializeObject(gameData, Formatting.Indented, new JsonSerializerSettings
				{
					TypeNameHandling = TypeNameHandling.Auto,
				});
				string path = Path.Combine(Application.dataPath, "WM.json");
				File.WriteAllText(path, json);
			}
			else
			{
				DataManager.SaveData(gameData);
			}
		}
	}
}