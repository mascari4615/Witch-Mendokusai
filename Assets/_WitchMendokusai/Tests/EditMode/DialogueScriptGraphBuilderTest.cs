using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 대본 → 그래프 세우기의 회귀 잠금. **글만 써서 대화가 실제로 흐르는지**를 본다
	/// (자산 파일 0개 · 에디터 0회). 세우고 나서 그냥 재생해 보는 것이 제일 확실한 검증이라 그렇게 한다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueScriptGraphBuilderTest
	{
		private static DialoguePlayback PlayScript(string scriptText)
		{
			DialogueGraph graph = DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(scriptText));
			DialoguePlayback playback = new(graph);
			playback.Begin();
			return playback;
		}

		[Test]
		public void WrittenScript_PlaysLineByLine()
		{
			DialoguePlayback playback = PlayScript(string.Join("\n",
				"### 장면 3 — 알리사 등장",
				"문 두드리는 소리.",
				"> 알리사: \"주인님, 아침입니다.\"",
				"> 욘: \"귀찮아.\""));

			Assert.That(playback.CurrentLine.Text, Is.EqualTo("주인님, 아침입니다."));
			Assert.That(playback.CurrentLine.ResolveSpeakerName(), Is.EqualTo("알리사"));

			playback.Advance();
			Assert.That(playback.CurrentLine.Text, Is.EqualTo("귀찮아."));

			playback.Advance();
			Assert.That(playback.IsPlaying, Is.False, "원고가 끝나면 대화도 끝난다");
		}

		[Test]
		public void SectionsFallThroughInWrittenOrder()
		{
			DialoguePlayback playback = PlayScript(string.Join("\n",
				"## 첫째",
				"> 욘: \"하나\"",
				"## 둘째",
				"> 욘: \"둘\""));

			Assert.That(playback.CurrentLine.Text, Is.EqualTo("하나"));
			playback.Advance();
			Assert.That(playback.CurrentLine.Text, Is.EqualTo("둘"), "장면이 끝나면 다음 장면으로 그냥 넘어간다");
		}

		[Test]
		public void GotoJumpsAndSkipsWhatIsBetween()
		{
			DialoguePlayback playback = PlayScript(string.Join("\n",
				"## 시작",
				"> 욘: \"간다\"",
				"> -> 끝",
				"## 중간",
				"> 욘: \"여긴 안 들린다\"",
				"## 끝",
				"> 욘: \"도착\""));

			Assert.That(playback.CurrentLine.Text, Is.EqualTo("간다"));
			playback.Advance();
			playback.Tick(0.1f);
			Assert.That(playback.CurrentLine.Text, Is.EqualTo("도착"), "건너뛰기는 중간 장면을 통과한다");
		}

		[Test]
		public void ChoicePicksTheWrittenBranch()
		{
			DialoguePlayback playback = PlayScript(string.Join("\n",
				"## 물어보기",
				"> 링: \"무슨 일 있었어?\"",
				"> - 응, 좀. -> 사정설명",
				"> - 아니. -> 끝인사",
				"## 사정설명",
				"> 욘: \"별거 아니야.\"",
				"> -> 끝인사",
				"## 끝인사",
				"> 링: \"그래!\""));

			playback.Advance();
			Assert.That(playback.CurrentChoices.Count, Is.EqualTo(2));
			Assert.That(playback.CurrentChoices[0], Is.EqualTo("응, 좀."));

			Assert.That(playback.SubmitChoice(1), Is.True);
			Assert.That(playback.CurrentLine.Text, Is.EqualTo("그래!"), "두 번째를 고르면 끝인사로 간다");
		}

		[Test]
		public void WaitIsBuiltFromScript()
		{
			DialoguePlayback playback = PlayScript(string.Join("\n",
				"## 시작",
				"> wait 2s",
				"> 욘: \"이제 말한다\""));

			Assert.That(playback.Current.Kind, Is.EqualTo(DialogueStepKind.Wait));
			playback.Tick(1.9f);
			Assert.That(playback.Current.Kind, Is.EqualTo(DialogueStepKind.Wait), "아직 2초가 아니다");
			playback.Tick(0.2f);
			Assert.That(playback.CurrentLine.Text, Is.EqualTo("이제 말한다"));
		}

		[Test]
		public void BrokenTargetEndsInsteadOfCrashing()
		{
			DialoguePlayback playback = PlayScript(string.Join("\n",
				"## 시작",
				"> 욘: \"간다\"",
				"> -> 오타난장면"));

			playback.Advance();
			playback.Tick(0.1f);

			Assert.That(playback.IsPlaying, Is.False,
				"오타는 검사기가 줄 번호로 짚어 준다 — 재생은 조용히 끝나되 터지지는 않는다");
		}

		[Test]
		public void EmptyScript_BuildsPlayableEmptyGraph()
		{
			DialogueGraph graph = DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(""));
			DialoguePlayback playback = new(graph);

			playback.Begin();

			Assert.That(playback.IsPlaying, Is.False);
		}
	}
}
