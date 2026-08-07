using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — <see cref="DialoguePlayback"/>(그래프를 실제로 쓰는 첫 소비자)의 회귀 잠금.
	///
	/// 여기서 잠그는 건 *소비자 계약* 이다: 언제 넘어가고 언제 안 넘어가는가.
	/// Speak=Advance / Choice=SubmitChoice / Wait(Time)=Tick / Wait(Event)=NotifyEvent /
	/// Branch=아예 안 보임. 시간·코루틴·말풍선 0 — 러너(MonoBehaviour)는 이 계약을 두르는 껍데기.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialoguePlaybackTest
	{
		private sealed class FixedCriteria : Criteria
		{
			private readonly bool result;

			public FixedCriteria(bool result)
			{
				this.result = result;
			}

			public override int GetCurValue() => result ? 1 : 0;
			public override int GetTargetValue() => 1;
			public override bool Evaluate() => result;
		}

		private static DialogueGraph NewGraph() => ScriptableObject.CreateInstance<DialogueGraph>();
		private static DialogueLine NewLine() => ScriptableObject.CreateInstance<DialogueLine>();

		private static DialogueStartNode SeedStart(DialogueGraph graph)
		{
			DialogueStartNode start = new();
			graph.AddNode(start);
			return start;
		}

		[Test]
		public void SpeakChain_AdvancesThenFinishesOnce()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = SeedStart(graph);
			DialogueLine firstLine = NewLine();
			DialogueLine secondLine = NewLine();
			DialogueSpeakNode firstSpeak = new() { Line = firstLine };
			DialogueSpeakNode secondSpeak = new() { Line = secondLine };
			graph.AddNode(firstSpeak);
			graph.AddNode(secondSpeak);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), firstSpeak.FindPort(DialogueSpeakNode.PORT_IN));
			graph.Connect(firstSpeak.FindPort(DialogueSpeakNode.PORT_NEXT), secondSpeak.FindPort(DialogueSpeakNode.PORT_IN));

			int finishedCount = 0;
			int stepCount = 0;
			DialoguePlayback playback = new(graph);
			playback.OnFinished += () => finishedCount++;
			playback.OnStepChanged += _ => stepCount++;

			playback.Begin();
			Assert.That(playback.CurrentLine, Is.SameAs(firstLine));
			Assert.That(playback.IsPlaying, Is.True);

			playback.Advance();
			Assert.That(playback.CurrentLine, Is.SameAs(secondLine));

			playback.Advance();
			Assert.That(playback.IsPlaying, Is.False);
			Assert.That(playback.CurrentLine, Is.Null);
			Assert.That(finishedCount, Is.EqualTo(1));
			Assert.That(stepCount, Is.EqualTo(3), "Speak 2 + End 1");

			playback.Advance();
			Assert.That(finishedCount, Is.EqualTo(1), "끝난 뒤 Advance 는 아무 일도 안 한다");
		}

		private static DialoguePlayback WaitPlayback(DialogueWaitKind kind, float seconds, string eventId, out DialogueLine afterLine)
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = SeedStart(graph);
			DialogueWaitNode wait = new() { Kind = kind, Seconds = seconds, EventId = eventId };
			afterLine = NewLine();
			DialogueSpeakNode after = new() { Line = afterLine };
			graph.AddNode(wait);
			graph.AddNode(after);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), wait.FindPort(DialogueWaitNode.PORT_IN));
			graph.Connect(wait.FindPort(DialogueWaitNode.PORT_NEXT), after.FindPort(DialogueSpeakNode.PORT_IN));

			DialoguePlayback playback = new(graph);
			playback.Begin();
			return playback;
		}

		[Test]
		public void WaitTime_AdvancesWhenTicksReachSeconds()
		{
			DialoguePlayback playback = WaitPlayback(DialogueWaitKind.Time, 1f, null, out DialogueLine afterLine);

			playback.Tick(0.4f);
			Assert.That(playback.Current.Kind, Is.EqualTo(DialogueStepKind.Wait));

			playback.Tick(0.7f);
			Assert.That(playback.CurrentLine, Is.SameAs(afterLine));
		}

		[Test]
		public void WaitTime_OneBigTickClearsConsecutiveWaits()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = SeedStart(graph);
			DialogueWaitNode firstWait = new() { Kind = DialogueWaitKind.Time, Seconds = 1f };
			DialogueWaitNode secondWait = new() { Kind = DialogueWaitKind.Time, Seconds = 1f };
			DialogueLine afterLine = NewLine();
			DialogueSpeakNode after = new() { Line = afterLine };
			graph.AddNode(firstWait);
			graph.AddNode(secondWait);
			graph.AddNode(after);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), firstWait.FindPort(DialogueWaitNode.PORT_IN));
			graph.Connect(firstWait.FindPort(DialogueWaitNode.PORT_NEXT), secondWait.FindPort(DialogueWaitNode.PORT_IN));
			graph.Connect(secondWait.FindPort(DialogueWaitNode.PORT_NEXT), after.FindPort(DialogueSpeakNode.PORT_IN));

			DialoguePlayback playback = new(graph);
			playback.Begin();
			playback.Tick(2.5f);

			Assert.That(playback.CurrentLine, Is.SameAs(afterLine), "남은 시간은 다음 대기로 넘어간다 — 프레임이 길어도 안 밀린다");
		}

		[Test]
		public void WaitEvent_OnlyMatchingIdAdvances()
		{
			DialoguePlayback playback = WaitPlayback(DialogueWaitKind.Event, 0f, "boss-defeated", out DialogueLine afterLine);

			playback.Tick(99f);
			Assert.That(playback.Current.Kind, Is.EqualTo(DialogueStepKind.Wait), "사건 대기는 시간으로 안 풀린다");

			playback.NotifyEvent("something-else");
			Assert.That(playback.Current.Kind, Is.EqualTo(DialogueStepKind.Wait));

			playback.NotifyEvent("boss-defeated");
			Assert.That(playback.CurrentLine, Is.SameAs(afterLine));
		}

		[Test]
		public void Advance_IgnoredDuringWait()
		{
			DialoguePlayback playback = WaitPlayback(DialogueWaitKind.Time, 5f, null, out DialogueLine _);

			playback.Advance();

			Assert.That(playback.Current.Kind, Is.EqualTo(DialogueStepKind.Wait),
				"기다리라고 적어둔 것을 Advance 로 건너뛰면 그 노드가 무의미해진다");
		}

		private static DialoguePlayback ChoicePlayback(out DialogueLine secondOptionLine)
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = SeedStart(graph);
			DialogueChoiceNode choice = new() { Prompt = "어느 쪽?", Options = new List<string> { "A", "B" } };
			DialogueLine firstOptionLine = NewLine();
			secondOptionLine = NewLine();
			DialogueSpeakNode firstOption = new() { Line = firstOptionLine };
			DialogueSpeakNode secondOption = new() { Line = secondOptionLine };
			graph.AddNode(choice);
			graph.AddNode(firstOption);
			graph.AddNode(secondOption);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), choice.FindPort(DialogueChoiceNode.PORT_IN));
			graph.Connect(choice.FindPort(DialogueChoiceNode.ChoicePortId(0)), firstOption.FindPort(DialogueSpeakNode.PORT_IN));
			graph.Connect(choice.FindPort(DialogueChoiceNode.ChoicePortId(1)), secondOption.FindPort(DialogueSpeakNode.PORT_IN));

			DialoguePlayback playback = new(graph);
			playback.Begin();
			return playback;
		}

		[Test]
		public void Choice_OnlySubmitAdvances()
		{
			DialoguePlayback playback = ChoicePlayback(out DialogueLine secondOptionLine);

			Assert.That(playback.CurrentChoices, Is.Not.Null);
			Assert.That(playback.CurrentChoices.Count, Is.EqualTo(2));

			playback.Advance();
			Assert.That(playback.Current.Kind, Is.EqualTo(DialogueStepKind.Choice), "안 고르면 안 넘어간다");

			Assert.That(playback.SubmitChoice(5), Is.False, "범위 밖은 상태 불변");
			Assert.That(playback.SubmitChoice(1), Is.True);
			Assert.That(playback.CurrentLine, Is.SameAs(secondOptionLine));
		}

		[Test]
		public void Stop_FinishesExactlyOnce()
		{
			DialoguePlayback playback = ChoicePlayback(out DialogueLine _);
			int finishedCount = 0;
			playback.OnFinished += () => finishedCount++;

			playback.Stop();
			Assert.That(playback.IsPlaying, Is.False);
			Assert.That(finishedCount, Is.EqualTo(1));

			playback.Stop();
			Assert.That(finishedCount, Is.EqualTo(1), "두 번 중단해도 끝 통지는 한 번");
		}

		[Test]
		public void Branch_NeverSurfacesToConsumer()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = SeedStart(graph);
			DialogueBranchNode branch = new() { Condition = new FixedCriteria(false) };
			DialogueLine unmetLine = NewLine();
			DialogueSpeakNode unmetSpeak = new() { Line = unmetLine };
			DialogueSpeakNode metSpeak = new() { Line = NewLine() };
			graph.AddNode(branch);
			graph.AddNode(unmetSpeak);
			graph.AddNode(metSpeak);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), branch.FindPort(DialogueBranchNode.PORT_IN));
			graph.Connect(branch.FindPort(DialogueBranchNode.PORT_TRUE), metSpeak.FindPort(DialogueSpeakNode.PORT_IN));
			graph.Connect(branch.FindPort(DialogueBranchNode.PORT_FALSE), unmetSpeak.FindPort(DialogueSpeakNode.PORT_IN));

			int stepCount = 0;
			DialoguePlayback playback = new(graph);
			playback.OnStepChanged += _ => stepCount++;
			playback.Begin();

			Assert.That(playback.CurrentLine, Is.SameAs(unmetLine));
			Assert.That(stepCount, Is.EqualTo(1), "분기는 스텝 통지를 만들지 않는다");
		}

		[Test]
		public void Speak_AutoAdvancesWhenDefaultSecondsSet()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = SeedStart(graph);
			DialogueLine firstLine = NewLine();
			DialogueLine secondLine = NewLine();
			DialogueSpeakNode firstSpeak = new() { Line = firstLine };
			DialogueSpeakNode secondSpeak = new() { Line = secondLine };
			graph.AddNode(firstSpeak);
			graph.AddNode(secondSpeak);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), firstSpeak.FindPort(DialogueSpeakNode.PORT_IN));
			graph.Connect(firstSpeak.FindPort(DialogueSpeakNode.PORT_NEXT), secondSpeak.FindPort(DialogueSpeakNode.PORT_IN));

			DialoguePlayback playback = new(graph) { DefaultSpeakSeconds = 2f };
			playback.Begin();

			playback.Tick(1.5f);
			Assert.That(playback.CurrentLine, Is.SameAs(firstLine), "아직 시간이 안 됐다");

			playback.Tick(1f);
			Assert.That(playback.CurrentLine, Is.SameAs(secondLine), "시간이 차면 대사도 저절로 넘어간다");
		}

		[Test]
		public void Speak_WithoutDuration_WaitsForExplicitAdvance()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = SeedStart(graph);
			DialogueLine onlyLine = NewLine();
			DialogueSpeakNode speak = new() { Line = onlyLine };
			graph.AddNode(speak);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), speak.FindPort(DialogueSpeakNode.PORT_IN));

			DialoguePlayback playback = new(graph);
			playback.Begin();
			playback.Tick(999f);

			Assert.That(playback.CurrentLine, Is.SameAs(onlyLine),
				"시간이 안 정해진 대사는 저절로 안 넘어간다 — 클릭으로 넘기는 연출을 위해");
		}

		[Test]
		public void EmptyGraph_FinishesOnBegin()
		{
			DialogueGraph graph = NewGraph();
			int finishedCount = 0;
			DialoguePlayback playback = new(graph);
			playback.OnFinished += () => finishedCount++;

			playback.Begin();

			Assert.That(playback.IsPlaying, Is.False);
			Assert.That(finishedCount, Is.EqualTo(1));
		}
	}
}
