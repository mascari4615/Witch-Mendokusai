using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 「글로 쓴 대화 한 편」 자산의 회귀 잠금.
	///
	/// 잠그는 것: ① 텍스트 파일이 실제로 재생 가능한 그래프가 된다 ② 그래프를 **두 번 세우지 않는다**
	/// (같은 대화를 다시 걸 때마다 세우면 대사 사본이 계속 쌓인다) ③ 원고를 고치면 다시 세울 수 있다
	/// ④ 글이 없어도 터지지 않는다(만들다 만 자산이 게임을 죽이면 안 된다).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueScriptSourceTest
	{
		private static DialogueScriptSource NewSource(string scriptText)
		{
			DialogueScriptSource source = ScriptableObject.CreateInstance<DialogueScriptSource>();
			// 자산 필드라 테스트에서 넣으려면 이 길뿐이다 — 실제 게임에선 인스펙터가 채운다.
			typeof(DialogueScriptSource)
				.GetProperty(nameof(DialogueScriptSource.Script))
				.SetValue(source, new TextAsset(scriptText));
			return source;
		}

		[Test]
		public void WrittenScript_BecomesPlayableGraph()
		{
			DialogueScriptSource source = NewSource(string.Join("\n",
				"### 첫 만남",
				"> 욘: \"...누구야.\"",
				"> 링: \"넷째!\""));

			DialoguePlayback playback = new(source.BuildGraph());
			playback.Begin();

			Assert.That(playback.CurrentLine.Text, Is.EqualTo("...누구야."));
			playback.Advance();
			Assert.That(playback.CurrentLine.ResolveSpeakerName(), Is.EqualTo("링"));
		}

		[Test]
		public void GraphIsBuiltOnce_AndReused()
		{
			DialogueScriptSource source = NewSource("> 욘: \"한 번만 세운다.\"");

			DialogueGraph first = source.BuildGraph();
			DialogueGraph second = source.BuildGraph();

			Assert.That(second, Is.SameAs(first),
				"부를 때마다 세우면 같은 대화를 걸 때마다 대사 사본이 쌓인다");
		}

		[Test]
		public void Invalidate_RebuildsFromChangedText()
		{
			DialogueScriptSource source = NewSource("> 욘: \"처음 글\"");
			DialogueGraph before = source.BuildGraph();

			source.Invalidate();
			DialogueGraph after = source.BuildGraph();

			Assert.That(after, Is.Not.SameAs(before), "원고를 고치고 바로 확인할 수 있어야 한다");
		}

		[Test]
		public void IssuesAreSurfacedAlongsideTheGraph()
		{
			DialogueScriptSource source = NewSource(string.Join("\n",
				"## 시작",
				"> 욘: \"간다\"",
				"> -> 없는장면"));

			source.BuildGraph(out ParsedDialogueScript parsed);

			Assert.That(parsed.Issues.Count, Is.EqualTo(1), "오타는 재생을 막지 않되 조용히 넘어가지도 않는다");
			Assert.That(parsed.Issues[0].LineNumber, Is.EqualTo(3));
		}

		[Test]
		public void MissingText_DoesNotThrow()
		{
			DialogueScriptSource source = ScriptableObject.CreateInstance<DialogueScriptSource>();

			DialogueGraph graph = source.BuildGraph();
			DialoguePlayback playback = new(graph);
			playback.Begin();

			Assert.That(playback.IsPlaying, Is.False, "만들다 만 자산이 게임을 죽이면 안 된다");
		}
	}
}
