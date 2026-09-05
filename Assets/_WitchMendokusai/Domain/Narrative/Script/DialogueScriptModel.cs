using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	public enum DialogueScriptEntryKind
	{
		Speak = 0,
		Choice = 1,
		Goto = 2,
		/// <summary>조건이 맞으면 그 장면으로, 아니면 다음 줄로.</summary>
		ConditionalGoto = 5,
		/// <summary>물건·퀘스트 같은 것을 실제로 일으킨다.</summary>
		Effect = 6,
		WaitTime = 3,
		WaitEvent = 4,
	}

	/// <summary>대본에 적을 수 있는 조건의 종류. 지금은 「그 대화를 봤나」 하나 — 늘릴 자리다.</summary>
	public enum DialogueScriptConditionKind
	{
		None = 0,

		/// <summary>그 대화를 봤는가(끝까지 갔는가 / 시작이라도 했는가는 <see cref="DialogueScriptCondition.Started"/>).</summary>
		Seen = 1,

		/// <summary>그 물건을 몇 개 이상 가졌는가.</summary>
		ItemCount = 2,

		/// <summary>그 퀘스트가 어떤 상태인가.</summary>
		QuestState = 3,

		/// <summary>그 대화에서 이 답을 골랐나.</summary>
		Chosen = 4,
	}

	/// <summary>
	/// 대본에 적힌 조건 한 줄. **여기서는 조건을 만들지 않고 「무엇을 적었나」만 담는다** —
	/// 실제 조건 객체(<see cref="Criteria"/>)는 그래프를 세울 때 만든다(읽기와 세우기의 분리).
	/// </summary>
	public readonly struct DialogueScriptCondition
	{
		public DialogueScriptConditionKind Kind { get; }
		public int DialogueId { get; }
		public bool Expected { get; }
		public bool Started { get; }

		/// <summary>물건 조건에서 쓰는 「이만큼 이상」. 이력 조건에서는 안 쓴다.</summary>
		public int Amount { get; }

		/// <summary>퀘스트 조건에서 묻는 상태. 다른 조건에서는 안 쓴다.</summary>
		public QuestState QuestState { get; }

		/// <summary>「고른 답」 조건에서 묻는 라벨 — 원고에 쓴 글자 그대로. 다른 조건에서는 비어 있다.</summary>
		public string Label { get; }

		public DialogueScriptCondition(DialogueScriptConditionKind kind, int dialogueId, bool expected, bool started,
			int amount = 1, QuestState questState = WitchMendokusai.QuestState.Completed, string label = null)
		{
			Kind = kind;
			DialogueId = dialogueId;
			Expected = expected;
			Started = started;
			Amount = amount;
			QuestState = questState;
			Label = label;
		}

		public bool HasCondition => Kind != DialogueScriptConditionKind.None;
	}

	/// <summary>선택지 한 줄 — 라벨과 갈 곳(장면 이름), 그리고 보일 조건(없을 수 있다).</summary>
	public readonly struct DialogueScriptChoice
	{
		public string Label { get; }
		public string TargetSection { get; }
		public DialogueScriptCondition Condition { get; }

		public DialogueScriptChoice(string label, string targetSection, DialogueScriptCondition condition = default)
		{
			Label = label;
			TargetSection = targetSection;
			Condition = condition;
		}
	}

	/// <summary>대본 한 줄이 뜻하는 것. 어느 줄에서 왔는지(<see cref="LineNumber"/>)를 끝까지 들고 다닌다.</summary>
	public sealed class DialogueScriptEntry
	{
		public DialogueScriptEntryKind Kind { get; }
		public int LineNumber { get; }
		public string Speaker { get; }
		public string Text { get; }

		/// <summary>말이 아닌 것 — 「(한숨)」·「(이불 속)」. 원고에 흔하고, 말풍선에 넣으면 안 된다.</summary>
		public string StageDirection { get; private set; }
		public string TargetSection { get; }
		public float Seconds { get; }
		public string EventId { get; }
		public IReadOnlyList<DialogueScriptChoice> Choices { get; }

		/// <summary>글로 적은 효과들(번호로 가리킨다). 다른 종류에서는 비어 있다.</summary>
		public IReadOnlyList<EffectInfoData> Effects { get; private set; }

		/// <summary>조건부 건너뛰기의 조건. 다른 종류에서는 비어 있다.</summary>
		public DialogueScriptCondition Condition { get; private set; }

		private DialogueScriptEntry(DialogueScriptEntryKind kind, int lineNumber, string speaker, string text,
			string targetSection, float seconds, string eventId, IReadOnlyList<DialogueScriptChoice> choices)
		{
			Kind = kind;
			LineNumber = lineNumber;
			Speaker = speaker;
			Text = text;
			TargetSection = targetSection;
			Seconds = seconds;
			EventId = eventId;
			Choices = choices;
		}

		public static DialogueScriptEntry Speak(int lineNumber, string speaker, string text, string stageDirection = null) =>
			new(DialogueScriptEntryKind.Speak, lineNumber, speaker, text, null, 0f, null, null) { StageDirection = stageDirection };
		public static DialogueScriptEntry Choice(int lineNumber, IReadOnlyList<DialogueScriptChoice> choices) =>
			new(DialogueScriptEntryKind.Choice, lineNumber, null, null, null, 0f, null, choices);
		public static DialogueScriptEntry Goto(int lineNumber, string targetSection) =>
			new(DialogueScriptEntryKind.Goto, lineNumber, null, null, targetSection, 0f, null, null);
		public static DialogueScriptEntry Effect(int lineNumber, IReadOnlyList<EffectInfoData> effects) =>
			new(DialogueScriptEntryKind.Effect, lineNumber, null, null, null, 0f, null, null) { Effects = effects };
		public static DialogueScriptEntry ConditionalGoto(int lineNumber, string targetSection, DialogueScriptCondition condition) =>
			new(DialogueScriptEntryKind.ConditionalGoto, lineNumber, null, null, targetSection, 0f, null, null) { Condition = condition };
		public static DialogueScriptEntry WaitTime(int lineNumber, float seconds) =>
			new(DialogueScriptEntryKind.WaitTime, lineNumber, null, null, null, seconds, null, null);
		public static DialogueScriptEntry WaitEvent(int lineNumber, string eventId) =>
			new(DialogueScriptEntryKind.WaitEvent, lineNumber, null, null, null, 0f, eventId, null);
	}

	public sealed class DialogueScriptSection
	{
		public string Name { get; }
		public int LineNumber { get; }
		public List<DialogueScriptEntry> Entries { get; } = new();

		public DialogueScriptSection(string name, int lineNumber)
		{
			Name = name;
			LineNumber = lineNumber;
		}
	}

	/// <summary>읽다가 걸린 것 — 줄 번호를 들고 있어야 원고에서 바로 찾는다.</summary>
	public sealed class DialogueScriptIssue
	{
		public int LineNumber { get; }
		public string Message { get; }

		public DialogueScriptIssue(int lineNumber, string message)
		{
			LineNumber = lineNumber;
			Message = message;
		}
	}

	public sealed class ParsedDialogueScript
	{
		public List<DialogueScriptSection> Sections { get; } = new();
		public List<DialogueScriptIssue> Issues { get; } = new();

		/// <summary>
		/// 대사로 안 본 인용줄 — 오류가 아니라 **기록**이다. 원고엔 경구·메모가 섞여 있어서
		/// 그걸 오류로 세면 진짜 오류가 묻힌다. 대신 「이만큼은 안 읽었다」를 사람이 볼 수 있게 남긴다.
		/// </summary>
		public List<DialogueScriptIssue> SkippedQuoteLines { get; } = new();

		public bool HasIssues => Issues.Count > 0;

		public DialogueScriptSection FindSection(string name)
		{
			for (int i = 0; i < Sections.Count; i++)
			{
				if (string.Equals(Sections[i].Name, name, StringComparison.Ordinal))
				{
					return Sections[i];
				}
			}
			return null;
		}
	}

}
