using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 원고에 적은 효과(`!아이템 1001 3`)가 실제로 일어나는지의 회귀 잠금.
	///
	/// 효과 노드는 있었지만 **원고로는 못 썼다** — 조건과 같은 「쓸 길 없음」이었다.
	/// 여기서 잠그는 것: ① 글자가 진짜 효과로 바뀐다 ② **딱 한 번** 일어난다
	/// ③ 모르는 말은 짐작하지 않고 알린다(엉뚱한 게 지급되면 되돌리기 어렵다).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueScriptEffectTest
	{
		private sealed class RecordingEffectSink : IDialogueEffectSink
		{
			public List<EffectInfoData> AppliedData { get; } = new();
			public int DataCallCount { get; private set; }

			public void Apply(IReadOnlyList<EffectInfo> effects)
			{
			}

			public void ApplyData(IReadOnlyList<EffectInfoData> effects)
			{
				DataCallCount++;
				AppliedData.AddRange(effects);
			}
		}

		private static DialoguePlayback PlayScript(string scriptText, RecordingEffectSink sink)
		{
			DialogueGraph graph = DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(scriptText));
			DialoguePlayback playback = new(graph, sink);
			playback.Begin();
			return playback;
		}

		[Test]
		public void WrittenEffect_IsApplied()
		{
			RecordingEffectSink sink = new();

			DialoguePlayback playback = PlayScript(string.Join("\n",
				"## 보상",
				"> !아이템 1001 3",
				"> 욘: \"가져가.\""), sink);

			Assert.That(sink.AppliedData.Count, Is.EqualTo(1));
			Assert.That(sink.AppliedData[0].Type, Is.EqualTo(EffectType.Item));
			Assert.That(sink.AppliedData[0].DataSoID, Is.EqualTo(1001));
			Assert.That(sink.AppliedData[0].Value, Is.EqualTo(3));
			Assert.That(playback.CurrentLine.Text, Is.EqualTo("가져가."), "효과 뒤 대사가 바로 나온다");
		}

		[Test]
		public void ConsecutiveEffects_BecomeOneStep_AppliedOnce()
		{
			RecordingEffectSink sink = new();

			PlayScript(string.Join("\n",
				"## 보상",
				"> !아이템 1001 3",
				"> !카드 200",
				"> !퀘스트열기 5000",
				"> 욘: \"끝.\""), sink);

			Assert.That(sink.DataCallCount, Is.EqualTo(1), "잇달아 적은 효과는 한 묶음");
			Assert.That(sink.AppliedData.Count, Is.EqualTo(3));
			Assert.That(sink.AppliedData[1].Type, Is.EqualTo(EffectType.AddCard));
			Assert.That(sink.AppliedData[2].Type, Is.EqualTo(EffectType.UnlockQuest));
		}

		[Test]
		public void EffectHappensOnlyOnce_EvenWithExtraTicks()
		{
			RecordingEffectSink sink = new();

			DialoguePlayback playback = PlayScript(string.Join("\n",
				"## 보상",
				"> !아이템 1001",
				"> 욘: \"가져가.\""), sink);
			playback.Tick(1f);
			playback.Tick(1f);
			playback.Advance();

			Assert.That(sink.DataCallCount, Is.EqualTo(1), "두 번 주면 물건이 불어난다");
			Assert.That(sink.AppliedData[0].Value, Is.EqualTo(1), "수량을 안 적으면 하나");
		}

		[Test]
		public void UnknownEffect_IsReported_AndNothingIsGiven()
		{
			RecordingEffectSink sink = new();
			string script = string.Join("\n",
				"## 보상",
				"> !이상한거 1001 3",
				"> 욘: \"가져가.\"");

			ParsedDialogueScript parsed = DialogueScriptParser.Parse(script);
			PlayScript(script, sink);

			Assert.That(parsed.Issues.Count, Is.EqualTo(1));
			Assert.That(parsed.Issues[0].LineNumber, Is.EqualTo(2));
			Assert.That(sink.DataCallCount, Is.Zero,
				"모르는 말을 짐작해서 지급하면 엉뚱한 게 나가고, 그건 되돌리기 어렵다");
		}

		[Test]
		public void EffectWithoutNumber_IsReported()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 보상",
				"> !아이템"));

			Assert.That(parsed.Issues.Count, Is.EqualTo(1));
		}

		[Test]
		public void EnglishWordsAreUnderstoodToo()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 보상",
				"> !item 1 2",
				"> !recipe 3",
				"> !quest 4"));

			Assert.That(parsed.HasIssues, Is.False);
			Assert.That(parsed.Sections[0].Entries[0].Effects.Count, Is.EqualTo(3));
		}
	}
}
