using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 지나간 대사 기록 (TASK-WM-052).
	///
	/// ★ 왜 필요한가: 대사는 시간이 지나면 사라진다. 잠깐 눈을 뗐거나 빠르게 넘겼으면 **다시 볼 방법이 없다.**
	///   그게 서사 게임에서 제일 답답한 순간이다. 화면(로그 창)은 나중 일이지만, **남겨 두는 것부터** 해야 한다 —
	///   안 남기면 나중에 창을 만들어도 보여줄 게 없다.
	///
	/// 저장 대상이 아니다(껐다 켜면 지워진다). 「방금 뭐라고 했지」를 위한 것이지 일지가 아니다.
	/// 가득 차면 **오래된 것부터 버린다** — 로그는 꼬리가 중요하다(차례 세우기와 반대 방향의 판단).
	/// </summary>
	public sealed class DialogueTranscript
	{
		public const int DEFAULT_CAPACITY = 100;

		/// <summary>기록 한 줄 — 누가 무엇을 말했나. 지문은 말이 아니라서 안 남긴다.</summary>
		public readonly struct Entry
		{
			public string Speaker { get; }
			public string Text { get; }

			public Entry(string speaker, string text)
			{
				Speaker = speaker;
				Text = text;
			}
		}

		private readonly List<Entry> entries = new();
		private readonly int capacity;

		public DialogueTranscript(int capacity = DEFAULT_CAPACITY)
		{
			this.capacity = capacity < 1 ? 1 : capacity;
		}

		public IReadOnlyList<Entry> Entries => entries;
		public int Count => entries.Count;

		/// <summary>가장 최근 줄. 하나도 없으면 기본값(빈 것).</summary>
		public Entry Last => entries.Count == 0 ? default : entries[entries.Count - 1];

		/// <summary>말한 줄을 남긴다. 말이 없는 줄(지문만·빈 대사)은 안 남긴다 — 로그에 빈칸이 쌓인다.</summary>
		public void Record(DialogueLine line)
		{
			if (line == null || string.IsNullOrWhiteSpace(line.Text))
			{
				return;
			}

			entries.Add(new Entry(line.ResolveSpeakerName(), line.Text));
			if (entries.Count > capacity)
			{
				entries.RemoveAt(0);
			}
		}

		public void Clear() => entries.Clear();
	}
}
