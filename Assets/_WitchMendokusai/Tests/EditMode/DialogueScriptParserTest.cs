using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 사람이 쓴 대본을 읽는 규칙의 회귀 잠금.
	///
	/// 대본은 `memo/wm/design/narrative/` 원고 모양 그대로다(`&gt; 이름: "대사"` + `### 장면`).
	/// 여기서 제일 중요한 것: **원고에 섞인 산문·지시문을 대사로 착각하지 않는 것**과
	/// **오타 난 장면 이름을 줄 번호와 함께 짚어 주는 것**(런타임엔 그냥 대화가 조용히 끝나 버린다).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueScriptParserTest
	{
		private const string NL = "\n";

		[Test]
		public void QuotedLines_BecomeSpeakEntries_AndProseIsIgnored()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"### 장면 3 — 알리사 등장",
				"",
				"문 두드리는 소리. 쟁반에 차를 들고 옴.",
				"",
				"> 알리사: \"주인님, 아침입니다.\"",
				"> 욘: (이불 속) \"...\"",
				"",
				"알리사가 차를 탁자에 내려놓고 나간다."));

			Assert.That(parsed.Sections.Count, Is.EqualTo(1));
			Assert.That(parsed.Sections[0].Name, Is.EqualTo("장면 3 — 알리사 등장"));
			Assert.That(parsed.Sections[0].Entries.Count, Is.EqualTo(2), "산문 두 줄은 대사가 아니다");
			Assert.That(parsed.Sections[0].Entries[0].Speaker, Is.EqualTo("알리사"));
			Assert.That(parsed.Sections[0].Entries[0].Text, Is.EqualTo("주인님, 아침입니다."), "따옴표는 벗긴다");
			Assert.That(parsed.Sections[0].Entries[1].StageDirection, Is.EqualTo("(이불 속)"),
				"지문은 버리지 않되 말과 섞지 않는다");
			Assert.That(parsed.Sections[0].Entries[1].Text, Is.EqualTo("..."), "말풍선엔 말만 들어간다");
			Assert.That(parsed.HasIssues, Is.False);
		}

		[Test]
		public void StageDirection_IsSplitFromSpokenText()
		{
			// 실측(2026-08-08): 원고에 흔한 모양 — 「욘: (한숨) "응."」.
			// 안 떼면 말풍선에 「(한숨) "응."」 이 통째로 뜬다.
			ParsedDialogueScript parsed = DialogueScriptParser.Parse("> 욘: (한숨) \"응.\"");

			DialogueScriptEntry entry = parsed.Sections[0].Entries[0];
			Assert.That(entry.StageDirection, Is.EqualTo("(한숨)"));
			Assert.That(entry.Text, Is.EqualTo("응."));
		}

		[Test]
		public void StageDirectionOnly_IsStillALine()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse("> 욘: (오래 바라본다)");

			DialogueScriptEntry entry = parsed.Sections[0].Entries[0];
			Assert.That(entry.StageDirection, Is.EqualTo("(오래 바라본다)"));
			Assert.That(entry.Text, Is.Empty, "말 없는 지문 줄도 연출이다 — 버리지 않는다");
			Assert.That(parsed.HasIssues, Is.False);
		}

		[Test]
		public void EmphasisMarkersAreStripped()
		{
			// 원고: `> *"우리는 진짜야?"*` — 별표는 글쓰기 표기지 대사 글자가 아니다.
			ParsedDialogueScript parsed = DialogueScriptParser.Parse("> *\"우리는 진짜야?\"*");

			Assert.That(parsed.Sections[0].Entries[0].Text, Is.EqualTo("우리는 진짜야?"));
		}

		[Test]
		public void Choices_GroupIntoOneEntry_AndKeepTargets()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 물어보기",
				"> 링: \"무슨 일 있었어?\"",
				"> - 응, 좀. -> 사정설명",
				"> - 아니. -> 끝인사",
				"## 사정설명",
				"> 욘: \"별거 아니야.\"",
				"## 끝인사",
				"> 링: \"그래!\""));

			DialogueScriptEntry choice = parsed.Sections[0].Entries[1];
			Assert.That(choice.Kind, Is.EqualTo(DialogueScriptEntryKind.Choice));
			Assert.That(choice.Choices.Count, Is.EqualTo(2), "잇달아 나온 선택지는 한 묶음");
			Assert.That(choice.Choices[0].Label, Is.EqualTo("응, 좀."));
			Assert.That(choice.Choices[1].TargetSection, Is.EqualTo("끝인사"));
			Assert.That(parsed.HasIssues, Is.False);
		}

		[Test]
		public void UnknownSectionTarget_IsReportedWithLineNumber()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 시작",
				"> 욘: \"가자.\"",
				"> -> 없는장면"));

			Assert.That(parsed.Issues.Count, Is.EqualTo(1));
			Assert.That(parsed.Issues[0].LineNumber, Is.EqualTo(3), "원고에서 바로 찾을 수 있어야 한다");
			Assert.That(parsed.Issues[0].Message.Contains("없는장면"), Is.True);
		}

		[Test]
		public void DuplicateSectionName_IsReported()
		{
			// 찾는 쪽은 첫 번째를 집고 세우는 쪽은 마지막으로 가면, 검사는 A 를 보고 재생은 B 로 간다.
			// 「### 만남」 같은 흔한 제목은 두 번 쓰기 쉽다.
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 만남",
				"> 욘: \"처음\"",
				"## 만남",
				"> 욘: \"두 번째\""));

			Assert.That(parsed.Issues.Count, Is.EqualTo(1));
			Assert.That(parsed.Issues[0].LineNumber, Is.EqualTo(3), "겹친 쪽(나중 것)의 줄을 짚는다");
		}

		[Test]
		public void DuplicateSectionName_JumpGoesToTheFirst()
		{
			DialogueGraph graph = DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(string.Join("\n",
				"## 시작",
				"> -> 만남",
				"## 만남",
				"> 욘: \"처음\"",
				"## 만남",
				"> 욘: \"두 번째\"")));
			DialoguePlayback playback = new(graph);

			playback.Begin();
			playback.Tick(0.1f);

			Assert.That(playback.CurrentLine.Text, Is.EqualTo("처음"),
				"이름으로 찾는 쪽과 실제로 가는 곳이 같아야 한다 — 어느 쪽이든 하나로 정해져야 눈으로 쫓을 수 있다");
		}

		[Test]
		public void DuplicateChoiceLabel_IsReported()
		{
			// 복사해 붙이고 라벨 고치는 걸 잊은 경우 — 플레이어 눈엔 똑같은 두 칸이 뜨고,
			// 무엇이 다른지는 고르고 나서야 안다.
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join(NL,
				"## 물음",
				"> - 간다 -> 가기",
				"> - 간다 -> 남기",
				"## 가기",
				"> 욘: \"응\"",
				"## 남기",
				"> 욘: \"아니\""));

			Assert.That(parsed.Issues.Count, Is.EqualTo(1));
			Assert.That(parsed.Issues[0].Message.Contains("겹친다"), Is.True);
		}

		[Test]
		public void JumpToEmptySection_IsReported()
		{
			// 빈 장면 자체는 흠이 아니다(산문만 있는 장면은 원고에 흔하다).
			// 하지만 거기로 **보내면** 아무 말 없이 다음 장면으로 흘러간다 — 쓴 사람 의도와 화면이 갈린다.
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join(NL,
				"## 시작",
				"> -> 빈곳",
				"## 빈곳",
				"카메라가 방을 훑는다.",
				"## 끝",
				"> 욘: \"끝\""));

			Assert.That(parsed.Issues.Count, Is.EqualTo(1));
			Assert.That(parsed.Issues[0].LineNumber, Is.EqualTo(2), "보내는 줄을 짚는다");
		}

		[Test]
		public void EmptySectionAlone_IsNotAnIssue()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join(NL,
				"## 장면 1",
				"카메라가 방을 천천히 훑는다. 나레이션 없음.",
				"## 장면 2",
				"> 욘: \"...\""));

			Assert.That(parsed.HasIssues, Is.False, "산문만 있는 장면은 원고에서 정상이다");
		}

		[Test]
		public void ChoiceWithoutTarget_IsReported()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 시작",
				"> - 그냥 라벨만 있는 선택지"));

			Assert.That(parsed.Issues.Count, Is.EqualTo(1));
			Assert.That(parsed.Issues[0].LineNumber, Is.EqualTo(2));
		}

		[Test]
		public void QuotedLineWithoutSpeaker_IsNarration()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse("> \"우리는 진짜야?\"");

			Assert.That(parsed.Sections[0].Entries.Count, Is.EqualTo(1));
			Assert.That(parsed.Sections[0].Entries[0].Speaker, Is.Null, "말하는 이 없는 대사 = 나레이션");
			Assert.That(parsed.Sections[0].Entries[0].Text, Is.EqualTo("우리는 진짜야?"));
			Assert.That(parsed.HasIssues, Is.False);
		}

		[Test]
		public void ProseQuote_IsSkippedNotReportedAsError()
		{
			// 실측(2026-08-08): 원고의 인용줄 절반은 경구·메모였다. 그걸 오류로 세면 진짜 오류가 묻힌다.
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 도토리",
				"> 도토리는 땅에 묻혀서 나무가 된다.",
				"> 욘: \"그런가.\""));

			Assert.That(parsed.HasIssues, Is.False, "대사가 아닌 인용줄은 오류가 아니다");
			Assert.That(parsed.SkippedQuoteLines.Count, Is.EqualTo(1), "다만 안 읽었다는 사실은 남긴다");
			Assert.That(parsed.SkippedQuoteLines[0].LineNumber, Is.EqualTo(2));
			Assert.That(parsed.Sections[0].Entries.Count, Is.EqualTo(1), "대사는 하나만 읽힌다");
		}

		[Test]
		public void RealOpeningExcerpt_ParsesCleanly()
		{
			// `memo/wm/design/narrative/opening.md` 에서 그대로 떼어 온 조각 — 원고 모양이 바뀌면 여기서 걸린다.
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"### 장면 3 — 알리사 등장",
				"",
				"문 두드리는 소리. 쟁반에 차를 들고 옴.",
				"",
				"> 알리사: \"주인님, 아침입니다.\"",
				"> 욘: (이불 속) \"...\"",
				"> 알리사: \"오늘 할 일이 있습니다.\"",
				"> 욘: \"귀찮아.\"",
				"",
				"알리사가 차를 탁자에 내려놓고 나간다."));

			Assert.That(parsed.HasIssues, Is.False);
			Assert.That(parsed.Sections[0].Entries.Count, Is.EqualTo(4));
			Assert.That(parsed.Sections[0].Entries[3].Speaker, Is.EqualTo("욘"));
		}

		[Test]
		public void Waits_AreReadInBothLanguages()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 시작",
				"> wait 2s",
				"> 기다림 1.5초",
				"> wait event boss-defeated",
				"> 기다림 사건 문열림"));

			Assert.That(parsed.Sections[0].Entries.Count, Is.EqualTo(4));
			Assert.That(parsed.Sections[0].Entries[0].Seconds, Is.EqualTo(2f));
			Assert.That(parsed.Sections[0].Entries[1].Seconds, Is.EqualTo(1.5f));
			Assert.That(parsed.Sections[0].Entries[2].EventId, Is.EqualTo("boss-defeated"));
			Assert.That(parsed.Sections[0].Entries[3].EventId, Is.EqualTo("문열림"));
			Assert.That(parsed.HasIssues, Is.False);
		}

		[Test]
		public void ScriptWithoutHeading_StillParses()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse("> 욘: \"귀찮아.\"");

			Assert.That(parsed.Sections.Count, Is.EqualTo(1), "소제목 없이 대사부터 쓰는 원고도 있다");
			Assert.That(parsed.Sections[0].Entries.Count, Is.EqualTo(1));
		}

		[Test]
		public void EmptyText_YieldsNothing()
		{
			Assert.That(DialogueScriptParser.Parse("").Sections.Count, Is.Zero);
			Assert.That(DialogueScriptParser.Parse(null).Sections.Count, Is.Zero);
		}
	}
}
