using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — **「그때 뭐라고 했나」** 기록의 회귀 잠금.
	///
	/// ★ 왜 「봤다」로는 부족한가: 서사에서 되짚는 건 본 적이 아니라 **한 말**이다.
	///   「그때 거절했잖아」는 대화를 봤는지가 아니라 무엇을 골랐는지를 묻는다.
	///   본 적만 남기면 어느 가지로 갔든 기록이 똑같아서 그런 대사를 쓸 수가 없다.
	///
	/// 저장 왕복은 여기 없다 — Json.NET·GameData 가 필요해 하네스 밖(유니티 CI)에서만 돈다.
	/// 그쪽은 `DialogueHistorySaveRoundTripTests` 에 있다. 갈라 둔 이유: 이 파일은 **에디터 없이도**
	/// 매 증분마다 돌아야 하고, 하나라도 게임 전역 타입을 물면 파일 전체가 안 돈다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueChoiceMemoryTest
	{
		private const string NL = "\n";
		private const int TALK_ID = 5200;

		[Test]
		public void ChosenLabelIsRemembered()
		{
			DialogueHistory history = new();
			history.MarkChoice(TALK_ID, "거절한다");

			Assert.That(history.HasChosen(TALK_ID, "거절한다"), Is.True);
			Assert.That(history.HasChosen(TALK_ID, "받는다"), Is.False);
			Assert.That(history.HasSeen(TALK_ID, DialogueSeenKind.Started), Is.True,
				"골랐으면 시작한 것이다");
		}

		[Test]
		public void SameDialogueCanRememberSeveralAnswers()
		{
			// 대화 하나에 선택지 묶음이 여럿일 수 있다 — 나중 답이 앞 답을 지우면 안 된다.
			DialogueHistory history = new();
			history.MarkChoice(TALK_ID, "거절한다");
			history.MarkChoice(TALK_ID, "그래도 물어본다");

			Assert.That(history.HasChosen(TALK_ID, "거절한다"), Is.True);
			Assert.That(history.HasChosen(TALK_ID, "그래도 물어본다"), Is.True);
		}

		[Test]
		public void AnswersDoNotLeakBetweenDialogues()
		{
			DialogueHistory history = new();
			history.MarkChoice(TALK_ID, "거절한다");

			Assert.That(history.HasChosen(TALK_ID + 1, "거절한다"), Is.False);
		}

		[Test]
		public void EmptyLabelIsNotRecorded()
		{
			DialogueHistory history = new();
			history.MarkChoice(TALK_ID, null);
			history.MarkChoice(TALK_ID, string.Empty);

			Assert.That(history.HasChosen(TALK_ID, string.Empty), Is.False);
			Assert.That(history.HasSeen(TALK_ID, DialogueSeenKind.Started), Is.False,
				"아무것도 안 고른 것을 「시작했다」로 세면 안 된다");
		}

		[Test]
		public void PlaybackTellsWhichAnswerWasPicked()
		{
			// 재생기는 대화 번호를 모른다 — 「무슨 일이 있었는지」만 말하고 기록은 부르는 쪽이 한다.
			DialoguePlayback playback = new(DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(
				string.Join(NL,
					"## 물어보기",
					"> - 거절한다 -> 끝",
					"> - 받는다 -> 끝",
					"## 끝",
					"> 욘: \"그래.\""))));

			string picked = null;
			playback.OnChoiceSelected += label => picked = label;
			playback.Begin();
			playback.SubmitChoice(1);

			Assert.That(picked, Is.EqualTo("받는다"));
		}

		[Test]
		public void RejectedChoiceTellsNothing()
		{
			DialoguePlayback playback = new(DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(
				string.Join(NL,
					"## 물어보기",
					"> - 거절한다 -> 끝",
					"## 끝",
					"> 욘: \"그래.\""))));

			int calls = 0;
			playback.OnChoiceSelected += _ => calls++;
			playback.Begin();
			playback.SubmitChoice(7);

			Assert.That(calls, Is.EqualTo(0), "안 고른 것을 골랐다고 말하면 이력이 거짓이 된다");
		}
	}
}
