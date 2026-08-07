using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 러너가 **고른 답을 실제로 남기는지**.
	///
	/// ★ 왜 러너까지 시험하나: 이력·로그·재생기 각각은 다 잠겨 있었는데, **잇는 줄**은 아무도 안 봤다.
	///   구독 한 줄을 빠뜨리면 세 부품 모두 초록인 채로 기능만 조용히 사라진다.
	///   실제로 이 줄들은 하루 동안 어떤 검사도 안 받고 있었다(하네스 밖 + 단일 파일 검사기 사각).
	///
	/// 재생·연출은 안 본다(코루틴·시간은 여기 관심사가 아니다). 「고르면 남는가」 하나만 본다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueRunnerChoiceWiringTest
	{
		private const string NL = "\n";
		private const int GRAPH_ID = 5300;

		private static DialogueGraph BuildGraph(params string[] lines)
		{
			DialogueGraph graph = DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(string.Join(NL, lines)));
			graph.ID = GRAPH_ID;
			return graph;
		}

		private static DialogueRunner NewRunner() => new GameObject("DialogueRunnerTest").AddComponent<DialogueRunner>();

		private static DialogueGraph BuildGraph()
		{
			DialogueGraph graph = DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(string.Join(NL,
				"## 물어보기",
				"> - 거절한다 -> 끝",
				"> - 받는다 -> 끝",
				"## 끝",
				"> 욘: \"그래.\"")));
			graph.ID = GRAPH_ID;
			return graph;
		}

		[Test]
		public void ChoosingAnAnswer_LeavesItInHistoryAndTheLog()
		{
			GameObject host = new("DialogueRunnerTest");
			DialogueRunner runner = host.AddComponent<DialogueRunner>();

			runner.Play(BuildGraph(), null);
			Assert.That(runner.IsPlaying, Is.True, "Play 직후엔 재생 중이어야 한다");
			Assert.That(runner.CurrentChoices, Is.Not.Null, "첫 스텝이 선택지여야 한다");
			Assert.That(runner.SubmitChoice(0), Is.True);

			Assert.That(runner.History.HasChosen(GRAPH_ID, "거절한다"), Is.True,
				"고른 답을 안 남기면 「그때 거절했잖아」 조건이 영영 안 맞는다");
			// 고른 답 **다음에** 그 가지의 대사가 바로 이어 찍힌다 — 로그 순서는 「고른 답 → 그 뒤 대사」다.
			// (마지막 줄만 보고 「고른 답이 없다」고 오해하기 쉬운 자리라 순서째로 잠근다.)
			DialogueTranscript.Entry chosenEntry = runner.Transcript.Entries[runner.Transcript.Count - 2];
			Assert.That(chosenEntry.IsChoice, Is.True);
			Assert.That(chosenEntry.Text, Is.EqualTo("거절한다"));
			Assert.That(runner.Transcript.Last.IsChoice, Is.False, "그 뒤 대사는 고른 답이 아니다");

			Object.DestroyImmediate(host);
		}

		[Test]
		public void PlayingWithoutTheUnityLifecycle_StillWiresTheQueue()
		{
			// 러너가 귀를 붙이는 자리가 Awake 뿐이었다면, 그 전에 대화를 걸면 조정자가 터진다
			// (터지게 만들어 뒀다 — 예전엔 조용히 사라졌다). 거는 자리에서도 붙이므로 안 터져야 한다.
			DialogueRunner runner = NewRunner();

			Assert.That(() => runner.Play(ScriptableObject.CreateInstance<DialogueLine>()), Throws.Nothing);
		}

		[Test]
		public void SecondDialogueWaitsInLine_InsteadOfOverwriting()
		{
			// 말하는 중에 또 걸면 **덮어쓰지 않고 줄을 선다** — 퀘스트 보상 대사가 인사말에 먹히면 안 된다.
			DialogueRunner runner = NewRunner();
			runner.Play(ScriptableObject.CreateInstance<DialogueLine>());
			runner.Play(ScriptableObject.CreateInstance<DialogueLine>());

			Assert.That(runner.PendingCount, Is.EqualTo(1), "둘째는 기다린다");
		}

		[Test]
		public void ListeningToTheEnd_LeavesItAsHeard()
		{
			// 「끝까지 들었다」를 남기는 자리도 잇는 줄이다 — 재생기는 끝났다고만 말하고,
			// 그걸 이력에 옮기는 건 러너다. 그 줄이 빠져도 부품은 전부 초록이다.
			DialogueRunner runner = NewRunner();
			runner.Play(BuildGraph("## 시작", "> 욘: \"하나.\"", "> 욘: \"둘.\""));
			runner.Skip();

			Assert.That(runner.IsPlaying, Is.False);
			Assert.That(runner.History.HasSeen(GRAPH_ID, DialogueSeenKind.Completed), Is.True);
		}

		[Test]
		public void StoppingHalfway_DoesNotCountAsHeard()
		{
			// 중간에 접은 대화는 다음에 다시 보여줘야 한다 — 「시작했다」와 「들었다」가 달라야 하는 이유.
			DialogueRunner runner = NewRunner();
			runner.Play(BuildGraph("## 시작", "> 욘: \"하나.\"", "> 욘: \"둘.\""));
			runner.Stop();

			Assert.That(runner.IsPlaying, Is.False);
			Assert.That(runner.History.HasSeen(GRAPH_ID, DialogueSeenKind.Started), Is.True);
			Assert.That(runner.History.HasSeen(GRAPH_ID, DialogueSeenKind.Completed), Is.False);
		}

		[Test]
		public void SkipStopsAtAChoice_SoThePlayerStillPicks()
		{
			// 러너의 건너뛰기가 재생기 규칙을 그대로 물려받는지 — 대신 골라 주면 안 된다.
			DialogueRunner runner = NewRunner();
			runner.Play(BuildGraph(
				"## 시작",
				"> 욘: \"하나.\"",
				"> - 왼쪽 -> 끝",
				"> - 오른쪽 -> 끝",
				"## 끝",
				"> 욘: \"끝.\""));

			Assert.That(runner.Skip(), Is.GreaterThan(0));
			Assert.That(runner.CurrentChoices, Is.Not.Null);
			Assert.That(runner.History.HasSeen(GRAPH_ID, DialogueSeenKind.Completed), Is.False,
				"아직 안 끝났다 — 건너뛰기가 「들었다」를 앞당기면 안 된다");
		}
	}
}
