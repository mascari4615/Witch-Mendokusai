using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 원고에서 「그때 뭐라고 했나」를 물을 수 있는지.
	///
	/// ★ 기억만 있고 물을 말이 없으면 없는 기능이다. 이력이 답을 기억하게 된 다음 칸.
	///
	/// 라벨이 **공백을 품는다**는 게 이 조건의 유일한 까다로움이다 — 다른 조건처럼 토큰으로 쪼개면
	/// 「그냥 간다」가 두 조각이 나고, 다시 붙이면 원고에 쓴 띄어쓰기와 달라져 영영 안 맞는다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueChosenConditionTest
	{
		private const string NL = "\n";
		private const int TALK_ID = 5200;

		private static DialogueScriptCondition ParseChoiceCondition(string conditionText)
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join(NL,
				"## 시작",
				"> - 물어본다 [" + conditionText + "] -> 시작",
				"> - 그냥 간다 -> 시작"));

			Assert.That(parsed.HasIssues, Is.False, "조건을 못 읽으면 걸림으로 남는다");
			return parsed.Sections[0].Entries[0].Choices[0].Condition;
		}

		[Test]
		public void WrittenChoiceMemoryBecomesARealCondition()
		{
			DialogueScriptCondition condition = ParseChoiceCondition("골랐음 5200 거절한다");

			Assert.That(condition.Kind, Is.EqualTo(DialogueScriptConditionKind.Chosen));
			Assert.That(condition.DialogueId, Is.EqualTo(TALK_ID));
			Assert.That(condition.Label, Is.EqualTo("거절한다"));
			Assert.That(condition.Expected, Is.True);
		}

		[Test]
		public void LabelWithSpacesSurvives()
		{
			DialogueScriptCondition condition = ParseChoiceCondition("골랐음 5200 그냥 간다");

			Assert.That(condition.Label, Is.EqualTo("그냥 간다"),
				"라벨은 사람이 쓴 문장이다 — 쪼갰다 붙이면 띄어쓰기가 달라져 영영 안 맞는다");
		}

		[Test]
		public void NotChosenIsInverted()
		{
			DialogueScriptCondition condition = ParseChoiceCondition("안골랐음 5200 거절한다");

			Assert.That(condition.Kind, Is.EqualTo(DialogueScriptConditionKind.Chosen));
			Assert.That(condition.Expected, Is.False);
		}

		[Test]
		public void ConditionAsksTheHistory()
		{
			DialogueHistory history = new();
			history.MarkChoice(TALK_ID, "거절한다");
			DialogueHistoryBridge.Register(history);
			try
			{
				DialogueChosenCriteria refused = new() { DialogueId = TALK_ID, Label = "거절한다" };
				DialogueChosenCriteria accepted = new() { DialogueId = TALK_ID, Label = "받는다" };

				Assert.That(refused.Evaluate(), Is.True);
				Assert.That(accepted.Evaluate(), Is.False);
			}
			finally
			{
				DialogueHistoryBridge.Clear(history);
			}
		}

		[Test]
		public void WithoutHistory_CountsAsNotChosen()
		{
			DialogueHistoryBridge.Clear(DialogueHistoryBridge.Current);
			DialogueChosenCriteria criteria = new() { DialogueId = TALK_ID, Label = "거절한다" };

			Assert.That(criteria.Evaluate(), Is.False,
				"모를 때는 덜 진행된 쪽으로 넘어진다 — 이 계열 전체의 규칙");
		}

		[Test]
		public void TheBranchIsActuallyTaken()
		{
			string script = string.Join(NL,
				"## 만남",
				"> ?골랐음 5200 거절한다 -> 서먹",
				"> 욘: \"안녕.\"",
				"## 서먹",
				"> 욘: \"…그때 그랬잖아.\"");

			DialogueHistory history = new();
			history.MarkChoice(TALK_ID, "거절한다");
			DialogueHistoryBridge.Register(history);
			try
			{
				DialoguePlayback playback = new(DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(script)));
				playback.Begin();
				Assert.That(playback.CurrentLine.Text, Is.EqualTo("…그때 그랬잖아."));
			}
			finally
			{
				DialogueHistoryBridge.Clear(history);
			}

			DialogueHistoryBridge.Clear(DialogueHistoryBridge.Current);
			DialoguePlayback fresh = new(DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(script)));
			fresh.Begin();
			Assert.That(fresh.CurrentLine.Text, Is.EqualTo("안녕."));
		}

		[Test]
		public void MissingLabel_IsReportedAsUnknownCondition()
		{
			// 번호만 적고 답을 안 적으면 무엇을 묻는지 알 수 없다 — 조용히 참/거짓으로 정하면 안 된다.
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join(NL,
				"## 시작",
				"> ?골랐음 5200 -> 시작"));

			Assert.That(parsed.HasIssues, Is.True);
		}
	}
}
