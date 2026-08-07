using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 옛 대사 사슬을 그래프로 옮기는 자리의 회귀 잠금.
	///
	/// ★ 왜 옮겼나: 대사 사슬을 트는 길이 **따로** 있었다(코루틴이 직접 말풍선을 띄웠다).
	///   그래서 그 길로 나온 대화는 건너뛰기도, 시간 주입도, 로그도 제대로 못 받았다.
	///
	/// ★ 여기서 잠그는 핵심: **뜻이 바뀌지 않았는가.** 옛 길은 갈래가 여럿이어도 늘 첫째만 갔다.
	///   그걸 「진짜 선택지」로 올리면 고르는 화면이 없는 지금 기존 대사들이 멈췄다가 접힌다.
	///   대신 버려지는 가지 수를 세어 알린다 — 조용하던 것을 눈에 보이게.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueLineChainGraphTest
	{
		private static DialogueLine NewLine() => ScriptableObject.CreateInstance<DialogueLine>();

		private static DialogueLine Chain(params DialogueLine[] lines)
		{
			for (int i = 0; i < lines.Length - 1; i++)
			{
				lines[i].Choices.Add(lines[i + 1]);
			}
			return lines[0];
		}

		private static int SpeakNodeCount(DialogueGraph graph)
		{
			int count = 0;
			for (int i = 0; i < graph.Nodes.Count; i++)
			{
				if (graph.Nodes[i] is DialogueSpeakNode)
				{
					count++;
				}
			}
			return count;
		}

		[Test]
		public void EveryLineInTheChainBecomesAStep()
		{
			DialogueGraph graph = DialogueLineChainGraphBuilder.Build(
				Chain(NewLine(), NewLine(), NewLine()), out int skipped);

			Assert.That(SpeakNodeCount(graph), Is.EqualTo(3));
			Assert.That(skipped, Is.EqualTo(0));
		}

		[Test]
		public void OnlyTheFirstBranchIsFollowed_AndTheRestAreCounted()
		{
			// 옛 동작 그대로 — 다만 버려지는 가지를 센다.
			DialogueLine first = NewLine();
			first.Choices.Add(NewLine());
			first.Choices.Add(NewLine());
			first.Choices.Add(NewLine());

			DialogueLine[] chain = { first };
			DialogueGraph graph = DialogueLineChainGraphBuilder.Build(chain[0], out int skipped);

			Assert.That(SpeakNodeCount(graph), Is.EqualTo(2), "첫째 갈래만 따라간다");
			Assert.That(skipped, Is.EqualTo(2), "안 간 길이 둘 — 조용히 버리지 말고 세어서 알린다");
		}

		[Test]
		public void AChainThatLoopsBack_Stops()
		{
			// 사슬이 자기에게 돌아오면 그래프를 세우다 영영 안 끝난다.
			DialogueLine first = NewLine();
			DialogueLine second = NewLine();
			first.Choices.Add(second);
			second.Choices.Add(first);

			DialogueGraph graph = DialogueLineChainGraphBuilder.Build(first, out int skipped);

			Assert.That(SpeakNodeCount(graph), Is.EqualTo(2));
			Assert.That(skipped, Is.EqualTo(0));
		}

		[Test]
		public void NoLine_GivesAnEmptyGraph()
		{
			DialogueGraph graph = DialogueLineChainGraphBuilder.Build(null, out int skipped);

			Assert.That(SpeakNodeCount(graph), Is.EqualTo(0));
			Assert.That(skipped, Is.EqualTo(0));
		}

		[Test]
		public void TheOldEntryNowGoesThroughTheSameDriver()
		{
			// 옛 입구로 걸어도 시간 주입으로 흐르고, 로그에 남는다 — 예전엔 둘 다 안 됐다.
			DialogueRunner runner = new GameObject("DialogueLineChainTest").AddComponent<DialogueRunner>();
			runner.Play(Chain(NewLine(), NewLine()));

			Assert.That(runner.IsPlaying, Is.True);
			runner.Tick(60f);

			Assert.That(runner.IsPlaying, Is.False, "시간이 지나면 끝까지 흐른다");
		}
	}
}
