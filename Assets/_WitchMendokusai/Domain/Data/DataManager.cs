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

		// γ P3-K — VContainer 관리로 이관. Construct 에서 주입 (TASK-WM-078, 2026-05-13).
		public SaveManager SaveManager { get; private set; }
		public WorkManager WorkManager { get; private set; }
		public QuestManager QuestManager { get; private set; }
		public GameStat GameStat { get; private set; } = new();
		public DungeonStat DungeonStat { get; private set; } = new();
		public readonly Dictionary<string, (Recipe recipe, int itemID)> CraftDic = new();

		public bool IsDataLoaded => SaveManager.IsDataLoaded;

		public int CurDollID { get; set; }
		public int DummyDollCount { get; set; }
		public Dictionary<int, bool> IsRecipeUnlocked { get; set; } = new();
		// 마도 온실(TASK-WM-167) — 「봐줘야 진짜」 영구 표본 채집 기록(plantDataId → 채집됨). SaveManager 가
		// hasSpecimen ↔ 이 dict 직렬화(IsRecipeUnlocked 와 동형). 도감(PlantCodexCategory)이 read.
		public Dictionary<int, bool> SpecimenCollected { get; set; } = new();

		public string localDisplayName = "";

		private TimeManager timeManager;
		private DataLoader dataLoader;

		[Inject]
		public void Construct(TimeManager timeManager, DataLoader dataLoader, SaveManager saveManager, WorkManager workManager, QuestManager questManager, IEffectRunner effectRunner)
		{
			this.timeManager = timeManager;
			this.dataLoader = dataLoader;
			SaveManager = saveManager;
			WorkManager = workManager;
			QuestManager = questManager;
			// TASK-WM-107 Slice 2C-3/3-2 + TASK-WM-120 γ — 소유자 push (↔QuestManager·
			// ↔IEffectRunner·↔SaveManager 순환 회피, [Inject] pull X / static back-ref X).
			// IEffectRunner 주입은 3-1 후 EffectRunner↛DataManager 라 비순환 (EffectRunner→{SOMgr,Player,Pool}만).
			// SaveManager 는 WM-078 γ P3-K 의 lazy `DataManager.Instance` 를 본 push 로 대체 (WM-120).
			questManager.BindDataManager(this);
			effectRunner.BindDataManager(this);
			saveManager.BindDataManager(this);
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
			// TASK-WM-118 B3b — 전제오류 시인: 결정적 PlayFab 우회 가드 제거.
			// PlayFabManager.Login() 이 이미 `if (AppSetting.Data.UseLocalData) return;`
			// 이고 결정 부팅이 UseLocalData=true 를 세팅 → 여기 BootMode 가드는
			// redundant + 오해유발(캐스케이드 원인을 Login-skip 으로 오귀속)이었다.
			playFabManager.Login();
		}

		public void CreateNewGameData()
		{
			SaveManager.CreateNewGameData();
		}
	}
}
