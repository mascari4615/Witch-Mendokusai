using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	public enum DialogueScriptEntryKind
	{
		Speak = 0,
		Choice = 1,
		Goto = 2,
		WaitTime = 3,
		WaitEvent = 4,
	}

	/// <summary>선택지 한 줄 — 라벨과 갈 곳(장면 이름).</summary>
	public readonly struct DialogueScriptChoice
	{
		public string Label { get; }
		public string TargetSection { get; }

		public DialogueScriptChoice(string label, string targetSection)
		{
			Label = label;
			TargetSection = targetSection;
		}
	}

	/// <summary>대본 한 줄이 뜻하는 것. 어느 줄에서 왔는지(<see cref="LineNumber"/>)를 끝까지 들고 다닌다.</summary>
	public sealed class DialogueScriptEntry
	{
		public DialogueScriptEntryKind Kind { get; }
		public int LineNumber { get; }
		public string Speaker { get; }
		public string Text { get; }
		public string TargetSection { get; }
		public float Seconds { get; }
		public string EventId { get; }
		public IReadOnlyList<DialogueScriptChoice> Choices { get; }

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

		public static DialogueScriptEntry Speak(int lineNumber, string speaker, string text) =>
			new(DialogueScriptEntryKind.Speak, lineNumber, speaker, text, null, 0f, null, null);
		public static DialogueScriptEntry Choice(int lineNumber, IReadOnlyList<DialogueScriptChoice> choices) =>
			new(DialogueScriptEntryKind.Choice, lineNumber, null, null, null, 0f, null, choices);
		public static DialogueScriptEntry Goto(int lineNumber, string targetSection) =>
			new(DialogueScriptEntryKind.Goto, lineNumber, null, null, targetSection, 0f, null, null);
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
	/// <item><c>&gt; 욘: (한숨) "응."</c> → 지문은 대사 앞에 그대로 남긴다(연출 정보 유실 X).</item>
	/// <item><c>&gt; - 응, 좀. -&gt; 사정설명</c> → 선택지 한 칸. 연달아 오면 한 묶음이 된다.</item>
	/// <item><c>&gt; -&gt; 끝인사</c> → 그 장면으로 건너뛰기.</item>
	/// <item><c>&gt; 기다림 2초</c> / <c>&gt; wait 2s</c> → 시간 대기.</item>
	/// <item><c>&gt; 기다림 사건 boss-defeated</c> / <c>&gt; wait event boss-defeated</c> → 사건 대기.</item>
	/// <item>그 밖의 줄(산문·지시문) → **무시한다.** 원고에는 카메라·음악 설명이 섞여 있고 그건 대사가 아니다.</item>
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

			string[] lines = scriptText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				int lineNumber = i + 1;
				string line = lines[i].Trim();

				if (line.StartsWith("#", StringComparison.Ordinal))
				{
					FlushChoices(current, ref pendingChoices, pendingChoiceLine);
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

					pendingChoices ??= new List<DialogueScriptChoice>();
					if (pendingChoices.Count == 0)
					{
						pendingChoiceLine = lineNumber;
					}
					pendingChoices.Add(new DialogueScriptChoice(
						StripQuotes(choiceBody.Substring(0, arrow).Trim()),
						choiceBody.Substring(arrow + GOTO_ARROW.Length).Trim()));
					continue;
				}

				FlushChoices(current, ref pendingChoices, pendingChoiceLine);

				if (body.StartsWith(GOTO_ARROW, StringComparison.Ordinal))
				{
					current.Entries.Add(DialogueScriptEntry.Goto(lineNumber, body.Substring(GOTO_ARROW.Length).Trim()));
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
					parsed.Issues.Add(new DialogueScriptIssue(lineNumber,
						$"누가 말하는지가 없다(`이름: 대사` 모양이어야 한다): \"{body}\""));
					continue;
				}

				string speaker = body.Substring(0, colon).Trim();
				string text = StripQuotes(body.Substring(colon + 1).Trim());
				if (text.Length == 0)
				{
					parsed.Issues.Add(new DialogueScriptIssue(lineNumber, $"대사가 비었다: \"{body}\""));
					continue;
				}
				current.Entries.Add(DialogueScriptEntry.Speak(lineNumber, speaker, text));
			}

			FlushChoices(current, ref pendingChoices, pendingChoiceLine);
			ValidateTargets(parsed);
			return parsed;
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

		/// <summary>따옴표(곧은 것·굽은 것 양쪽)로 감싼 대사는 벗긴다 — 원고는 둘을 섞어 쓴다.</summary>
		private static string StripQuotes(string text)
		{
			if (text.Length < 2)
			{
				return text;
			}
			char first = text[0];
			char last = text[text.Length - 1];
			bool straight = first == '"' && last == '"';
			bool curly = first == '“' && last == '”';
			return straight || curly ? text.Substring(1, text.Length - 2).Trim() : text;
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
					if (entry.Kind == DialogueScriptEntryKind.Goto)
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
			if (parsed.FindSection(name) != null)
			{
				return;
			}
			parsed.Issues.Add(new DialogueScriptIssue(lineNumber, $"그런 장면이 없다: \"{name}\""));
		}
	}
}
