namespace WitchMendokusai
{
	public static class PlayerProviderBridge
	{
		private static PlayerProvider _instance;
		public static void Register(PlayerProvider playerProvider) => _instance = playerProvider;
		public static Player Current => _instance.Current;
	}
}
