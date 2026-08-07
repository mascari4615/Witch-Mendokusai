using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — **선택지 화면이 기대게 될 약속**을 미리 붙든다.
	///
	/// ★ 왜 지금 하나: 선택지 화면은 아직 없다(모양이 사용자 결정 대기). 그런데 화면은 이 두 알림 위에 얹힌다 —
	///   「고를 것이 떴다」와 「대화가 끝났다」. 지금 이게 틀려 있으면 화면을 만드는 날 **화면 탓을 하게 된다.**
	///   부품이 다 초록이어도 알림이 안 오면 화면은 아무것도 못 그린다.
	///
	/// 특히 두 가지: 조건에 걸려 **안 보이는 칸은 빼고** 알려야 하고,
	/// 「끝났다」는 접든 끝까지 가든 **딱 한 번**이라야 한다(두 번 오면 화면이 두 번 닫힌다).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueRunnerUiContractTest
	{
		private const string NL = "\n";

		private static DialogueRunner NewRunner() =>
			new GameObject("DialogueRunnerUiContractTest").AddComponent<DialogueRunner>();

		private static DialogueGraph BuildGraph(params string[] lines) =>
			DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(string.Join(NL, lines)));

		[Test]
		public void ChoicesAreAnnouncedWhenTheyAppear()
		{
			DialogueRunner runner = NewRunner();
			IReadOnlyList<string> announced = null;
			runner.OnChoicesPresented += options => announced = options;

			runner.Play(BuildGraph(
				"## 시작",
				"> - 왼쪽 -> 끝",
				"> - 오른쪽 -> 끝",
				"## 끝",
				"> 욘: \"끝.\""));

			Assert.That(announced, Is.Not.Null, "안 알리면 화면은 아무것도 못 그린다");
			Assert.That(announced.Count, Is.EqualTo(2));
			Assert.That(announced[0], Is.EqualTo("왼쪽"));
		}

		[Test]
		public void HiddenChoicesAreNotAnnounced()
		{
			// 조건에 걸려 안 보이는 칸까지 알리면 화면이 못 고를 버튼을 그린다 —
			// 그리고 그 번호로 고르면 엉뚱한 가지로 간다(보이는 번호와 원고 번호가 어긋난다).
			DialogueHistoryBridge.Clear(DialogueHistoryBridge.Current);
			DialogueItemBridge.Clear(DialogueItemBridge.Current);

			DialogueRunner runner = NewRunner();
			IReadOnlyList<string> announced = null;
			runner.OnChoicesPresented += options => announced = options;

			runner.Play(BuildGraph(
				"## 시작",
				"> - 열쇠를 쓴다 [아이템 1001] -> 끝",
				"> - 그냥 간다 -> 끝",
				"## 끝",
				"> 욘: \"끝.\""));

			Assert.That(announced, Is.Not.Null);
			Assert.That(announced.Count, Is.EqualTo(1), "가진 게 없으니 열쇠 칸은 안 뜬다");
			Assert.That(announced[0], Is.EqualTo("그냥 간다"));
		}

		[Test]
		public void TheQuestionRightBeforeTheChoicesBecomesThePrompt()
		{
			// 고를 것만 뜨고 질문이 없으면 화면은 「뭘 묻는 건지」를 못 보여준다.
			// 원고엔 늘 앞줄에 물음이 있다 — 새 문법 없이 그걸 쓴다(쓰는 사람이 따로 적을 게 없어야 채워진다).
			DialoguePlayback playback = new(BuildGraph(
				"## 시작",
				"> 링: \"뭘 해볼까?\"",
				"> - 왼쪽 -> 끝",
				"> - 오른쪽 -> 끝",
				"## 끝",
				"> 욘: \"끝.\""));
			playback.Begin();
			playback.Advance();

			Assert.That(playback.Current.Kind, Is.EqualTo(DialogueStepKind.Choice));
			Assert.That(playback.Current.Prompt, Is.EqualTo("뭘 해볼까?"));
		}

		[Test]
		public void WithoutALineRightBefore_ThereIsNoPrompt()
		{
			// 멀리서 끌어오면 엉뚱한 문장이 질문으로 뜬다 — 붙어 있는 물음만 질문으로 친다.
			DialoguePlayback playback = new(BuildGraph(
				"## 시작",
				"> 링: \"먼 옛날 이야기다.\"",
				"> wait 1s",
				"> - 왼쪽 -> 끝",
				"## 끝",
				"> 욘: \"끝.\""));
			playback.Begin();
			playback.Advance();
			playback.Tick(5f);

			Assert.That(playback.Current.Kind, Is.EqualTo(DialogueStepKind.Choice));
			Assert.That(playback.Current.Prompt, Is.Null);
		}

		[Test]
		public void FinishedIsAnnouncedOnce_WhenItRunsOut()
		{
			DialogueRunner runner = NewRunner();
			int finished = 0;
			runner.OnDialogueFinished += () => finished++;

			runner.Play(BuildGraph("## 시작", "> 욘: \"한 마디.\""));
			runner.Tick(60f);
			runner.Tick(60f);

			Assert.That(finished, Is.EqualTo(1), "두 번 오면 화면이 두 번 닫힌다");
		}

		[Test]
		public void FinishedIsAnnouncedOnce_WhenStoppedHalfway()
		{
			DialogueRunner runner = NewRunner();
			int finished = 0;
			runner.OnDialogueFinished += () => finished++;

			runner.Play(BuildGraph("## 시작", "> 욘: \"하나.\"", "> 욘: \"둘.\""));
			runner.Stop();
			runner.Stop();

			Assert.That(finished, Is.EqualTo(1), "접는 것도 끝나는 것이다 — 다만 한 번만");
		}
	}
}
