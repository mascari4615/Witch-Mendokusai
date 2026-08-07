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
		public void WhenOneEnds_TheNextInLineStartsByItself()
		{
			// 이 줄이 끊기면 첫 대화 뒤의 모든 대화가 **줄에 선 채로 영영 안 나온다** — 그리고 아무것도 안 터진다.
			// 오늘 아침까지는 시간을 유니티만 줄 수 있어서 이걸 볼 방법 자체가 없었다.
			DialogueRunner runner = NewRunner();
			runner.Play(BuildGraph(5401, "첫째."));
			runner.Play(ScriptableObject.CreateInstance<DialogueLine>());

			Assert.That(runner.PendingCount, Is.EqualTo(1), "둘째는 기다린다");

			runner.Tick(60f);

			Assert.That(runner.PendingCount, Is.EqualTo(0), "첫째가 끝나면 줄이 빠진다");
			Assert.That(runner.IsPlaying, Is.True, "둘째가 저절로 걸린다");
			Assert.That(runner.History.HasSeen(5401, DialogueSeenKind.Completed), Is.True);
		}

		[Test]
		public void TheQueueDrainsToTheEnd()
		{
			// 줄이 끝까지 빠지는지 — 마지막 것까지 끝나야 조용해진다.
			DialogueRunner runner = NewRunner();
			runner.Play(BuildGraph(5402, "첫째."));
			runner.Play(BuildGraph(5403, "둘째."));

			runner.Tick(60f);
			runner.Tick(60f);

			Assert.That(runner.IsPlaying, Is.False);
			Assert.That(runner.PendingCount, Is.EqualTo(0));
			Assert.That(runner.History.HasSeen(5403, DialogueSeenKind.Completed), Is.True,
				"둘째도 끝까지 갔다");
		}

		[Test]
		public void CapacitiesComeFromTheInspector_NotFromTheField()
		{
			// 수치는 인스펙터에서 만질 수 있어야 한다(WM 규칙). 그런데 그 값은 **컴포넌트가 만들어진 뒤에**
			// 채워지므로, 담는 그릇을 필드 초기화로 만들면 조절해도 안 먹는다.
			// 여기서는 기본값이 실제로 그릇에 전달되는지만 본다 — 전달 경로가 끊기면 이 값도 안 맞는다.
			DialogueRunner runner = NewRunner();

			for (int i = 0; i < DialogueTranscript.DEFAULT_CAPACITY + 5; i++)
			{
				runner.Transcript.RecordChoice("답 " + i);
			}

			Assert.That(runner.Transcript.Count, Is.EqualTo(DialogueTranscript.DEFAULT_CAPACITY),
				"넘치면 오래된 것부터 버린다 — 그릇 크기가 전달됐다는 뜻");
		}

		[Test]
		public void AwakeActuallyRuns_SoItsWiringIsChecked()
		{
			// 붙는 즉시 도는 코드가 검사에서만 안 돌면 두 쪽이 다른 물건이 된다.
			// 여기서는 「Awake 가 돌았다」의 눈에 보이는 증거 하나만 본다 — 자기 자신을 등록한다.
			NewRunner();

			Assert.That(DialogueRunner.Instance, Is.Not.Null);
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
