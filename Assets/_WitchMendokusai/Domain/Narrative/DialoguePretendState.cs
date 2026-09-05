using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 「~했다 치고」 상태 — 게임을 안 켜고 조건이 걸린 가지를 밟아 보기 위한 가짜 (TASK-WM-052).
	///
	/// ★ 왜 도구 밖에 있나: 원래 이 판단은 미리보기 창 **안에** 있었다. 그런데 그 창은
	///   하네스도 안 물고(에디터 타입) 단일 파일 검사기도 못 본다 — **아무 검사도 안 받는 자리**였다.
	///   원고 쓰는 사람이 「무슨 가지를 보게 되는가」를 정하는 로직이 거기 있었는데도 그랬다.
	///   판단만 꺼내 오면 화면 없이 시험할 수 있고, 창은 칸을 그리는 얇은 껍데기가 된다.
	///
	/// ★ Play 중에는 쓰지 않는다 — 그땐 진짜 상태가 있고, 가짜로 덮으면 게임 쪽 판단까지 흔든다.
	///   그 판단은 부르는 쪽(창)이 한다. 여기는 「무엇을 참으로 칠까」만 안다.
	/// </summary>
	public sealed class DialoguePretendState
	{
		/// <summary>「가졌다」고 한 물건의 개수 — 개수까지 흉내내면 손잡이가 복잡해져서 안 쓰인다.</summary>
		public const int PRETEND_ITEM_AMOUNT = 99;

		private sealed class PretendItems : IDialogueItemCountSource
		{
			private readonly HashSet<int> owned;

			public PretendItems(HashSet<int> ownedItemIds)
			{
				owned = ownedItemIds;
			}

			public int GetItemAmount(int itemId) => owned.Contains(itemId) ? PRETEND_ITEM_AMOUNT : 0;
		}

		private sealed class PretendQuests : IDialogueQuestStateSource
		{
			private readonly HashSet<int> completed;

			public PretendQuests(HashSet<int> completedQuestIds)
			{
				completed = completedQuestIds;
			}

			public bool TryGetQuestState(int questId, out QuestState state)
			{
				state = completed.Contains(questId) ? QuestState.Completed : QuestState.Locked;
				return true;
			}
		}

		public DialogueHistory History { get; } = new();
		public IDialogueItemCountSource Items { get; }
		public IDialogueQuestStateSource Quests { get; }

		private DialoguePretendState(IReadOnlyList<int> seenDialogueIds, HashSet<int> itemIds, HashSet<int> questIds)
		{
			for (int i = 0; i < seenDialogueIds.Count; i++)
			{
				History.MarkCompleted(seenDialogueIds[i]);
			}
			Items = new PretendItems(itemIds);
			Quests = new PretendQuests(questIds);
		}

		/// <summary>사람이 적어 넣은 번호 글자들로 가짜 상태를 만든다.</summary>
		public static DialoguePretendState From(string seenDialogueIds, string itemIds, string completedQuestIds)
		{
			return new DialoguePretendState(
				ParseIds(seenDialogueIds),
				new HashSet<int>(ParseIds(itemIds)),
				new HashSet<int>(ParseIds(completedQuestIds)));
		}

		/// <summary>
		/// 쉼표로 적은 번호들을 읽는다. 숫자가 아닌 조각은 **그냥 버린다** —
		/// 손으로 적는 칸이라 오타·빈칸·끝쉼표가 흔한데, 거기서 터지면 도구를 못 쓴다.
		/// </summary>
		public static List<int> ParseIds(string text)
		{
			List<int> ids = new();
			if (string.IsNullOrWhiteSpace(text))
			{
				return ids;
			}

			string[] parts = text.Split(',');
			for (int i = 0; i < parts.Length; i++)
			{
				if (int.TryParse(parts[i].Trim(), out int id))
				{
					ids.Add(id);
				}
			}
			return ids;
		}

		/// <summary>세 창구에 가짜를 끼운다.</summary>
		public void Register()
		{
			DialogueHistoryBridge.Register(History);
			DialogueItemBridge.Register(Items);
			DialogueQuestBridge.Register(Quests);
		}

		/// <summary>자기가 끼운 것만 뺀다 — 그 사이 다른 쪽이 등록했으면 안 건드린다.</summary>
		public void Unregister()
		{
			DialogueHistoryBridge.Clear(History);
			DialogueItemBridge.Clear(Items);
			DialogueQuestBridge.Clear(Quests);
		}
	}
}
