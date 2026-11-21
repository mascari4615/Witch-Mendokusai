namespace WitchMendokusai
{
	// DungeonManager 는 SceneLifetimeScope — pool-spawned 오브젝트(root scope)가 직접 [Inject] 받을 수 없음.
	// null-safe: 씬에 DungeonManager 없으면 IsDungeon = false (던전 외 씬).
	public static class DungeonManagerBridge
	{
		private static DungeonManager _instance;
		public static void Register(DungeonManager dm) => _instance = dm;
		public static bool IsDungeon => _instance != null && _instance.IsDungeon;
		public static bool IsEnding => _instance != null && _instance.IsEnding;
		// 판 밖이면 효과 없음
		public static DungeonConstraintRunEffect ConstraintEffect => IsDungeon ? _instance.ConstraintEffect : DungeonConstraintRunEffect.Identity;
		public static DungeonConstraintSideEffect SideEffect => IsDungeon ? _instance.SideRules.Effect : DungeonConstraintSideEffect.Identity;

		// 회복 감소로 잃은 회복량. 고유 규칙 (잃은 회복의 냥) 이 받음
		public static void ReportHealLoss(int lostHeal)
		{
			if (IsDungeon)
				_instance.SideRules.OnHealLost(lostHeal);
		}
	}
}
