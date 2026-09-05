using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 대화가 「그 퀘스트 어디까지 갔나」를 묻는 좁은 구멍 (TASK-WM-052).
	/// 세는 일은 게임(QuestManager)이 이미 한다 — 여기선 답만 받는다(가방·이력과 같은 형태).
	/// </summary>
	public interface IDialogueQuestStateSource
	{
		/// <summary>모르는 퀘스트면 false 를 준다(터뜨리지 않는다).</summary>
		bool TryGetQuestState(int questId, out QuestState state);
	}

	/// <summary>퀘스트 상태 창구 static accessor — 이력·가방 다리와 동형.</summary>
	public static class DialogueQuestBridge
	{
		private static IDialogueQuestStateSource source;

		public static void Register(IDialogueQuestStateSource questStateSource) => source = questStateSource;

		public static void Clear(IDialogueQuestStateSource questStateSource)
		{
			if (source == questStateSource)
			{
				source = null;
			}
		}

		public static IDialogueQuestStateSource Current => source;

		public static bool TryGetState(int questId, out QuestState state)
		{
			state = QuestState.Locked;
			return source != null && source.TryGetQuestState(questId, out state);
		}
	}

	/// <summary>
	/// 「그 퀘스트가 이 상태인가」 조건. 분기 노드·선택지에 그대로 꽂힌다.
	///
	/// 쓰임새: 의뢰를 끝낸 뒤에만 나오는 대사 · 아직 안 받은 사람에게만 하는 안내.
	///
	/// ★ 창구가 없거나 모르는 퀘스트면 **「잠김」으로 친다** — 진행 안 한 것으로 보는 쪽이 안전하다.
	///   「끝냈다」로 잘못 보면 안 준 보상을 받은 것처럼 구는 대사가 나온다.
	/// </summary>
	[Serializable]
	public class DialogueQuestCriteria : Criteria
	{
		[SerializeField] private int questId;

		[Tooltip("이 상태와 같으면 참.")]
		[SerializeField] private QuestState expectedState = QuestState.Completed;

		[Tooltip("체크 해제하면 「그 상태가 아니면」이 참이 된다.")]
		[SerializeField] private bool expectedMatch = true;

		public int QuestId { get => questId; set => questId = value; }
		public QuestState ExpectedState { get => expectedState; set => expectedState = value; }
		public bool ExpectedMatch { get => expectedMatch; set => expectedMatch = value; }

		private QuestState CurrentState =>
			DialogueQuestBridge.TryGetState(questId, out QuestState state) ? state : QuestState.Locked;

		public override int GetCurValue() => (int)CurrentState;

		public override int GetTargetValue() => (int)expectedState;

		public override bool Evaluate() => (CurrentState == expectedState) == expectedMatch;
	}
}
