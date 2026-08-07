using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 로그가 **플레이어가 고른 답**도 남기는지.
	///
	/// ★ 왜 필요한가: 로그를 되짚는 이유의 절반은 「내가 뭐라고 했더라」다.
	///   대사만 남기면 대화가 왜 그쪽으로 흘렀는지 로그만 봐서는 알 수 없다.
	///   그리고 남의 대사와 **섞어서** 남기면, 나중에 화면을 만들 때 다시 갈라낼 방법이 없다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueTranscriptChoiceTest
	{
		[Test]
		public void ChosenAnswerIsRecorded()
		{
			DialogueTranscript transcript = new();
			transcript.RecordChoice("거절한다");

			Assert.That(transcript.Count, Is.EqualTo(1));
			Assert.That(transcript.Last.Text, Is.EqualTo("거절한다"));
		}

		[Test]
		public void ChosenAnswerIsMarkedApartFromSpokenLines()
		{
			DialogueTranscript transcript = new();
			transcript.RecordChoice("거절한다");

			Assert.That(transcript.Last.IsChoice, Is.True,
				"섞어서 남기면 나중에 화면을 만들 때 다시 갈라낼 방법이 없다");
			Assert.That(transcript.Last.Speaker, Is.Null,
				"고른 사람은 화면 밖이다 — 이름을 붙이면 원고에 없는 화자가 로그에 생긴다");
		}

		[Test]
		public void EmptyAnswerIsNotRecorded()
		{
			DialogueTranscript transcript = new();
			transcript.RecordChoice(null);
			transcript.RecordChoice("   ");

			Assert.That(transcript.Count, Is.EqualTo(0), "로그에 빈칸이 쌓이면 되짚기가 더 어려워진다");
		}

		[Test]
		public void ChoicesAlsoFallOffWhenFull()
		{
			// 가득 차면 오래된 것부터 버린다 — 고른 답이라고 눌러앉으면 꼬리가 밀려난다.
			DialogueTranscript transcript = new(2);
			transcript.RecordChoice("하나");
			transcript.RecordChoice("둘");
			transcript.RecordChoice("셋");

			Assert.That(transcript.Count, Is.EqualTo(2));
			Assert.That(transcript.Entries[0].Text, Is.EqualTo("둘"));
			Assert.That(transcript.Last.Text, Is.EqualTo("셋"));
		}
	}
}
