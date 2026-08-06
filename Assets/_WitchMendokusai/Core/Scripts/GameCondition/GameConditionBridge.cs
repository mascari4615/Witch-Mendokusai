namespace WitchMendokusai
{
	public static class GameConditionBridge
	{
		private static IGameConditionBridge instance;

		public static void Register(IGameConditionBridge bridge)
		{
			instance = bridge;
		}

		/// <summary>
		/// 아직 아무도 등록 안 했나. 부팅 이른 화면(제목·로비)에서 매 프레임 묻는 쪽이 필요하다 —
		/// 그냥 물으면 널 참조로 터지는데, 그게 매 프레임이면 로그가 덮여 진짜 문제가 안 보인다.
		/// (`SOManagerBridge.HasInstance` 와 같은 관례.)
		/// </summary>
		public static bool HasInstance => instance != null;

		public static bool Get(GameConditionType conditionType) => instance[conditionType];

		public static bool IsGameConditionAny(params GameConditionType[] conditions) => instance.IsGameConditionAny(conditions);
	}
}
