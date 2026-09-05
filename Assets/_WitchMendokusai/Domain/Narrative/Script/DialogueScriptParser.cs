using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
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
	public static partial class DialogueScriptParser
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
	}
}
