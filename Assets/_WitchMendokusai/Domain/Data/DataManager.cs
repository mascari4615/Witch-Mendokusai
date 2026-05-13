using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	[DefaultExecutionOrder(-100)]
	public class DataManager : MonoBehaviour
	{
		public static DataManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out DataManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		public SaveManager SaveManager { get; private set; } = new();
		public WorkManager WorkManager { get; private set; } = new();
		public QuestManager QuestManager { get; private set; } = new();
		public GameStat GameStat { get; private set; } = new();
		public DungeonStat DungeonStat { get; private set; } = new();
		public readonly Dictionary<string, (Recipe recipe, int itemID)> CraftDic = new();

		public bool IsDataLoaded => SaveManager.IsDataLoaded;

		public int CurDollID { get; set; }
		public int DummyDollCount { get; set; }
		public Dictionary<int, bool> IsRecipeUnlocked { get; set; } = new();

		public string localDisplayName = "";

		private TimeManager timeManager;
		private DataLoader dataLoader;

		[Inject]
		public void Construct(TimeManager timeManager, DataLoader dataLoader)
		{
			this.timeManager = timeManager;
			this.dataLoader = dataLoader;
		}

		private PlayFabManager playFabManager;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		public IEnumerator Init()
		{
			Debug.Log($"{nameof(DataManager)} {nameof(Init)}");

			playFabManager = GetComponent<PlayFabManager>();
			timeManager.RegisterCallback(WorkManager.TickEachWorks);

			yield return StartCoroutine(dataLoader.LoadData());

			ForEach<ItemData>(itemData =>
			{
				if (itemData.Recipes == null)
					return;

				foreach (Recipe recipe in itemData.Recipes)
					CraftDic[RecipeUtil.RecipeToString(recipe)] = (recipe, itemData.ID);
			});

			if (AppSetting.Data.UseLocalData)
			{
				SaveManager.LoadLocalData();
			}
		}

		private void OnApplicationQuit() => SaveManager.SaveData();

		public List<EquipmentData> GetEquipmentData(int dollID)
		{
			Doll doll = GetDoll(dollID);
			List<EquipmentData> equipmentData = new()
			{
				doll.SignatureEquipment
			};

			foreach (Item item in doll.Equipment)
			{
				equipmentData.Add(item?.Data as EquipmentData);
			}

			return equipmentData;
		}

		public void SetCurDoll(int dollID)
		{
			CurDollID = dollID;
			SaveManager.SaveData();
		}

		public void SaveData(GameData gameData)
		{
			playFabManager.SavePlayerData(gameData);
		}

		public void Login()
		{
			playFabManager.Login();
		}

		public void CreateNewGameData()
		{
			SaveManager.CreateNewGameData();
		}
	}
}
