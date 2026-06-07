namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-188 — Bridge 패턴(DomainSDK ↛ Core Singleton 직접호출 금지).
	/// Core 어댑터(ModContentRegistryHost) ctor 가 Register(this) 호출 → DomainSDK 정적 호출로 등록.
	/// FastFail — Bootstrap(RootLifetimeScope eager resolve) 후 호출 보장이라 null check 제거.
	/// </summary>
	public static class ModContentRegistryBridge
	{
		private static IModContentRegistry instance;

		public static IModContentRegistry Instance => instance;

		public static void Register(IModContentRegistry registry)
		{
			instance = registry;
		}

		public static void RegisterQuest(RuntimeQuestSaveData questSaveData)
		{
			instance.RegisterQuest(questSaveData);
		}

		public static void RegisterEffect(EffectInfoData effectInfoData)
		{
			instance.RegisterEffect(effectInfoData);
		}
	}
}
