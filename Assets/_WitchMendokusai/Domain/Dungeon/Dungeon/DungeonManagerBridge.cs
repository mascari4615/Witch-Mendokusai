namespace WitchMendokusai
{
	// DungeonManager 는 SceneLifetimeScope — pool-spawned 오브젝트(root scope)가 직접 [Inject] 받을 수 없음.
	// null-safe: 씬에 DungeonManager 없으면 IsDungeon = false (던전 외 씬).
	public static class DungeonManagerBridge
	{
		private static DungeonManager _instance;
		public static void Register(DungeonManager dm) => _instance = dm;
		public static bool IsDungeon => _instance != null && _instance.IsDungeon;
	}
}
