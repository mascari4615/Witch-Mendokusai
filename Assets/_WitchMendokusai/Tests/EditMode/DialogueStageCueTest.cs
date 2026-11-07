using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	// 무대 신호 문법: 읽기, 오류, 되쓰기, 효과와 분리
	public sealed class DialogueStageCueTest
	{
		private static ParsedDialogueScript Parse(params string[] lines) =>
			DialogueScriptParser.Parse(string.Join("\n", lines));

		private static DialogueStageCue FirstCue(ParsedDialogueScript parsed)
		{
			foreach (DialogueScriptSection section in parsed.Sections)
			{
				foreach (DialogueScriptEntry entry in section.Entries)
				{
					if (entry.Kind == DialogueScriptEntryKind.Stage)
					{
						return entry.Cue;
					}
				}
			}
			return null;
		}

		[Test]
		public void EnterCue_ReadsActorSlotAndEntrance()
		{
			DialogueStageCue cue = FirstCue(Parse("## 시작", "> @등장 욘 오른쪽 페이드", "> 욘: \"응.\""));

			Assert.That(cue.Kind, Is.EqualTo(DialogueStageCueKind.Enter));
			Assert.That(cue.Actor, Is.EqualTo("욘"));
			Assert.That(cue.Slot, Is.EqualTo(DialogueStageSlot.Right));
			Assert.That(cue.Entrance, Is.EqualTo(DialogueStageEntrance.Fade));
		}

		[Test]
		public void UnknownCommand_IsAnIssueWithLineNumber()
		{
			ParsedDialogueScript parsed = Parse("## 시작", "> @표정 욘 졸림", "> 욘: \"응.\"");

			Assert.That(parsed.Issues.Count, Is.EqualTo(1));
			Assert.That(parsed.Issues[0].LineNumber, Is.EqualTo(2));
			Assert.That(FirstCue(parsed), Is.Null);
		}

		[Test]
		public void UnknownSlotName_IsAnIssue()
		{
			ParsedDialogueScript parsed = Parse("## 시작", "> @이동 욘 저쪽", "> 욘: \"응.\"");
			Assert.That(parsed.Issues.Count, Is.EqualTo(1));
		}

		[Test]
		public void AtLine_IsNotCountedAsSkippedQuote()
		{
			ParsedDialogueScript parsed = Parse("## 시작", "> @방식 미연시", "> 욘: \"응.\"");
			Assert.That(parsed.SkippedQuoteLines.Count, Is.EqualTo(0));
			Assert.That(parsed.Issues.Count, Is.EqualTo(0));
		}

		[Test]
		public void EveryCue_SurvivesWriteAndRead()
		{
			string[] cues =
			{
				"방식 미연시", "방식 자막", "방식 필드", "방식 말풍선",
				"등장 링 왼쪽 슬라이드", "퇴장 링 페이드", "퇴장 모두 슬라이드", "이동 링 오른쪽끝",
				"강조 링", "강조 끔", "들썩임 링", "끄덕임 링", "점프 링", "흔들림 링",
				"아이콘 링 놀람", "샷 플레이어 클로즈", "샷 끝", "이펙트 반짝 링", "이펙트 플래시", "띠 켬", "띠 끔",
			};
			foreach (string text in cues)
			{
				Assert.That(DialogueStageCueText.TryRead(text, out DialogueStageCue cue, out string problem), Is.True, problem);
				Assert.That(DialogueStageCueText.Write(cue), Is.EqualTo(text));
			}
		}

		[Test]
		public void WriterKeepsCuesInOrder()
		{
			ParsedDialogueScript parsed = Parse("## 시작", "> @방식 필드", "> 링: \"앗.\"", "> @흔들림 링", "> 욘: \"응.\"");
			string written = DialogueScriptWriter.Write(parsed);
			Assert.That(written.IndexOf("> @방식 필드") < written.IndexOf("> 링:"), Is.True);
			Assert.That(written.IndexOf("> @흔들림 링") < written.IndexOf("> 욘:"), Is.True);
		}

		[Test]
		public void CueBetweenEffects_KeepsStreamOrder()
		{
			ParsedDialogueScript parsed = Parse("## 시작", "> !아이템 1001", "> @점프 링", "> !아이템 1002", "> 링: \"자.\"");
			List<DialogueScriptEntry> entries = parsed.Sections[0].Entries;

			Assert.That(entries[0].Kind, Is.EqualTo(DialogueScriptEntryKind.Effect));
			Assert.That(entries[1].Kind, Is.EqualTo(DialogueScriptEntryKind.Stage));
			Assert.That(entries[2].Kind, Is.EqualTo(DialogueScriptEntryKind.Effect));
		}

		[Test]
		public void CueAfterGoto_IsADeadLine()
		{
			ParsedDialogueScript parsed = Parse("## 시작", "> -> 끝", "> @점프 링", "## 끝", "> 링: \"끝.\"");
			Assert.That(parsed.Issues.Count, Is.EqualTo(1));
		}
	}
}
