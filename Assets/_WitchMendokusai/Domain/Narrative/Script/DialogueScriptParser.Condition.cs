using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 대본 읽기의 조건 읽기 부분. 같은 클래스의 partial 조각이다.
	public static partial class DialogueScriptParser
	{
		/// <summary>
		/// 라벨 끝의 `[조건]` 을 떼어 읽는다(`- 열쇠를 보여준다 [봤음 4615] -> 장면`).
		/// 대괄호가 없으면 조건 없음. 있는데 못 읽으면 **오류로 남기고 조건 없음으로 둔다** —
		/// 조용히 잠가 버리면 「왜 이 선택지가 안 뜨지」를 영영 못 찾는다.
		/// </summary>
		private static DialogueScriptCondition ReadBracketCondition(ParsedDialogueScript parsed, ref string label, int lineNumber)
		{
			int open = label.LastIndexOf('[');
			int close = label.LastIndexOf(']');
			if (open < 0 || close < open)
			{
				return default;
			}

			string conditionText = label.Substring(open + 1, close - open - 1).Trim();
			label = label.Substring(0, open).Trim();

			if (TryReadCondition(conditionText, out DialogueScriptCondition condition))
			{
				return condition;
			}
			parsed.Issues.Add(new DialogueScriptIssue(lineNumber, $"모르는 조건이다: \"{conditionText}\""));
			return default;
		}

		/// <summary>
		/// 조건 한 마디를 읽는다. 지금 아는 말: `봤음/seen` · `안봤음/unseen` · `시작함/started` + 대화 번호.
		/// 모르는 말이면 false — 부르는 쪽이 줄 번호와 함께 남긴다.
		/// </summary>
		private static bool TryReadCondition(string text, out DialogueScriptCondition condition)
		{
			condition = default;
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}

			// 「고른 답」은 라벨이 문장이라 공백을 품는다 — 토큰으로 쪼개기 **전에** 따로 읽는다.
			// (쪼개고 다시 붙이면 원고에 쓴 띄어쓰기가 달라져서 라벨이 안 맞는다.)
			if (TryReadChosenCondition(text, out condition))
			{
				return true;
			}

			string[] parts = text.Split(new[] { ' ', '	', ':' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2 || parts.Length > 3)
			{
				return false;
			}
			if (int.TryParse(parts[1], out int dialogueId) == false)
			{
				return false;
			}

			// 물건 조건은 개수를 하나 더 받는다: `아이템 1001 3` = 세 개 이상.
			int amount = 1;
			if (parts.Length == 3 && int.TryParse(parts[2], out int parsedAmount))
			{
				amount = parsedAmount;
			}
			else if (parts.Length == 3)
			{
				return false;
			}

			switch (parts[0])
			{
				case "퀘스트완료":
				case "questdone":
					condition = new DialogueScriptCondition(DialogueScriptConditionKind.QuestState, dialogueId, true, false, 1, WitchMendokusai.QuestState.Completed);
					return true;
				case "퀘스트미완":
				case "questnotdone":
					condition = new DialogueScriptCondition(DialogueScriptConditionKind.QuestState, dialogueId, false, false, 1, WitchMendokusai.QuestState.Completed);
					return true;
				case "퀘스트열림":
				case "questopen":
					condition = new DialogueScriptCondition(DialogueScriptConditionKind.QuestState, dialogueId, true, false, 1, WitchMendokusai.QuestState.Unlocked);
					return true;
				case "아이템":
				case "item":
					condition = new DialogueScriptCondition(DialogueScriptConditionKind.ItemCount, dialogueId, true, false, amount);
					return true;
				case "아이템없음":
				case "noitem":
					condition = new DialogueScriptCondition(DialogueScriptConditionKind.ItemCount, dialogueId, false, false, amount);
					return true;
			}

			// 이력 조건은 개수를 안 받는다 — 셋을 적었으면 오타다.
			if (parts.Length == 3)
			{
				return false;
			}

			switch (parts[0])
			{
				case "봤음":
				case "seen":
					condition = new DialogueScriptCondition(DialogueScriptConditionKind.Seen, dialogueId, true, false);
					return true;
				case "안봤음":
				case "unseen":
					condition = new DialogueScriptCondition(DialogueScriptConditionKind.Seen, dialogueId, false, false);
					return true;
				case "시작함":
				case "started":
					condition = new DialogueScriptCondition(DialogueScriptConditionKind.Seen, dialogueId, true, true);
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// `골랐음 5200 그냥 간다` — 그 대화에서 **그 답을 고른 적 있나**.
		///
		/// ★ 왜 따로 읽나: 라벨은 사람이 쓴 문장이라 **공백을 품는다.** 다른 조건처럼 토큰으로 쪼개면
		///   「그냥 간다」가 두 조각이 되고, 다시 붙이면 원고에 쓴 띄어쓰기와 달라져 영영 안 맞는다.
		///   그래서 번호 뒤는 **남은 글자 그대로** 라벨로 쓴다.
		/// </summary>
		private static bool TryReadChosenCondition(string text, out DialogueScriptCondition condition)
		{
			condition = default;

			string trimmed = text.Trim();
			bool expected;
			if (trimmed.StartsWith("골랐음", StringComparison.Ordinal) || trimmed.StartsWith("chose", StringComparison.Ordinal))
			{
				expected = true;
			}
			else if (trimmed.StartsWith("안골랐음", StringComparison.Ordinal) || trimmed.StartsWith("notchose", StringComparison.Ordinal))
			{
				expected = false;
			}
			else
			{
				return false;
			}

			int wordEnd = trimmed.IndexOf(' ');
			if (wordEnd < 0)
			{
				return false;
			}

			string rest = trimmed.Substring(wordEnd + 1).Trim();
			int idEnd = rest.IndexOf(' ');
			if (idEnd < 0)
			{
				return false;
			}
			if (int.TryParse(rest.Substring(0, idEnd), out int dialogueId) == false)
			{
				return false;
			}

			string label = rest.Substring(idEnd + 1).Trim();
			if (label.Length == 0)
			{
				return false;
			}

			condition = new DialogueScriptCondition(DialogueScriptConditionKind.Chosen, dialogueId, expected, false,
				1, WitchMendokusai.QuestState.Completed, label);
			return true;
		}
	}
}
