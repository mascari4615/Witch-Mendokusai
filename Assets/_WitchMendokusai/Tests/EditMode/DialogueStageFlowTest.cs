using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 그래프 흐름과 러너 배선: 신호 순서, 분기, 건너뛰기, 수동 넘김, 잠긴 칸
	public sealed class DialogueStageFlowTest
	{
		private sealed class FakeHost : IDialogueStageHost
		{
			public readonly List<string> Calls = new();
			public readonly List<DialogueStageDelta> Lines = new();
			public IReadOnlyList<DialogueChoiceRow> Rows;
			public IDialogueStageControls Controls;
			public Action<int> Submit;

			public bool IsHoldingInput => false;
			public bool IsPausingTime => false;

			public void Begin(IDialogueStageControls controls)
			{
				Controls = controls;
				Calls.Add("begin");
			}

			public void ApplyLine(DialogueStageDelta delta, string speakerName, string text)
			{
				Lines.Add(delta);
				Calls.Add("line:" + text);
			}

			public void ApplyCues(DialogueStageDelta delta) => Calls.Add("cues");

			public void PresentChoices(DialogueStageDelta delta, string speakerName, string prompt, IReadOnlyList<DialogueChoiceRow> rows, Action<int> submit)
			{
				Rows = rows;
				Submit = submit;
				Calls.Add("choices:" + prompt);
			}

			public void Finish() => Calls.Add("finish");

			public bool TryResolveWorldAnchor(string actorName, out Transform anchor)
			{
				anchor = null;
				return false;
			}
		}

		private static DialogueGraph Graph(params string[] lines) =>
			DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(string.Join("\n", lines)));

		private static DialogueRunner NewRunner() => new GameObject("DialogueStageFlowTest").AddComponent<DialogueRunner>();

		private static FakeHost Install()
		{
			FakeHost host = new();
			DialogueStageBridge.Register(host);
			return host;
		}

		[Test]
		public void CuesArePassedThroughInvisibly_BeforeTheLine()
		{
			DialoguePlayback playback = new(Graph("## 시작", "> @방식 미연시", "> @등장 링 왼쪽", "> 링: \"앗.\""));
			List<DialogueStageCue> seen = new();
			playback.OnStageCues += cues => seen.AddRange(cues);

			playback.Begin();

			Assert.That(seen.Count, Is.EqualTo(2));
			Assert.That(playback.Current.Kind, Is.EqualTo(DialogueStepKind.Speak));
		}

		[Test]
		public void TrailingCueBeforeSectionEnd_StillFlows()
		{
			DialoguePlayback playback = new(Graph("## 시작", "> 링: \"앗.\"", "> @띠 끔"));
			int count = 0;
			playback.OnStageCues += cues => count += cues.Count;

			playback.Begin();
			playback.Advance();

			Assert.That(count, Is.EqualTo(1));
			Assert.That(playback.IsPlaying, Is.False);
			Assert.That(playback.ReachedEnd, Is.True);
		}

		[Test]
		public void OnlyTheChosenBranchCues_Accumulate()
		{
			DialoguePlayback playback = new(Graph(
				"## 시작",
				"> - 왼쪽 -> 왼쪽길",
				"> - 오른쪽 -> 오른쪽길",
				"## 왼쪽길",
				"> @등장 링 왼쪽",
				"> 링: \"왼쪽.\"",
				"> -> 끝",
				"## 오른쪽길",
				"> @등장 욘 오른쪽",
				"> 욘: \"오른쪽.\"",
				"## 끝",
				"> 링: \"끝.\""));
			DialogueStagePlan plan = new();
			playback.OnStageCues += plan.Queue;

			playback.Begin();
			playback.SubmitChoice(1);
			plan.Flush();

			Assert.That(plan.Actors.Count, Is.EqualTo(1));
			Assert.That(plan.Actors[0].Name, Is.EqualTo("욘"));
		}

		[Test]
		public void Skip_StillDeliversCues_AndStopsAtChoice()
		{
			DialoguePlayback playback = new(Graph(
				"## 시작",
				"> @등장 링 왼쪽",
				"> 링: \"하나.\"",
				"> @이동 링 오른쪽",
				"> 링: \"둘.\"",
				"> - 끝낸다 -> 끝",
				"## 끝",
				"> 링: \"끝.\""));
			DialogueStagePlan plan = new();
			playback.OnStageCues += plan.Queue;

			playback.Begin();
			playback.Skip();
			plan.Flush();

			Assert.That(playback.Current.Kind, Is.EqualTo(DialogueStepKind.Choice));
			Assert.That(plan.Actors[0].Slot, Is.EqualTo(DialogueStageSlot.Right));
		}

		[Test]
		public void ManualSpeakAdvance_IgnoresTimeButNotWait()
		{
			DialoguePlayback playback = new(Graph("## 시작", "> 링: \"앗.\"", "> wait 1s", "> 링: \"둘.\""))
			{
				DefaultSpeakSeconds = 1f,
				ManualSpeakAdvance = true,
			};
			playback.Begin();

			playback.Tick(10f);
			Assert.That(playback.Current.SpeakLine.Text, Is.EqualTo("앗."));

			playback.Advance();
			Assert.That(playback.Current.Kind, Is.EqualTo(DialogueStepKind.Wait));
			playback.Tick(1.5f);
			Assert.That(playback.Current.SpeakLine.Text, Is.EqualTo("둘."));
		}

		[Test]
		public void PromptSkipsCuesBetweenLineAndChoice()
		{
			DialoguePlayback playback = new(Graph(
				"## 시작",
				"> 링: \"뭘 해볼까?\"",
				"> @점프 링",
				"> - 끝낸다 -> 끝",
				"## 끝",
				"> 링: \"끝.\""));
			playback.Begin();
			playback.Advance();

			Assert.That(playback.Current.Prompt, Is.EqualTo("뭘 해볼까?"));
		}

		[Test]
		public void LockedChoice_IsShownButNotSelectable_AndIndexesStayAligned()
		{
			DialogueItemBridge.Clear(DialogueItemBridge.Current);
			DialoguePlayback playback = new(Graph(
				"## 시작",
				"> - 열쇠를 쓴다 [아이템 1001] {열쇠가 없다} -> 끝",
				"> - 숨은 칸 [아이템 1002] -> 끝",
				"> - 그냥 간다 -> 다른길",
				"## 끝",
				"> 링: \"끝.\"",
				"## 다른길",
				"> 욘: \"다른 길.\""));
			playback.Begin();

			IReadOnlyList<DialogueChoiceRow> rows = playback.Current.Rows;
			Assert.That(rows.Count, Is.EqualTo(2));
			Assert.That(rows[0].IsLocked, Is.True);
			Assert.That(rows[0].LockReason, Is.EqualTo("열쇠가 없다"));
			Assert.That(rows[1].IsLocked, Is.False);
			Assert.That(playback.Current.Options.Count, Is.EqualTo(1));

			Assert.That(playback.SubmitChoice(rows[1].SubmitIndex), Is.True);
			Assert.That(playback.Current.SpeakLine.Text, Is.EqualTo("다른 길."));
		}

		[Test]
		public void LockReasonWithoutCondition_IsAnIssue()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 시작", "> - 칸 {사유} -> 끝", "## 끝", "> 링: \"끝.\""));
			Assert.That(parsed.HasIssues, Is.True);
		}

		[Test]
		public void LockReason_SurvivesWriteAndRead()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 시작", "> - 칸 [아이템 1001] {사유} -> 끝", "> - 간다 -> 끝", "## 끝", "> 링: \"끝.\""));
			ParsedDialogueScript again = DialogueScriptParser.Parse(DialogueScriptWriter.Write(parsed));
			Assert.That(again.Sections[0].Entries[0].Choices[0].LockReason, Is.EqualTo("사유"));
			Assert.That(again.Sections[0].Entries[0].Choices[0].Label, Is.EqualTo("칸"));
		}

		[Test]
		public void Runner_NovelLineWaitsForInput_WorldLineStaysTimed()
		{
			FakeHost host = Install();
			DialogueRunner runner = NewRunner();
			runner.Play(Graph(
				"## 시작",
				"> @방식 미연시",
				"> 링: \"하나.\"",
				"> @방식 말풍선",
				"> 링: \"둘.\"",
				"> 링: \"셋.\""));

			runner.Tick(100f);
			Assert.That(runner.CurrentLine.Text, Is.EqualTo("하나."), "미연시 줄은 시간으로 안 넘어감");
			Assert.That(runner.CanAdvanceLine, Is.True);

			runner.AdvanceLine();
			Assert.That(runner.CurrentLine.Text, Is.EqualTo("둘."));
			runner.Tick(100f);
			Assert.That(runner.IsPlaying, Is.False, "말풍선 줄은 기존처럼 시간으로 넘어감");
			Assert.That(host.Calls[0], Is.EqualTo("begin"));
			Assert.That(host.Calls[host.Calls.Count - 1], Is.EqualTo("finish"));

			DialogueStageBridge.Clear(host);
		}

		[Test]
		public void Runner_ScriptWithoutCues_StaysInWorld()
		{
			FakeHost host = Install();
			DialogueRunner runner = NewRunner();
			runner.Play(Graph("## 시작", "> 링: \"하나.\""));

			Assert.That(host.Lines[0].Presentation, Is.EqualTo(DialogueStagePresentation.World));
			runner.Tick(100f);
			Assert.That(runner.IsPlaying, Is.False);

			DialogueStageBridge.Clear(host);
		}

		[Test]
		public void Runner_ChoiceGoesToHost_WithPromptAndNoStall()
		{
			FakeHost host = Install();
			DialogueRunner runner = NewRunner();
			runner.Play(Graph(
				"## 시작",
				"> 링: \"뭘 해볼까?\"",
				"> - 간다 -> 끝",
				"## 끝",
				"> 링: \"끝.\""));

			runner.AdvanceLine();
			Assert.That(host.Calls.Contains("choices:뭘 해볼까?"), Is.True);
			Assert.That(host.Rows.Count, Is.EqualTo(1));

			runner.Tick(1000f);
			Assert.That(runner.IsPlaying, Is.True, "화면이 있으면 시간으로 안 접힘");

			host.Submit(0);
			Assert.That(runner.CurrentLine.Text, Is.EqualTo("끝."));

			DialogueStageBridge.Clear(host);
		}

		[Test]
		public void Runner_StopFinishesHostOnce()
		{
			FakeHost host = Install();
			DialogueRunner runner = NewRunner();
			runner.Play(Graph("## 시작", "> @방식 미연시", "> 링: \"하나.\""));

			runner.Stop();

			int finishes = 0;
			foreach (string call in host.Calls)
			{
				finishes += call == "finish" ? 1 : 0;
			}
			Assert.That(finishes, Is.EqualTo(1));

			DialogueStageBridge.Clear(host);
		}

		[Test]
		public void Runner_WaitFlushesPendingCues()
		{
			FakeHost host = Install();
			DialogueRunner runner = NewRunner();
			runner.Play(Graph("## 시작", "> @방식 미연시", "> wait 1s", "> 링: \"하나.\""));

			Assert.That(host.Calls.Contains("cues"), Is.True);
			Assert.That(runner.CanAdvanceLine, Is.False, "대기 중에는 입력으로 안 넘어감");

			runner.Stop();
			DialogueStageBridge.Clear(host);
		}
	}
}
