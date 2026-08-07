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
			Assert.That(parsed.Sections[0].Entries[1].Text, Is.EqualTo("(이불 속) \"...\""),
				"지문은 버리지 않는다 — 연출 정보다");
			Assert.That(parsed.HasIssues, Is.False);
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
		public void ChoiceWithoutTarget_IsReported()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 시작",
				"> - 그냥 라벨만 있는 선택지"));

			Assert.That(parsed.Issues.Count, Is.EqualTo(1));
			Assert.That(parsed.Issues[0].LineNumber, Is.EqualTo(2));
		}

		[Test]
		public void SpeakerlessQuoteLine_IsReported()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse("> 이건 그냥 인용문이다");

			Assert.That(parsed.Issues.Count, Is.EqualTo(1), "누가 말하는지 없는 인용줄은 조용히 버리지 않는다");
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
