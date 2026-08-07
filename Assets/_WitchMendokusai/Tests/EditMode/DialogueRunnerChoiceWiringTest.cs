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
	}
}
