using System.Collections.Generic;

namespace WitchMendokusai
{
	public static class DataManagerBridge
	{
		private static DataManager _instance;
		public static void Register(DataManager dataManager) => _instance = dataManager;
		public static GameStat GameStat => _instance.GameStat;
		public static DungeonStat DungeonStat => _instance.DungeonStat;
		public static Dictionary<int, bool> IsRecipeUnlocked => _instance.IsRecipeUnlocked;
	}
}
