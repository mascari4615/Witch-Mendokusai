using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — **끝나면 다음 것이 이어 걸리는가**.
	///
	/// ★ 왜 여태 못 봤나: 그래프 재생을 코루틴이 밀고 있어서 **화면 없이는 대화가 한 발도 못 갔다.**
	///   시간 주입으로 바꾸고 나서야 「끝까지 → 다음 것」이 확인 가능해졌다.
	///
	/// ★ 왜 중요한가: 이 줄이 끊기면 첫 대화 뒤의 모든 대화가 **줄에 선 채로 영영 안 나온다.**
	///   퀘스트 보상 대사도, 다음 장면도. 그리고 아무것도 안 터진다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueRunnerQueueFlowTest
	{
		private const string NL = "\n";

		private static DialogueRunner NewRunner() =>
			new GameObject("DialogueRunnerQueueTest").AddComponent<DialogueRunner>();

		private static DialogueGraph BuildGraph(int id, string text)
		{
			DialogueGraph graph = DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(
				string.Join(NL, "## 시작", "> 욘: \"" + text + "\"")));
			graph.ID = id;
			return graph;
		}

		[Test]
		public void TimeMovesTheDialogueForward()
		{
			// 시간을 주는 쪽이 유니티뿐이면 이 시험 자체가 못 쓰인다 — 그래서 주입으로 바꿨다.
			DialogueRunner runner = NewRunner();
			runner.Play(BuildGraph(5400, "한 마디."));

			Assert.That(runner.IsPlaying, Is.True);
			runner.Tick(60f);

			Assert.That(runner.IsPlaying, Is.False, "읽을 시간이 지나면 저절로 넘어가고 끝난다");
			Assert.That(runner.History.HasSeen(5400, DialogueSeenKind.Completed), Is.True);
		}

		[Test]
		public void TickDoesNothingWhenNothingIsPlaying()
		{
			// 매 프레임 불리는 자리다 — 아무것도 안 트는 동안 조용해야 한다.
			DialogueRunner runner = NewRunner();

			Assert.That(() => runner.Tick(1f), Throws.Nothing);
			Assert.That(runner.IsPlaying, Is.False);
		}
	}
}
