namespace WitchMendokusai
{
	/// <summary>실제 퀘스트 관리자에 묻는 얇은 어댑터 — 상태 판단은 게임 것을 그대로 쓴다.</summary>
	public sealed class QuestManagerDialogueQuestSource : IDialogueQuestStateSource
	{
		private readonly QuestManager questManager;

		public QuestManagerDialogueQuestSource(QuestManager manager)
		{
			questManager = manager;
		}

		public bool TryGetQuestState(int questId, out QuestState state)
		{
			// 모르는 번호로 물으면 게임 쪽이 던진다(FastFail 설계) — 대화가 그걸로 죽으면 안 되니 여기서 막는다.
			return questManager.GetQuestStates().TryGetValue(questId, out state);
		}
	}
}
