using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Aspect
{
	/// <summary>두루마리 하나를 풀어 나온 것</summary>
	public readonly struct ScrollDraw
	{
		public ScrollDraw(ScrollEntry entry, int form)
		{
			Entry = entry;
			Form = form;
		}

		public ScrollEntry Entry { get; }

		/// <summary>이번에 열린 모습 번호. 1 이면 처음 배움</summary>
		public int Form { get; }

		public bool IsNew => Form == 1;
	}

	/// <summary>
	/// 봉인된 두루마리의 규칙. 기획 정본 memo wm/design/gameplay/aspects.md (11-25 22:13, 22:19).
	/// 든 것은 봉인된 채로 안 보이고 풀 때 뽑음. 모르는 것이 먼저, 다 알면 안 열린 모습.
	/// 다 모았으면 풀 수 없고 두루마리는 아이템으로 남음 (판매나 분해는 바깥 몫)
	/// </summary>
	public static class ScrollRules
	{
		/// <summary>지금 풀 수 있는 두루마리인지. 아니면 무엇을 풀어도 새것이 없음</summary>
		public static bool CanInterpret(IReadOnlyList<ScrollEntry> entries, ScrollKnowledge knowledge)
		{
			foreach (ScrollEntry entry in entries)
			{
				if (knowledge.IsComplete(entry) == false)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// 든 것을 풀 때 뽑음. 뽑기만 하고 기록은 <see cref="Apply"/>.
		/// pick 은 0 이상 count 미만을 내는 무작위 (시험에서 고정하려고 밖에서 받음)
		/// </summary>
		public static bool TryDraw(IReadOnlyList<ScrollEntry> entries, ScrollKnowledge knowledge, Func<int, int> pick, out ScrollDraw draw)
		{
			List<ScrollEntry> candidates = Unknown(entries, knowledge);
			if (candidates.Count == 0)
			{
				candidates = Incomplete(entries, knowledge);
			}
			if (candidates.Count == 0)
			{
				draw = default;
				return false;
			}

			int index = pick(candidates.Count);
			if (index < 0 || index >= candidates.Count)
			{
				throw new ArgumentOutOfRangeException(nameof(pick), $"pick({candidates.Count}) 가 {index} 를 돌려줌");
			}
			ScrollEntry chosen = candidates[index];
			draw = new ScrollDraw(chosen, knowledge.Opened(chosen.Id) + 1);
			return true;
		}

		/// <summary>뽑은 것을 기록. 같은 뽑기를 두 번 넣으면 막음 (화면이 두 번 부르는 실수)</summary>
		public static void Apply(ScrollKnowledge knowledge, ScrollDraw draw)
		{
			int opened = knowledge.Opened(draw.Entry.Id);
			if (draw.Form != opened + 1)
			{
				throw new InvalidOperationException($"두루마리 항목 {draw.Entry.Id}: 열린 모습 {opened} 인데 {draw.Form} 번째를 기록하려 함");
			}
			knowledge.OpenedForms[draw.Entry.Id] = draw.Form;
		}

		private static List<ScrollEntry> Unknown(IReadOnlyList<ScrollEntry> entries, ScrollKnowledge knowledge)
		{
			List<ScrollEntry> found = new List<ScrollEntry>();
			foreach (ScrollEntry entry in entries)
			{
				if (knowledge.IsLearned(entry.Id) == false)
				{
					found.Add(entry);
				}
			}
			return found;
		}

		private static List<ScrollEntry> Incomplete(IReadOnlyList<ScrollEntry> entries, ScrollKnowledge knowledge)
		{
			List<ScrollEntry> found = new List<ScrollEntry>();
			foreach (ScrollEntry entry in entries)
			{
				if (knowledge.IsComplete(entry) == false)
				{
					found.Add(entry);
				}
			}
			return found;
		}
	}
}
