using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 지나간 대사 기록의 회귀 잠금.
	///
	/// 잠그는 것: ① 말한 순서대로 남는다 ② 말 없는 줄(지문만·빈 대사)은 안 남는다
	/// ③ 가득 차면 **오래된 것부터** 버린다(로그는 꼬리가 중요하다 — 차례 세우기와 반대 판단)
	/// ④ 원고에서 만든 줄의 이름도 제대로 남는다(자산 화자가 없어도).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueTranscriptTest
	{
		[Test]
		public void RecordsInOrder()
		{
			DialogueTranscript transcript = new();
			transcript.Record(DialogueLine.CreateRuntime("알리사", "주인님, 아침입니다."));
			transcript.Record(DialogueLine.CreateRuntime("욘", "귀찮아."));

			Assert.That(transcript.Count, Is.EqualTo(2));
			Assert.That(transcript.Entries[0].Speaker, Is.EqualTo("알리사"));
			Assert.That(transcript.Entries[1].Text, Is.EqualTo("귀찮아."));
			Assert.That(transcript.Last.Text, Is.EqualTo("귀찮아."));
		}

		[Test]
		public void SkipsLinesWithoutWords()
		{
			DialogueTranscript transcript = new();
			transcript.Record(null);
			transcript.Record(DialogueLine.CreateRuntime("욘", ""));
			transcript.Record(DialogueLine.CreateRuntime("욘", "   "));
			transcript.Record(DialogueLine.CreateRuntime("욘", null, 0f, "(오래 바라본다)"));

			Assert.That(transcript.Count, Is.Zero, "로그에 빈칸이 쌓이면 읽을 수가 없다");
		}

		[Test]
		public void WhenFull_TheOldestIsDropped()
		{
			DialogueTranscript transcript = new(2);
			transcript.Record(DialogueLine.CreateRuntime("욘", "하나"));
			transcript.Record(DialogueLine.CreateRuntime("욘", "둘"));
			transcript.Record(DialogueLine.CreateRuntime("욘", "셋"));

			Assert.That(transcript.Count, Is.EqualTo(2));
			Assert.That(transcript.Entries[0].Text, Is.EqualTo("둘"), "로그는 꼬리가 중요하다");
			Assert.That(transcript.Last.Text, Is.EqualTo("셋"));
		}

		[Test]
		public void CapacityBelowOne_StillKeepsOne()
		{
			DialogueTranscript transcript = new(0);
			transcript.Record(DialogueLine.CreateRuntime("욘", "하나"));

			Assert.That(transcript.Count, Is.EqualTo(1), "잘못 설정해도 아무것도 안 남는 로그가 되면 안 된다");
		}

		[Test]
		public void Clear_EmptiesIt()
		{
			DialogueTranscript transcript = new();
			transcript.Record(DialogueLine.CreateRuntime("욘", "하나"));

			transcript.Clear();

			Assert.That(transcript.Count, Is.Zero);
			Assert.That(transcript.Last.Text, Is.Null);
		}

		[Test]
		public void ScriptWrittenLines_KeepTheirName()
		{
			DialogueGraph graph = DialogueScriptGraphBuilder.Build(
				DialogueScriptParser.Parse("> 링: \"넷째!\""));
			DialoguePlayback playback = new(graph);
			DialogueTranscript transcript = new();
			playback.OnStepChanged += step =>
			{
				if (step.Kind == DialogueStepKind.Speak)
				{
					transcript.Record(step.SpeakLine);
				}
			};

			playback.Begin();

			Assert.That(transcript.Last.Speaker, Is.EqualTo("링"),
				"원고에서 만든 줄은 화자 자산이 없다 — 이름만 들고 온다");
			Assert.That(transcript.Last.Text, Is.EqualTo("넷째!"));
		}
	}
}
