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
		// 특수시공 개척(TASK-WM-194) — 스테이지별 최고 도달 웨이브(stageID → wave). 무한 모드의 점수 그 자체.
		// SaveManager 가 towerDefenseBestWave ↔ 이 dict 직렬화(SpecimenCollected 와 동형).
		public Dictionary<int, int> TowerDefenseBestWave { get; set; } = new();
		// 개척 메타 — 유물(재화)과 뽑아서 얻은 포탑 인형 목록.
		public int TowerDefenseRelics { get; set; }

		/// <summary>
		/// 개척 이어하기 — 한 슬롯. 껐다 켜면 처음부터였다(개선 목록 15번).
		/// 여러 슬롯을 두지 않는 이유: 판이 하나뿐인데 슬롯이 여럿이면 고르는 화면부터 만들어야 하고,
		/// 그건 「이어하기」가 아니라 세이브 관리다.
		/// </summary>
		public TowerDefenseSaveData TowerDefenseResume { get; set; }
		public List<int> TowerDefenseUnlockedTowers { get; set; } = new();

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
