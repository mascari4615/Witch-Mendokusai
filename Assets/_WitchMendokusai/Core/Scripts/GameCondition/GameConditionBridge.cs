namespace WitchMendokusai
{
	public static class GameConditionBridge
	{
		private static IGameConditionBridge instance;

		public static void Register(IGameConditionBridge bridge)
		{
			instance = bridge;
		}

		public static bool Get(GameConditionType conditionType) => instance[conditionType];

		public static bool IsGameConditionAny(params GameConditionType[] conditions) => instance.IsGameConditionAny(conditions);
	}
}
