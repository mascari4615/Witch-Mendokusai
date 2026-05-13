namespace WitchMendokusai
{
	public static class QuestManagerBridge
	{
		private static QuestManager _instance;
		public static void Register(QuestManager questManager) => _instance = questManager;
		public static void AddQuest(RuntimeQuest quest) => _instance.AddQuest(quest);
		public static void UnlockQuest(QuestSO questSO) => _instance.UnlockQuest(questSO);
	}
}
