using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 대사가 화면에 머무는 시간의 회귀 잠금.
	///
	/// 여태 모든 대사가 똑같이 3초였다 — 「응.」은 지루하고 **긴 줄은 다 읽기 전에 사라진다.**
	/// 여기서 잠그는 것: ① 길수록 오래 ② 아무리 짧아도 최소는 지킨다 ③ 아무리 길어도 멈춘다
	/// ④ 작가가 직접 적은 시간이 제일 세다 ⑤ 속도를 끄면 「눌러야 넘어감」이 된다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueReadingTimeTest
	{
		[Test]
		public void LongerTextTakesLonger()
		{
			float shortLine = DialogueReadingTime.For("응.", 10f, 0f, 0f);
			float longLine = DialogueReadingTime.For(new string('가', 50), 10f, 0f, 0f);

			Assert.That(longLine, Is.GreaterThan(shortLine));
			Assert.That(longLine, Is.EqualTo(5f), "50자 ÷ 초당 10자 = 5초");
		}

		[Test]
		public void MinimumIsKept()
		{
			Assert.That(DialogueReadingTime.For("응.", 10f, 1.2f, 8f), Is.EqualTo(1.2f),
				"짧은 대사가 스치듯 지나가면 읽기 전에 사라진다");
		}

		[Test]
		public void MaximumStopsIt()
		{
			Assert.That(DialogueReadingTime.For(new string('가', 500), 10f, 1.2f, 8f), Is.EqualTo(8f));
		}

		[Test]
		public void BrokenLimits_KeepMinimum()
		{
			Assert.That(DialogueReadingTime.For(new string('가', 500), 10f, 3f, 1f), Is.EqualTo(3f),
				"위 한계를 아래보다 작게 잡아 놨어도 짧게 스치게 만들지 않는다");
		}

		[Test]
		public void SpeedOff_MeansNoAutoAdvance()
		{
			Assert.That(DialogueReadingTime.For("긴 대사든 짧은 대사든", 0f, 1.2f, 8f), Is.Zero);
		}

		[Test]
		public void EmptyText_StillKeepsMinimum()
		{
			Assert.That(DialogueReadingTime.For(null, 10f, 1.2f, 8f), Is.EqualTo(1.2f));
			Assert.That(DialogueReadingTime.For("   ", 10f, 1.2f, 8f), Is.EqualTo(1.2f));
		}

		[Test]
		public void Playback_UsesReadingSpeed_ButLineWaitWins()
		{
			DialogueGraph graph = ScriptableObject.CreateInstance<DialogueGraph>();
			DialogueStartNode start = new();
			DialogueLine longLine = DialogueLine.CreateRuntime("욘", new string('가', 40));
			DialogueLine afterLine = DialogueLine.CreateRuntime("링", "끝");
			DialogueSpeakNode speak = new() { Line = longLine };
			DialogueSpeakNode after = new() { Line = afterLine };
			graph.AddNode(start);
			graph.AddNode(speak);
			graph.AddNode(after);
			graph.Connect(start.FindPort(DialogueStartNode.PORT_NEXT), speak.FindPort(DialogueSpeakNode.PORT_IN));
			graph.Connect(speak.FindPort(DialogueSpeakNode.PORT_NEXT), after.FindPort(DialogueSpeakNode.PORT_IN));

			DialoguePlayback playback = new(graph)
			{
				ReadingCharactersPerSecond = 10f,
				MinimumSpeakSeconds = 1f,
				MaximumSpeakSeconds = 8f,
				DefaultSpeakSeconds = 3f,
			};
			playback.Begin();

			playback.Tick(3.5f);
			Assert.That(playback.CurrentLine, Is.SameAs(longLine),
				"40자면 4초 — 기본 3초로 넘겨 버리면 다 못 읽는다");

			playback.Tick(0.6f);
			Assert.That(playback.CurrentLine, Is.SameAs(afterLine));
		}
	}
}
