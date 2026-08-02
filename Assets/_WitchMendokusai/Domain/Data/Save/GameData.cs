using System;
using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	[Serializable]
	public class GameData
	{
		public int curDollIndex = 0;
		public int dummyDollCount = 1;

		public List<InventorySlotSaveData> inventoryItems = new();
		public List<InventorySlotSaveData> hotbarItems = new();
		public List<DollSaveData> dolls = new();
		public Dictionary<WorkListType, List<Work>> works = new()
		{
			{ WorkListType.DollWork, new() },
			{ WorkListType.DummyWork, new() },
			{ WorkListType.VQuestWork, new() }
		};
		public Dictionary<int, int> questStates = new();
		public Dictionary<int, bool> hasRecipe = new();
		// 마도 온실(TASK-WM-167) — 「봐줘야 진짜」 영구 표본 기록. plantDataId → 채집됨. 관찰+개화+수확된 작물만.
		// 수확해 사라져도 도감엔 영원히 남는다(테마 "우리는 진짜인가" = 봐준 건 영원). hasRecipe 와 동형.
		public Dictionary<int, bool> hasSpecimen = new();
		// 특수시공 개척(TASK-WM-194) — 스테이지별 최고 도달 웨이브. 무한 모드라 승리가 없고 *버틴 웨이브 수가 곧 점수*라,
		// 이 기록이 없으면 판이 끝나도 남는 게 없어 다시 할 이유가 사라진다. stageID → 최고 웨이브.
		public Dictionary<int, int> towerDefenseBestWave = new();
		// 특수시공 개척 메타(TASK-WM-194) — 판이 끝나면 남는 것. 유물 = 버틴 만큼 받는 재화,
		// 해금 포탑 = 유물로 뽑아 얻은 인형. 이게 없으면 한 판이 끝나도 다음 판이 달라지지 않는다.
		public int towerDefenseRelics;
		public List<int> towerDefenseUnlockedTowers = new();
		public List<RuntimeQuestSaveData> runtimeQuests = new();
		public Dictionary<GameStatType, int> gameStats = new();
		public Dictionary<int, DungeonSaveData> dungeons = new(); // DungeonID
		public Dictionary<int, WorldStageSaveData> worldStages = new(); // WorldStageID, RuntimeBuildingData
		public Dictionary<int, UpgradeSaveData> upgrades = new(); // UpgradeID, UpgradeSaveData
		public List<WindowLayoutEntry> windowLayouts = new();
	}
}