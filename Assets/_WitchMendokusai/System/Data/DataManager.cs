using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using System.IO;
using Newtonsoft.Json;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	[DefaultExecutionOrder(-100)]
	public class DataManager : Singleton<DataManager>
	{
		public SaveManager SaveManager { get; private set; } = new();
		public WorkManager WorkManager { get; private set; } = new();
		public QuestManager QuestManager { get; private set; } = new();
		public GameStat GameStat { get; private set; } = new();
		public DungeonStat DungeonStat { get; private set; } = new();
		public readonly Dictionary<string, (Recipe recipe, int itemID)> CraftDic = new();

		private SOManager SOManager;

		public bool IsDataLoaded => SaveManager.IsDataLoaded;

		public int CurDollID;
		public int DummyDollCount;
		public Dictionary<int, bool> IsRecipeUnlocked = new();

		public string localDisplayName = "";

		public PlayFabManager PlayFabManager { get; private set; }

		protected override void Awake()
		{
			base.Awake();
			StartCoroutine(Init());
		}

		private IEnumerator Init()
		{
			SOManager = SOManager.Instance;
			PlayFabManager = GetComponent<PlayFabManager>();
			TimeManager.Instance.RegisterCallback(WorkManager.TickEachWorks);

			yield return StartCoroutine(DataLoader.Instance.LoadData());

			ForEach<ItemData>(itemData =>
			{
				if (itemData.Recipes == null)
					return;

				foreach (Recipe recipe in itemData.Recipes)
					CraftDic[RecipeUtil.RecipeToString(recipe)] = (recipe, itemData.ID);
			});

			if (GameSetting.UseLocalData)
			{
				SaveManager.LoadLocalData();
			}
		}

		public Color GetGradeColor(ItemGrade grade) => grade switch
		{
			ItemGrade.Common => Color.white,
			ItemGrade.Uncommon => new Color(43 / 255f, 123 / 255f, 1),
			ItemGrade.Rare => new Color(242 / 255f, 210 / 255f, 0),
			ItemGrade.Legendary => new Color(1, 0, 142 / 255f),
			_ => throw new ArgumentOutOfRangeException(nameof(ItemGrade), grade, null)
		};

		private void OnApplicationQuit() => SaveManager.SaveData();

		public List<EquipmentData> GetEquipmentDatas(int dollID)
		{
			Doll doll = GetDoll(dollID);
			List<EquipmentData> equipmentDatas = new()
			{
				doll.SignatureEquipment
			};

			List<Guid?> guids = doll.EquipmentGuids;
			foreach (Guid? guid in guids)
			{
				if (guid == null)
					equipmentDatas.Add(null);
				else
					equipmentDatas.Add(SOManager.ItemInventory.GetItem(guid)?.Data as EquipmentData);
			}

			return equipmentDatas;
		}

		public void SetCurDoll(int dollID)
		{
			CurDollID = dollID;
			SaveManager.SaveData();
		}
	}
}