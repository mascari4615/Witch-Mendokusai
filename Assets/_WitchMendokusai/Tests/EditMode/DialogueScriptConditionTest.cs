using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 원고에 적은 조건이 실제로 걸리는지의 회귀 잠금.
	///
	/// 조건 기능(분기·조건 선택지·대화 이력)은 다 있었지만 **원고로는 쓸 수가 없었다** =
	/// 손으로 노드를 놓는 사람만 쓸 수 있는 기능이었다. 그 구멍을 막은 자리라, 여기서 잠그는 건
	/// 「글자로 쓴 조건 → 진짜 조건 객체 → 실제 흐름 변화」의 **한 줄 전체**다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueScriptConditionTest
	{
		private const int GREETING_ID = 4615;

		private static DialoguePlayback PlayScript(string scriptText)
		{
			DialogueGraph graph = DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(scriptText));
			DialoguePlayback playback = new(graph);
			playback.Begin();
			return playback;
		}

		[Test]
		public void ConditionalJump_TakesBranchOnlyWhenConditionHolds()
		{
			string script = string.Join("\n",
				"## 만남",
				"> ?봤음 4615 -> 또봄",
				"> 욘: \"...누구야.\"",
				"> -> 끝",
				"## 또봄",
				"> 욘: \"또 왔네.\"",
				"## 끝",
				"> 링: \"가자!\"");

			DialogueHistory history = new();
			DialogueHistoryBridge.Register(history);
			try
			{
				Assert.That(PlayScript(script).CurrentLine.Text, Is.EqualTo("...누구야."),
					"아직 안 봤으면 조건이 안 맞아 다음 줄로 흐른다");

				history.MarkCompleted(GREETING_ID);

				Assert.That(PlayScript(script).CurrentLine.Text, Is.EqualTo("또 왔네."),
					"보고 나면 같은 원고가 다른 길로 간다");
			}
			finally
			{
				DialogueHistoryBridge.Clear(history);
			}
		}

		[Test]
		public void ConditionalChoice_IsHiddenUntilConditionHolds()
		{
			string script = string.Join("\n",
				"## 물어보기",
				"> 링: \"뭐 할래?\"",
				"> - 그냥 간다 -> 끝",
				"> - 그 얘기 다시 해줘 [봤음 4615] -> 다시",
				"## 다시",
				"> 욘: \"또?\"",
				"## 끝",
				"> 링: \"응.\"");

			DialogueHistory history = new();
			DialogueHistoryBridge.Register(history);
			try
			{
				DialoguePlayback before = PlayScript(script);
				before.Advance();
				Assert.That(before.CurrentChoices.Count, Is.EqualTo(1), "조건이 안 맞는 선택지는 안 뜬다");

				history.MarkCompleted(GREETING_ID);

				DialoguePlayback after = PlayScript(script);
				after.Advance();
				Assert.That(after.CurrentChoices.Count, Is.EqualTo(2));
				Assert.That(after.SubmitChoice(1), Is.True);
				Assert.That(after.CurrentLine.Text, Is.EqualTo("또?"), "늦게 열린 선택지도 제 가지로 간다");
			}
			finally
			{
				DialogueHistoryBridge.Clear(history);
			}
		}

		[Test]
		public void UnseenAndStarted_AreBothUnderstood()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 시작",
				"> ?안봤음 1 -> 시작",
				"> ?시작함 2 -> 시작",
				"> ?seen 3 -> 시작",
				"> ?unseen 4 -> 시작"));

			Assert.That(parsed.HasIssues, Is.False, "한국어와 영어를 둘 다 읽는다");
			Assert.That(parsed.Sections[0].Entries.Count, Is.EqualTo(4));
			Assert.That(parsed.Sections[0].Entries[0].Condition.Expected, Is.False);
			Assert.That(parsed.Sections[0].Entries[1].Condition.Started, Is.True);
		}

		[Test]
		public void UnknownCondition_IsReportedAndDoesNotLockTheChoice()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 시작",
				"> - 라벨 [모르는조건 7] -> 시작"));

			Assert.That(parsed.Issues.Count, Is.EqualTo(1));
			Assert.That(parsed.Issues[0].LineNumber, Is.EqualTo(2));

			DialogueScriptChoice choice = parsed.Sections[0].Entries[0].Choices[0];
			Assert.That(choice.Condition.HasCondition, Is.False,
				"조용히 잠가 버리면 「왜 이 선택지가 안 뜨지」를 영영 못 찾는다 — 조건 없음으로 두고 알린다");
			Assert.That(choice.Label, Is.EqualTo("라벨"), "대괄호는 라벨에서 떼어낸다");
		}

		[Test]
		public void ConditionalJumpWithoutTarget_IsReported()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 시작",
				"> ?봤음 4615"));

			Assert.That(parsed.Issues.Count, Is.EqualTo(1));
		}
	}
}
