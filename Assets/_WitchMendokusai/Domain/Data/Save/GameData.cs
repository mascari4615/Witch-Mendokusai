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
		public List<RuntimeQuestSaveData> runtimeQuests = new();
		public Dictionary<GameStatType, int> gameStats = new();
		public Dictionary<int, DungeonSaveData> dungeons = new(); // DungeonID
		public Dictionary<int, WorldStageSaveData> worldStages = new(); // WorldStageID, RuntimeBuildingData
		public Dictionary<int, UpgradeSaveData> upgrades = new(); // UpgradeID, UpgradeSaveData
		public List<WindowLayoutEntry> windowLayouts = new();
	}
}