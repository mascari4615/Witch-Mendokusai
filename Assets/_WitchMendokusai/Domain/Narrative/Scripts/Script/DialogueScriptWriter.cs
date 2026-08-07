using System.Collections.Generic;
using System.Text;

namespace WitchMendokusai
{
	/// <summary>
	/// 읽어들인 대본을 **다시 원고 글자로** 쓴다 (TASK-WM-052).
	///
	/// ★ 왜 필요한가 두 가지:
	/// ① 에디터에서 고친 대화를 **글로 되돌려** 작가에게 줄 수 있다(원고가 계속 정본으로 남는다).
	/// ② **읽기와 쓰기가 어긋나는지 기계가 잡는다** — 원고를 읽어 다시 쓰고 또 읽었을 때 같지 않으면
	///    둘 중 하나가 틀린 것이다. 읽기 규칙만 있으면 이 어긋남을 아무도 못 본다.
	///
	/// 원본 글자를 그대로 보존하지는 않는다(주석·산문·빈 줄은 대본이 아니므로 사라진다).
	/// 보존하는 것은 **뜻**이다 — 장면·말하는 이·대사·지문·선택지·조건·기다림·효과.
	/// </summary>
	public static class DialogueScriptWriter
	{
		public static string Write(ParsedDialogueScript script)
		{
			StringBuilder builder = new();
			if (script == null)
			{
				return string.Empty;
			}

			for (int s = 0; s < script.Sections.Count; s++)
			{
				DialogueScriptSection section = script.Sections[s];
				if (s > 0)
				{
					builder.Append('\n');
				}
				builder.Append("## ").Append(section.Name).Append('\n');

				for (int e = 0; e < section.Entries.Count; e++)
				{
					WriteEntry(builder, section.Entries[e]);
				}
			}
			return builder.ToString();
		}

		private static void WriteEntry(StringBuilder builder, DialogueScriptEntry entry)
		{
			switch (entry.Kind)
			{
				case DialogueScriptEntryKind.Speak:
					WriteSpeak(builder, entry);
					return;

				case DialogueScriptEntryKind.Choice:
					for (int i = 0; i < entry.Choices.Count; i++)
					{
						DialogueScriptChoice choice = entry.Choices[i];
						builder.Append("> - ").Append(choice.Label);
						WriteCondition(builder, choice.Condition, true);
						builder.Append(" -> ").Append(choice.TargetSection).Append('\n');
					}
					return;

				case DialogueScriptEntryKind.Goto:
					builder.Append("> -> ").Append(entry.TargetSection).Append('\n');
					return;

				case DialogueScriptEntryKind.ConditionalGoto:
					builder.Append("> ?");
					WriteCondition(builder, entry.Condition, false);
					builder.Append(" -> ").Append(entry.TargetSection).Append('\n');
					return;

				case DialogueScriptEntryKind.WaitTime:
					builder.Append("> wait ").Append(entry.Seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("s\n");
					return;

				case DialogueScriptEntryKind.WaitEvent:
					builder.Append("> wait event ").Append(entry.EventId).Append('\n');
					return;

				case DialogueScriptEntryKind.Effect:
					WriteEffects(builder, entry.Effects);
					return;
			}
		}

		private static void WriteSpeak(StringBuilder builder, DialogueScriptEntry entry)
		{
			builder.Append("> ");
			if (string.IsNullOrEmpty(entry.Speaker) == false)
			{
				builder.Append(entry.Speaker).Append(": ");
			}
			if (string.IsNullOrEmpty(entry.StageDirection) == false)
			{
				builder.Append(entry.StageDirection);
				if (string.IsNullOrEmpty(entry.Text) == false)
				{
					builder.Append(' ');
				}
			}
			if (string.IsNullOrEmpty(entry.Text) == false)
			{
				builder.Append('"').Append(entry.Text).Append('"');
			}
			builder.Append('\n');
		}

		/// <summary>조건을 원고 말로 되돌린다. 선택지는 `[…]`, 조건부 건너뛰기는 대괄호 없이.</summary>
		private static void WriteCondition(StringBuilder builder, DialogueScriptCondition condition, bool bracketed)
		{
			if (condition.HasCondition == false)
			{
				return;
			}

			string word = condition.Started ? "시작함" : condition.Expected ? "봤음" : "안봤음";
			if (bracketed)
			{
				builder.Append(" [").Append(word).Append(' ').Append(condition.DialogueId).Append(']');
				return;
			}
			builder.Append(word).Append(' ').Append(condition.DialogueId);
		}

		private static void WriteEffects(StringBuilder builder, IReadOnlyList<EffectInfoData> effects)
		{
			if (effects == null)
			{
				return;
			}
			for (int i = 0; i < effects.Count; i++)
			{
				EffectInfoData effect = effects[i];
				builder.Append("> !").Append(EffectWord(effect.Type)).Append(' ').Append(effect.DataSoID);
				if (effect.Value != 1)
				{
					builder.Append(' ').Append(effect.Value);
				}
				builder.Append('\n');
			}
		}

		/// <summary>
		/// 효과 종류를 원고 말로. 원고에서 읽을 수 없는 종류는 **영어 이름 그대로** 쓴다 —
		/// 그러면 다시 읽을 때 「모르는 효과」로 걸려서 사람 눈에 띈다(조용히 사라지는 것보다 낫다).
		/// </summary>
		private static string EffectWord(EffectType type)
		{
			switch (type)
			{
				case EffectType.Item: return "아이템";
				case EffectType.AddCard: return "카드";
				case EffectType.AddQuest: return "퀘스트추가";
				case EffectType.UnlockQuest: return "퀘스트열기";
				case EffectType.UnlockRecipe: return "레시피";
				default: return type.ToString();
			}
		}
	}
}
