using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 대본 읽기의 줄 갈래 읽기 부분. 같은 클래스의 partial 조각이다.
	public static partial class DialogueScriptParser
	{
		/// <summary>소제목에서 `#` 과 앞뒤 공백만 걷어낸다 — 제목 글자 그대로가 장면 이름이다.</summary>
		private static string ReadHeading(string line) => line.TrimStart('#').Trim();

		private static DialogueScriptSection EnsureSection(ParsedDialogueScript parsed, DialogueScriptSection current, int lineNumber)
		{
			if (current != null)
			{
				return current;
			}
			// 소제목 없이 대사부터 시작하는 원고도 있다 — 이름 없는 첫 장면을 만들어 준다.
			DialogueScriptSection section = new(DEFAULT_SECTION_NAME, lineNumber);
			parsed.Sections.Add(section);
			return section;
		}

		private static void FlushChoices(DialogueScriptSection section, ref List<DialogueScriptChoice> pending, int lineNumber)
		{
			if (pending == null || pending.Count == 0 || section == null)
			{
				pending = null;
				return;
			}
			section.Entries.Add(DialogueScriptEntry.Choice(lineNumber, pending));
			pending = null;
		}

		private static void FlushEffects(DialogueScriptSection section, ref List<EffectInfoData> pending, int lineNumber)
		{
			if (pending == null || pending.Count == 0 || section == null)
			{
				pending = null;
				return;
			}
			section.Entries.Add(DialogueScriptEntry.Effect(lineNumber, pending));
			pending = null;
		}

		/// <summary>
		/// 효과 한 줄을 읽는다: `<무엇> <번호> [수량]`. 아는 말만 받는다 —
		/// 모르는 말을 숫자로 넘겨 짐작하면 **엉뚱한 게 지급된다**(그건 되돌리기 어려운 종류의 사고다).
		/// </summary>
		private static bool TryReadEffect(string text, out EffectInfoData effect)
		{
			effect = default;
			string[] parts = text.Split(new[] { ' ', '	' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2 || int.TryParse(parts[1], out int dataId) == false)
			{
				return false;
			}

			int value = 1;
			if (parts.Length >= 3 && int.TryParse(parts[2], out int parsedValue))
			{
				value = parsedValue;
			}

			EffectType type;
			switch (parts[0])
			{
				case "아이템":
				case "item":
					type = EffectType.Item;
					break;
				case "카드":
				case "card":
					type = EffectType.AddCard;
					break;
				case "퀘스트추가":
				case "quest":
					type = EffectType.AddQuest;
					break;
				case "퀘스트열기":
				case "unlockquest":
					type = EffectType.UnlockQuest;
					break;
				case "레시피":
				case "recipe":
					type = EffectType.UnlockRecipe;
					break;
				default:
					return false;
			}

			effect = new EffectInfoData
			{
				Type = type,
				DataSoID = dataId,
				ArithmeticOperator = ArithmeticOperator.Add,
				Value = value,
			};
			return true;
		}

		private static bool TryReadWait(string body, int lineNumber, out DialogueScriptEntry entry, out string problem)
		{
			entry = null;
			problem = null;

			string lowered = body.ToLowerInvariant();
			bool isWait = lowered.StartsWith("wait ", StringComparison.Ordinal) || body.StartsWith("기다림 ", StringComparison.Ordinal);
			if (isWait == false)
			{
				return false;
			}

			string argument = body.Substring(body.IndexOf(' ') + 1).Trim();
			if (argument.StartsWith("event ", StringComparison.Ordinal) || argument.StartsWith("사건 ", StringComparison.Ordinal))
			{
				string eventId = argument.Substring(argument.IndexOf(' ') + 1).Trim();
				if (eventId.Length == 0)
				{
					problem = "기다릴 사건 이름이 없다.";
					return false;
				}
				entry = DialogueScriptEntry.WaitEvent(lineNumber, eventId);
				return true;
			}

			string number = argument.TrimEnd('s', 'S', '초', ' ');
			if (float.TryParse(number, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float seconds) == false)
			{
				problem = $"기다릴 시간을 못 읽었다: \"{argument}\"";
				return false;
			}
			entry = DialogueScriptEntry.WaitTime(lineNumber, seconds);
			return true;
		}
	}
}
