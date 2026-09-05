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

	/// <summary>
	/// 사람이 쓴 대본을 읽는다 (TASK-WM-052).
	///
	/// ★ 왜 새 형식을 안 만들었나: 사용자는 **이미** `memo/wm/design/narrative/` 에 정해진 모양으로
	///   대사를 쓰고 있다(`> 이름: "대사"` 인용줄 + `### 장면 N — 제목` 소제목). 새 문법을 발명하면
	///   그 원고들이 전부 「변환 대상」이 된다. 그래서 **쓰고 있던 모양을 그대로 읽는다.**
	///
	/// 읽는 것:
	/// <list type="bullet">
	/// <item><c>## 제목</c> / <c>### 장면 1 — 욘의 방</c> → 장면(점프 대상). 제목이 곧 이름이다.</item>
	/// <item><c>&gt; 욘: "귀찮아."</c> → 말하기. 따옴표는 벗긴다.</item>
	/// <item><c>&gt; 욘: (한숨) "응."</c> → **지문은 따로 담는다**(말풍선엔 말만 들어간다).</item>
	/// <item><c>&gt; - 응, 좀. -&gt; 사정설명</c> → 선택지 한 칸. 연달아 오면 한 묶음이 된다.</item>
	/// <item><c>&gt; -&gt; 끝인사</c> → 그 장면으로 건너뛰기.</item>
	/// <item><c>&gt; ?안봤음 4615 -&gt; 첫인사</c> → **조건이 맞을 때만** 그 장면으로(아니면 다음 줄로).</item>
	/// <item><c>&gt; - 열쇠를 보여준다 [봤음 4615] -&gt; 장면</c> → 조건이 맞을 때만 **보이는** 선택지.</item>
	/// <item><c>[아이템 1001]</c> · <c>[아이템 1001 3]</c> · <c>[아이템없음 1001]</c> → 물건을 가졌는지로 가른다.</item>
	/// <item><c>[퀘스트완료 5000]</c> · <c>[퀘스트미완 5000]</c> · <c>[퀘스트열림 5000]</c> → 의뢰 진행으로 가른다.</item>
	/// <item><c>&gt; !아이템 1001 3</c> → 실제로 일으킨다(아이템·카드·퀘스트추가·퀘스트열기·레시피).</item>
	/// <item><c>&gt; 기다림 2초</c> / <c>&gt; wait 2s</c> → 시간 대기.</item>
	/// <item><c>&gt; 기다림 사건 boss-defeated</c> / <c>&gt; wait event boss-defeated</c> → 사건 대기.</item>
	/// <item><c>&gt; "우리는 진짜야?"</c> → 이름 없이 따옴표만 있으면 **나레이션**(말하는 이 없음).</item>
	/// <item>그 밖의 줄(산문·지시문·문서 인용) → **무시하고 세어만 둔다**(<see cref="ParsedDialogueScript.SkippedQuoteLines"/>).
	///   실측(2026-08-08): 원고의 인용줄 절반은 대사가 아니라 경구·메모였다. 그걸 오류라고 하면
	///   **진짜 오류가 그 소음에 묻힌다.**</item>
	/// </list>
	///
	/// 순수 함수 — 파일 입출력도 Unity 의존도 없다. 문자열만 받는다.
	/// </summary>
	public static class DialogueScriptParser
	{
		private const string DEFAULT_SECTION_NAME = "(시작)";
		private const string GOTO_ARROW = "->";

		public static ParsedDialogueScript Parse(string scriptText)
		{
			ParsedDialogueScript parsed = new();
			if (string.IsNullOrEmpty(scriptText))
			{
				return parsed;
			}

			DialogueScriptSection current = null;
			List<DialogueScriptChoice> pendingChoices = null;
			int pendingChoiceLine = 0;
			List<EffectInfoData> pendingEffects = null;
			int pendingEffectLine = 0;

			string[] lines = scriptText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				int lineNumber = i + 1;
				string line = lines[i].Trim();

				if (line.StartsWith("#", StringComparison.Ordinal))
				{
					FlushChoices(current, ref pendingChoices, pendingChoiceLine);
					FlushEffects(current, ref pendingEffects, pendingEffectLine);
					current = new DialogueScriptSection(ReadHeading(line), lineNumber);
					parsed.Sections.Add(current);
					continue;
				}

				if (line.StartsWith(">", StringComparison.Ordinal) == false)
				{
					continue;
				}

				string body = line.Substring(1).Trim();
				if (body.Length == 0)
				{
					continue;
				}

				current = EnsureSection(parsed, current, lineNumber);

				if (body.StartsWith("-", StringComparison.Ordinal) && body.StartsWith(GOTO_ARROW, StringComparison.Ordinal) == false)
				{
					string choiceBody = body.Substring(1).Trim();
					int arrow = choiceBody.IndexOf(GOTO_ARROW, StringComparison.Ordinal);
					if (arrow < 0)
					{
						parsed.Issues.Add(new DialogueScriptIssue(lineNumber,
							$"선택지에 갈 곳이 없다(`-> 장면이름` 이 빠졌다): \"{choiceBody}\""));
						continue;
					}

					string labelPart = choiceBody.Substring(0, arrow).Trim();
					DialogueScriptCondition condition = ReadBracketCondition(parsed, ref labelPart, lineNumber);

					pendingChoices ??= new List<DialogueScriptChoice>();
					if (pendingChoices.Count == 0)
					{
						pendingChoiceLine = lineNumber;
					}
					pendingChoices.Add(new DialogueScriptChoice(
						StripQuotes(labelPart),
						choiceBody.Substring(arrow + GOTO_ARROW.Length).Trim(),
						condition));
					continue;
				}

				FlushChoices(current, ref pendingChoices, pendingChoiceLine);
				if (body.StartsWith("!", StringComparison.Ordinal) == false)
				{
					FlushEffects(current, ref pendingEffects, pendingEffectLine);
				}

				if (body.StartsWith(GOTO_ARROW, StringComparison.Ordinal))
				{
					current.Entries.Add(DialogueScriptEntry.Goto(lineNumber, body.Substring(GOTO_ARROW.Length).Trim()));
					continue;
				}

				// `!아이템 1001 3` — 물건·퀘스트 같은 것을 일으킨다. 잇달아 오면 한 묶음이 된다.
				if (body.StartsWith("!", StringComparison.Ordinal))
				{
					string effectBody = body.Substring(1).Trim();
					if (TryReadEffect(effectBody, out EffectInfoData effectData) == false)
					{
						parsed.Issues.Add(new DialogueScriptIssue(lineNumber, $"모르는 효과다: \"{effectBody}\""));
						continue;
					}

					pendingEffects ??= new List<EffectInfoData>();
					if (pendingEffects.Count == 0)
					{
						pendingEffectLine = lineNumber;
					}
					pendingEffects.Add(effectData);
					continue;
				}

				// `?봤음 4615 -> 이미본장면` — 조건이 맞으면 그 장면으로, 아니면 다음 줄로.
				if (body.StartsWith("?", StringComparison.Ordinal))
				{
					string conditionBody = body.Substring(1).Trim();
					int conditionArrow = conditionBody.IndexOf(GOTO_ARROW, StringComparison.Ordinal);
					if (conditionArrow < 0)
					{
						parsed.Issues.Add(new DialogueScriptIssue(lineNumber,
							$"조건부 건너뛰기에 갈 곳이 없다(`-> 장면이름` 이 빠졌다): \"{conditionBody}\""));
						continue;
					}

					string conditionText = conditionBody.Substring(0, conditionArrow).Trim();
					if (TryReadCondition(conditionText, out DialogueScriptCondition jumpCondition) == false)
					{
						parsed.Issues.Add(new DialogueScriptIssue(lineNumber, $"모르는 조건이다: \"{conditionText}\""));
						continue;
					}
					current.Entries.Add(DialogueScriptEntry.ConditionalGoto(
						lineNumber, conditionBody.Substring(conditionArrow + GOTO_ARROW.Length).Trim(), jumpCondition));
					continue;
				}

				if (TryReadWait(body, lineNumber, out DialogueScriptEntry waitEntry, out string waitProblem))
				{
					current.Entries.Add(waitEntry);
					continue;
				}
				if (waitProblem != null)
				{
					parsed.Issues.Add(new DialogueScriptIssue(lineNumber, waitProblem));
					continue;
				}

				int colon = body.IndexOf(':');
				if (colon <= 0)
				{
					// 이름이 없다 — 따옴표로 감싼 것만 나레이션으로 본다. 나머지는 대사가 아니다(경구·메모·문서 인용).
					if (TryStripQuotes(StripEmphasis(body), out string narration) && narration.Length > 0)
					{
						current.Entries.Add(DialogueScriptEntry.Speak(lineNumber, null, narration));
						continue;
					}
					parsed.SkippedQuoteLines.Add(new DialogueScriptIssue(lineNumber, body));
					continue;
				}

				string speaker = body.Substring(0, colon).Trim();
				string spoken = SplitStageDirection(body.Substring(colon + 1).Trim(), out string stageDirection);
				string text = StripQuotes(spoken);
				if (text.Length == 0 && string.IsNullOrEmpty(stageDirection))
				{
					parsed.Issues.Add(new DialogueScriptIssue(lineNumber, $"대사가 비었다: \"{body}\""));
					continue;
				}
				current.Entries.Add(DialogueScriptEntry.Speak(lineNumber, speaker, text, stageDirection));
			}

			FlushChoices(current, ref pendingChoices, pendingChoiceLine);
			FlushEffects(current, ref pendingEffects, pendingEffectLine);
			ValidateSectionNames(parsed);
			ValidateChoiceLabels(parsed);
			ValidateSpeakerNames(parsed);
			ValidateTargets(parsed);
			ValidateReachableSections(parsed);
			ValidateChoicesHaveAWayOut(parsed);
			ValidateNoDeadEntries(parsed);
			return parsed;
		}

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

		/// <summary>
		/// 앞머리의 `(지문)` 을 떼어낸다 — 「욘: (한숨) "응."」. 실측(2026-08-08): 원고에 흔한 모양이다.
		/// 안 떼면 **말풍선에 「(한숨) "응."」 이 통째로 뜬다** — 지문은 말이 아니다.
		/// 지문만 있고 말이 없는 줄(「(오래 바라본다)」)도 정상으로 본다.
		/// </summary>
		private static string SplitStageDirection(string text, out string stageDirection)
		{
			stageDirection = null;
			string trimmed = StripEmphasis(text);
			if (trimmed.StartsWith("(", StringComparison.Ordinal) == false)
			{
				return trimmed;
			}

			int close = trimmed.IndexOf(')');
			if (close < 0)
			{
				return trimmed;
			}

			stageDirection = trimmed.Substring(0, close + 1).Trim();
			return trimmed.Substring(close + 1).Trim();
		}

		/// <summary>`*기울임*` 표시를 걷어낸다 — 원고의 강조는 글쓰기 표기지 대사 글자가 아니다.</summary>
		private static string StripEmphasis(string text)
		{
			string trimmed = text.Trim();
			while (trimmed.Length >= 2 && trimmed[0] == '*' && trimmed[trimmed.Length - 1] == '*')
			{
				trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
			}
			return trimmed;
		}

		/// <summary>따옴표(곧은 것·굽은 것 양쪽)로 감싼 대사는 벗긴다 — 원고는 둘을 섞어 쓴다.</summary>
		private static string StripQuotes(string text) => TryStripQuotes(text, out string stripped) ? stripped : text;

		/// <summary>
		/// 감싼 따옴표를 벗겼으면 true. 「벗겨졌는가」 자체가 판단 근거라서 따로 낸다 —
		/// 이름 없는 인용줄이 *대사인지 경구인지* 를 이걸로 가른다.
		/// </summary>
		private static bool TryStripQuotes(string text, out string stripped)
		{
			stripped = text;
			if (text.Length < 2)
			{
				return false;
			}
			char first = text[0];
			char last = text[text.Length - 1];
			bool straight = first == '"' && last == '"';
			bool curly = first == '“' && last == '”';
			if (straight == false && curly == false)
			{
				return false;
			}
			stripped = text.Substring(1, text.Length - 2).Trim();
			return true;
		}

		/// <summary>
		/// 같은 이름의 장면이 둘 이상인가.
		///
		/// ★ 왜 오류인가: 이름으로 찾는 쪽(<see cref="ParsedDialogueScript.FindSection"/>)은 **첫 번째**를 집고,
		///   그래프를 세우는 쪽은 나중 것으로 덮어써 **마지막**으로 간다. 즉 검사는 A 를 보고 실제 재생은 B 로 간다 —
		///   눈으로는 「분명 저 장면을 가리켰는데 다른 대사가 나온다」로만 보이는 종류다.
		///   원고에서 「### 만남」 같은 흔한 제목을 두 번 쓰기 쉬우므로 반드시 잡아야 한다.
		/// </summary>
		private static void ValidateSectionNames(ParsedDialogueScript parsed)
		{
			HashSet<string> seen = new(StringComparer.Ordinal);
			for (int i = 0; i < parsed.Sections.Count; i++)
			{
				DialogueScriptSection section = parsed.Sections[i];
				if (seen.Add(section.Name))
				{
					continue;
				}
				parsed.Issues.Add(new DialogueScriptIssue(section.LineNumber,
					$"장면 이름이 겹친다: \"{section.Name}\" — 가리키는 쪽과 실제로 가는 곳이 달라진다"));
			}
		}

		/// <summary>
		/// 한 묶음 안에 **같은 라벨의 선택지**가 둘 이상인가.
		/// 플레이어 눈엔 똑같은 두 칸이 뜬다 — 무엇이 다른지 알 길이 없고, 고르고 나서야 다른 데로 간다.
		/// 대개 복사해 붙이고 라벨 고치는 걸 잊은 것이다.
		/// </summary>
		private static void ValidateChoiceLabels(ParsedDialogueScript parsed)
		{
			for (int s = 0; s < parsed.Sections.Count; s++)
			{
				List<DialogueScriptEntry> entries = parsed.Sections[s].Entries;
				for (int e = 0; e < entries.Count; e++)
				{
					if (entries[e].Kind != DialogueScriptEntryKind.Choice)
					{
						continue;
					}

					HashSet<string> labels = new(StringComparer.Ordinal);
					IReadOnlyList<DialogueScriptChoice> choices = entries[e].Choices;
					for (int c = 0; c < choices.Count; c++)
					{
						if (labels.Add(choices[c].Label))
						{
							continue;
						}
						parsed.Issues.Add(new DialogueScriptIssue(entries[e].LineNumber,
							$"선택지 라벨이 겹친다: \"{choices[c].Label}\" — 플레이어 눈엔 같은 칸이 둘이다"));
					}
				}
			}
		}

		/// <summary>
		/// 화자 이름 오타로 **보이는** 것을 짚는다 — 「욘」을 「온」이라 쓴 경우.
		///
		/// ★ 왜 필요한가: 이름이 틀려도 **아무 일도 안 일어난다.** 그냥 다른 사람이 말한 게 되고,
		///   그 이름으로 등록된 캐릭터가 없으니 말풍선이 엉뚱한 자리(카메라 앞)에 뜬다.
		///   원고에도 게임에도 오류가 안 남아서 눈으로만 잡아야 하는 종류다.
		///
		/// 판정은 **보수적으로**: 그 원고에서 **딱 한 번** 나온 이름이면서, 여러 번 나온 이름과
		/// **글자 하나 차이**일 때만 묻는다. 새 등장인물을 오타라고 우기면 그 검사는 곧 무시당한다.
		///
		/// ★ 실측(2026-08-08)으로 배운 것: **두 글자 미만 이름은 아예 안 본다.**
		///   「욘」과 「링」은 서로 글자 하나 차이라, 짧은 이름끼리는 이 규칙이 **아무 뜻이 없다**
		///   (실제로 멀쩡한 원고 둘을 오타라고 잡았다). 그래서 「온」 같은 진짜 오타도 놓치지만,
		///   정상을 잡는 검사보다 낫다.
		/// </summary>
		private static void ValidateSpeakerNames(ParsedDialogueScript parsed)
		{
			Dictionary<string, int> counts = new(StringComparer.Ordinal);
			Dictionary<string, int> firstLine = new(StringComparer.Ordinal);

			for (int s = 0; s < parsed.Sections.Count; s++)
			{
				List<DialogueScriptEntry> entries = parsed.Sections[s].Entries;
				for (int e = 0; e < entries.Count; e++)
				{
					DialogueScriptEntry entry = entries[e];
					if (entry.Kind != DialogueScriptEntryKind.Speak || string.IsNullOrEmpty(entry.Speaker))
					{
						continue;
					}
					counts.TryGetValue(entry.Speaker, out int count);
					counts[entry.Speaker] = count + 1;
					if (firstLine.ContainsKey(entry.Speaker) == false)
					{
						firstLine[entry.Speaker] = entry.LineNumber;
					}
				}
			}

			foreach (KeyValuePair<string, int> rare in counts)
			{
				if (rare.Value != 1 || rare.Key.Length < 2)
				{
					continue;
				}
				foreach (KeyValuePair<string, int> common in counts)
				{
					if (common.Value < 2 || common.Key.Length < 2 || IsOneEditApart(rare.Key, common.Key) == false)
					{
						continue;
					}
					parsed.Issues.Add(new DialogueScriptIssue(firstLine[rare.Key],
						$"\"{rare.Key}\" 는 이 원고에 한 번뿐인데 \"{common.Key}\" 와 글자 하나 차이다 — 오타인가?"));
					break;
				}
			}
		}

		/// <summary>글자 하나 차이인가(바꿈·넣음·뺌 한 번). 같은 글자면 false — 오타 후보가 아니다.</summary>
		private static bool IsOneEditApart(string left, string right)
		{
			if (left == right)
			{
				return false;
			}
			if (Math.Abs(left.Length - right.Length) > 1)
			{
				return false;
			}

			string shorter = left.Length <= right.Length ? left : right;
			string longer = left.Length <= right.Length ? right : left;

			int shortIndex = 0;
			int longIndex = 0;
			bool usedEdit = false;
			while (shortIndex < shorter.Length && longIndex < longer.Length)
			{
				if (shorter[shortIndex] == longer[longIndex])
				{
					shortIndex++;
					longIndex++;
					continue;
				}
				if (usedEdit)
				{
					return false;
				}
				usedEdit = true;
				if (shorter.Length == longer.Length)
				{
					shortIndex++;
				}
				longIndex++;
			}
			return true;
		}

		/// <summary>
		/// 한 장면 안에서 **이미 다른 데로 보낸 뒤에 더 쓴 줄**을 짚는다 — 그 줄부터는 절대 안 나온다.
		///
		/// ★ 왜 생기나: 갈 곳(<c>-&gt; 어디</c>)이나 선택지는 그 자리에서 흐름을 **가로챈다.**
		///   그 아래 대사는 다음 장면 것이 아니라 **아무 데도 아닌 글**이 된다.
		///   대개 선택지를 위로 옮기거나, 장면을 나눠야 할 걸 안 나눈 경우다.
		///
		/// ★ 왜 아무도 못 잡나: 도달 검사(<see cref="ValidateReachableSections"/>)는 **장면 단위**라
		///   여길 못 본다 — 장면 자체는 멀쩡히 도달하고, 그 안의 뒷줄만 굶는다.
		///   화면엔 아무 흔적이 없다. 「분명 썼는데 안 나온다」로만 보인다.
		///
		/// 조건부 갈 곳은 세지 않는다 — 조건이 거짓이면 그대로 아래로 읽어 내려가니까.
		/// </summary>
		private static void ValidateNoDeadEntries(ParsedDialogueScript parsed)
		{
			for (int s = 0; s < parsed.Sections.Count; s++)
			{
				List<DialogueScriptEntry> entries = parsed.Sections[s].Entries;
				for (int e = 0; e < entries.Count - 1; e++)
				{
					DialogueScriptEntryKind kind = entries[e].Kind;
					if (kind != DialogueScriptEntryKind.Goto && kind != DialogueScriptEntryKind.Choice)
					{
						continue;
					}

					string reason = kind == DialogueScriptEntryKind.Goto ? "갈 곳" : "선택지";
					parsed.Issues.Add(new DialogueScriptIssue(entries[e + 1].LineNumber,
						$"이 줄부터는 절대 안 나온다 — 위 {reason}에서 이미 다른 데로 보냈다. 장면을 나눠라"));
					break;
				}
			}
		}

		/// <summary>
		/// **전부 조건부인 선택지 묶음**을 짚는다 — 조건이 다 거짓이면 아무 칸도 안 뜬다.
		///
		/// ★ 그때 무슨 일이 일어나나: 재생 쪽은 **대화를 그냥 끝낸다.** 그게 옳은 처리다 —
		///   고를 게 없는 화면에서 플레이어를 붙잡아 두는 것보다 낫다. 하지만 원고를 쓴 사람은
		///   그 규칙을 모른다. 눈에는 「말하다 말고 대화가 툭 끊긴다」로만 보이고,
		///   **재현도 안 된다**(조건이 하나라도 참인 저장에서는 멀쩡히 돌아가니까).
		///
		/// 그래서 쓰는 자리에서 알린다. 고치는 법은 하나 — **조건 없는 칸을 하나 두는 것**
		/// (「그냥 간다」 같은 퇴로). 잡아 두는 게 아니라 나갈 문을 만들어 두라는 뜻이다.
		/// </summary>
		private static void ValidateChoicesHaveAWayOut(ParsedDialogueScript parsed)
		{
			for (int s = 0; s < parsed.Sections.Count; s++)
			{
				List<DialogueScriptEntry> entries = parsed.Sections[s].Entries;
				for (int e = 0; e < entries.Count; e++)
				{
					DialogueScriptEntry entry = entries[e];
					if (entry.Kind != DialogueScriptEntryKind.Choice || entry.Choices.Count == 0)
					{
						continue;
					}

					bool hasUnconditionalChoice = false;
					for (int c = 0; c < entry.Choices.Count; c++)
					{
						if (entry.Choices[c].Condition.HasCondition)
						{
							continue;
						}
						hasUnconditionalChoice = true;
						break;
					}

					if (hasUnconditionalChoice)
					{
						continue;
					}

					parsed.Issues.Add(new DialogueScriptIssue(entry.LineNumber,
						"선택지가 전부 조건부다 — 조건이 다 거짓이면 아무 칸도 안 뜨고 대화가 그냥 끝난다. 조건 없는 칸을 하나 둬라"));
				}
			}
		}

		/// <summary>
		/// **아무도 안 부르는 장면**을 짚는다 — 써 두었지만 게임에서 절대 안 나오는 글.
		///
		/// ★ 왜 필요한가: 이건 **아무 증상이 없다.** 원고도 멀쩡하고, 그래프도 멀쩡하고, 게임도 안 터진다.
		///   그냥 그 장면이 조용히 빠질 뿐이다. 대개 「-> 재회」 라고 쓸 걸 「-> 재회2」 로 쓰거나
		///   가지를 옮기다 보내는 쪽을 지운 경우다 — 쓴 사람은 다 이어 놨다고 믿는다.
		///   갈 곳 오타(<see cref="ValidateTargets"/>)는 **보내는 쪽**을 보고, 이건 **받는 쪽**을 본다.
		///
		/// 길은 둘이다: ① 누군가 이름으로 가리킨다(갈 곳·조건부 갈 곳·선택지)
		/// ② **앞 장면에서 그냥 흘러 들어온다** — 종이에 쓴 순서대로 읽히니까.
		/// 흘러가지 *못하는* 경우는 그 장면 안에 조건 없는 「갈 곳」이나 선택지가 있을 때다(거기서 새 버린다).
		/// 조건부 갈 곳은 안 세는데, 조건이 거짓이면 그대로 흘러가기 때문이다.
		/// </summary>
		private static void ValidateReachableSections(ParsedDialogueScript parsed)
		{
			if (parsed.Sections.Count == 0)
			{
				return;
			}

			HashSet<string> reachable = new(StringComparer.Ordinal);
			Queue<int> pending = new();
			// 첫 장면은 대화가 시작되는 자리다 — 아무도 안 가리켜도 도달한 것으로 친다.
			MarkReachable(parsed, 0, reachable, pending);

			while (pending.Count > 0)
			{
				int index = pending.Dequeue();
				DialogueScriptSection section = parsed.Sections[index];
				bool flowsToNext = true;

				for (int e = 0; e < section.Entries.Count; e++)
				{
					DialogueScriptEntry entry = section.Entries[e];
					if (entry.Kind == DialogueScriptEntryKind.Goto || entry.Kind == DialogueScriptEntryKind.ConditionalGoto)
					{
						MarkReachable(parsed, IndexOfSection(parsed, entry.TargetSection), reachable, pending);
						if (entry.Kind == DialogueScriptEntryKind.Goto)
						{
							// 조건 없는 갈 곳에서 흐름이 샌다 — 이 뒤로는 다음 장면까지 못 간다.
							flowsToNext = false;
							break;
						}
						continue;
					}
					if (entry.Kind != DialogueScriptEntryKind.Choice)
					{
						continue;
					}
					for (int c = 0; c < entry.Choices.Count; c++)
					{
						MarkReachable(parsed, IndexOfSection(parsed, entry.Choices[c].TargetSection), reachable, pending);
					}
					// 선택지에서도 샌다 — 고르면 그리로 가고, 고를 게 없으면 대화가 끝난다.
					flowsToNext = false;
					break;
				}

				if (flowsToNext)
				{
					MarkReachable(parsed, index + 1, reachable, pending);
				}
			}

			for (int i = 0; i < parsed.Sections.Count; i++)
			{
				DialogueScriptSection section = parsed.Sections[i];
				if (reachable.Contains(section.Name))
				{
					continue;
				}
				parsed.Issues.Add(new DialogueScriptIssue(section.LineNumber,
					$"\"{section.Name}\" 장면으로 가는 길이 없다 — 써 두었지만 게임에선 한 번도 안 나온다"));
			}
		}

		private static void MarkReachable(ParsedDialogueScript parsed, int index, HashSet<string> reachable, Queue<int> pending)
		{
			if (index < 0 || index >= parsed.Sections.Count)
			{
				return;
			}
			if (reachable.Add(parsed.Sections[index].Name) == false)
			{
				return;
			}
			pending.Enqueue(index);
		}

		/// <summary>이름이 겹치면 앞의 것을 집는다 — 찾는 쪽(FindSection)과 같은 판단이라야 한다.</summary>
		private static int IndexOfSection(ParsedDialogueScript parsed, string name)
		{
			for (int i = 0; i < parsed.Sections.Count; i++)
			{
				if (string.Equals(parsed.Sections[i].Name, name, StringComparison.Ordinal))
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>갈 곳 이름이 실제 장면인지 — 오타는 여기서 잡아야 한다(런타임엔 그냥 대화가 끝나 버린다).</summary>
		private static void ValidateTargets(ParsedDialogueScript parsed)
		{
			for (int i = 0; i < parsed.Sections.Count; i++)
			{
				List<DialogueScriptEntry> entries = parsed.Sections[i].Entries;
				for (int e = 0; e < entries.Count; e++)
				{
					DialogueScriptEntry entry = entries[e];
					if (entry.Kind == DialogueScriptEntryKind.Goto || entry.Kind == DialogueScriptEntryKind.ConditionalGoto)
					{
						RequireSection(parsed, entry.TargetSection, entry.LineNumber);
						continue;
					}
					if (entry.Kind != DialogueScriptEntryKind.Choice)
					{
						continue;
					}
					for (int c = 0; c < entry.Choices.Count; c++)
					{
						RequireSection(parsed, entry.Choices[c].TargetSection, entry.LineNumber);
					}
				}
			}
		}

		private static void RequireSection(ParsedDialogueScript parsed, string name, int lineNumber)
		{
			DialogueScriptSection target = parsed.FindSection(name);
			if (target == null)
			{
				parsed.Issues.Add(new DialogueScriptIssue(lineNumber, $"그런 장면이 없다: \"{name}\""));
				return;
			}

			// 빈 장면 자체는 흠이 아니다(원고엔 산문만 있는 장면이 흔하다). 하지만 **거기로 보내면** 다르다 —
			// 아무 말도 없이 다음 장면으로 흘러가서, 쓴 사람은 「저기로 갔다」고 믿는데 화면은 딴 데를 보여준다.
			if (target.Entries.Count == 0)
			{
				parsed.Issues.Add(new DialogueScriptIssue(lineNumber,
					$"\"{name}\" 장면엔 대사가 없다 — 거기로 가면 그냥 다음 장면으로 흘러간다"));
			}
		}
	}
}
