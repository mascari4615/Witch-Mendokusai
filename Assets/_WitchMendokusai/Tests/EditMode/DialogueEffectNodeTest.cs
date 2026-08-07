using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 대화가 뭔가를 *일으키는* 노드의 회귀 잠금.
	///
	/// 잠그는 것: ① 효과가 실제로 넘어간다 ② 소비자에겐 안 보인다(스텝 통지 0)
	/// ③ **딱 한 번만 일어난다**(두 번 주면 물건이 불어난다) ④ 통로가 없으면 조용히 넘어가지 않고 터진다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueEffectNodeTest
	{
		/// <summary>일으킨 효과를 그냥 적어 두는 대역 — 진짜 게임 시스템 없이 「무엇이 몇 번」만 본다.</summary>
		private sealed class RecordingEffectSink : IDialogueEffectSink
		{
			public List<EffectInfo> Applied { get; } = new();
			public int CallCount { get; private set; }

			public void Apply(IReadOnlyList<EffectInfo> effects)
			{
				CallCount++;
				for (int i = 0; i < effects.Count; i++)
				{
					Applied.Add(effects[i]);
				}
			}
		}

		private static DialogueGraph NewGraph() => ScriptableObject.CreateInstance<DialogueGraph>();
		private static DialogueLine NewLine() => ScriptableObject.CreateInstance<DialogueLine>();

		private static EffectInfo GiveItem(int value) => new()
		{
			Type = EffectType.Item,
			ArithmeticOperator = ArithmeticOperator.Add,
			Value = value,
		};

		/// <summary>start → 효과 노드 → 대사 그래프.</summary>
		private static DialogueGraph BuildEffectThenSpeak(List<EffectInfo> effects, out DialogueLine afterLine)
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueEffectNode effectNode = new() { Effects = effects };
			afterLine = NewLine();
			DialogueSpeakNode after = new() { Line = afterLine };
			graph.AddNode(start);
			graph.AddNode(effectNode);
			graph.AddNode(after);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), effectNode.FindPort(DialogueEffectNode.PORT_IN));
			graph.Connect(effectNode.FindPort(DialogueEffectNode.PORT_NEXT), after.FindPort(DialogueSpeakNode.PORT_IN));
			return graph;
		}

		[Test]
		public void Effects_AreAppliedAndNodeIsInvisibleToConsumer()
		{
			DialogueGraph graph = BuildEffectThenSpeak(new List<EffectInfo> { GiveItem(3) }, out DialogueLine afterLine);
			RecordingEffectSink sink = new();

			int stepCount = 0;
			DialoguePlayback playback = new(graph, sink);
			playback.OnStepChanged += _ => stepCount++;
			playback.Begin();

			Assert.That(sink.Applied.Count, Is.EqualTo(1));
			Assert.That(sink.Applied[0].Value, Is.EqualTo(3));
			Assert.That(playback.CurrentLine, Is.SameAs(afterLine), "효과 뒤 대사가 바로 나온다");
			Assert.That(stepCount, Is.EqualTo(1), "효과 노드는 스텝 통지를 만들지 않는다");
		}

		[Test]
		public void Effects_AppliedExactlyOnce_EvenWithExtraTicks()
		{
			DialogueGraph graph = BuildEffectThenSpeak(new List<EffectInfo> { GiveItem(1) }, out DialogueLine _);
			RecordingEffectSink sink = new();

			DialoguePlayback playback = new(graph, sink);
			playback.Begin();
			playback.Tick(1f);
			playback.Tick(1f);
			playback.Advance();

			Assert.That(sink.CallCount, Is.EqualTo(1), "두 번 일어나면 물건이 불어난다 — 여기가 제일 위험한 자리");
		}

		[Test]
		public void ConsecutiveEffectNodes_AllApplyWithinOneStep()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueEffectNode first = new() { Effects = new List<EffectInfo> { GiveItem(1) } };
			DialogueEffectNode second = new() { Effects = new List<EffectInfo> { GiveItem(2) } };
			DialogueLine afterLine = NewLine();
			DialogueSpeakNode after = new() { Line = afterLine };
			graph.AddNode(start);
			graph.AddNode(first);
			graph.AddNode(second);
			graph.AddNode(after);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), first.FindPort(DialogueEffectNode.PORT_IN));
			graph.Connect(first.FindPort(DialogueEffectNode.PORT_NEXT), second.FindPort(DialogueEffectNode.PORT_IN));
			graph.Connect(second.FindPort(DialogueEffectNode.PORT_NEXT), after.FindPort(DialogueSpeakNode.PORT_IN));

			RecordingEffectSink sink = new();
			DialoguePlayback playback = new(graph, sink);
			playback.Begin();

			Assert.That(sink.CallCount, Is.EqualTo(2));
			Assert.That(playback.CurrentLine, Is.SameAs(afterLine));
		}

		[Test]
		public void MissingSink_Throws()
		{
			DialogueGraph graph = BuildEffectThenSpeak(new List<EffectInfo> { GiveItem(1) }, out DialogueLine _);

			DialoguePlayback playback = new(graph);

			Assert.That(() => playback.Begin(), Throws.TypeOf<InvalidOperationException>(),
				"주기로 적어 둔 것이 조용히 안 나오는 게 제일 나쁜 결말이라 터뜨린다");
		}

		[Test]
		public void EmptyEffectNode_IsWarnedByValidator()
		{
			DialogueGraph graph = BuildEffectThenSpeak(new List<EffectInfo>(), out DialogueLine _);

			DialogueGraphValidationResult result = DialogueGraphValidator.Validate(graph);

			Assert.That(result.CountOf(DialogueGraphIssueKind.EffectNodeWithoutEffects), Is.EqualTo(1));
			Assert.That(result.IsValid, Is.True, "지나가기만 할 뿐 고장은 아니다 — 경고까지");
		}

		[Test]
		public void GraphWithoutEffectNodes_NeedsNoSink()
		{
			DialogueGraph graph = NewGraph();
			DialogueStartNode start = new();
			DialogueLine onlyLine = NewLine();
			DialogueSpeakNode speak = new() { Line = onlyLine };
			graph.AddNode(start);
			graph.AddNode(speak);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), speak.FindPort(DialogueSpeakNode.PORT_IN));

			DialoguePlayback playback = new(graph);
			playback.Begin();

			Assert.That(playback.CurrentLine, Is.SameAs(onlyLine), "효과가 없는 대화는 통로 없이도 그냥 돈다");
		}
	}
}
