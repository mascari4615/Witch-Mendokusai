using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — **건너뛰기**의 회귀 잠금.
	///
	/// ★ 이 기능의 위험은 「빨리 감기」가 아니라 **몰래 달라지는 것**이다.
	///   건너뛴 판과 안 건너뛴 판이 **끝난 뒤에 같은 상태**여야 한다 —
	///   보상을 덜 받거나, 아직 안 열린 문을 열었다고 말하거나, 대신 골라 주면 안 된다.
	///   그래서 「멈추는 자리 셋」과 「효과는 그대로 준다」를 잠근다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueSkipTest
	{
		private const string NL = "\n";

		/// <summary>받은 효과를 그냥 세는 대역 — 건너뛰어도 받았는지만 본다.</summary>
		private sealed class CountingSink : IDialogueEffectSink
		{
			public int ItemGrants { get; private set; }

			public void Apply(IReadOnlyList<EffectInfo> effects)
			{
				ItemGrants += effects == null ? 0 : effects.Count;
			}

			public void ApplyData(IReadOnlyList<EffectInfoData> effects)
			{
				ItemGrants += effects == null ? 0 : effects.Count;
			}
		}

		private static DialoguePlayback Play(string script, IDialogueEffectSink sink = null)
		{
			DialoguePlayback playback = new(
				DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(script)), sink);
			playback.Begin();
			return playback;
		}

		[Test]
		public void PlainLines_AreSkippedToTheEnd()
		{
			DialoguePlayback playback = Play(string.Join(NL,
				"## 시작",
				"> 욘: \"하나.\"",
				"> 욘: \"둘.\"",
				"> 욘: \"셋.\""));

			int skipped = playback.Skip();

			Assert.That(playback.ReachedEnd, Is.True);
			Assert.That(skipped, Is.EqualTo(3), "세 마디를 넘겼다");
		}

		[Test]
		public void SkipStopsAtAChoice()
		{
			// 대신 골라 주면 그건 건너뛰기가 아니라 플레이를 뺏는 것이다.
			DialoguePlayback playback = Play(string.Join(NL,
				"## 시작",
				"> 욘: \"하나.\"",
				"> - 왼쪽 -> 왼",
				"> - 오른쪽 -> 오른",
				"## 왼",
				"> 욘: \"왼.\"",
				"## 오른",
				"> 욘: \"오른.\""));

			playback.Skip();

			Assert.That(playback.CurrentChoices, Is.Not.Null);
			Assert.That(playback.ReachedEnd, Is.False);
		}

		[Test]
		public void SkipStopsAtAnEventWait()
		{
			// 대화 밖에서 뭔가 일어나야 하는 자리다 — 넘기면 대화가 게임을 앞지른다.
			DialoguePlayback playback = Play(string.Join(NL,
				"## 시작",
				"> 욘: \"기다려 봐.\"",
				"> wait event 문열림",
				"> 욘: \"열렸다.\""));

			playback.Skip();

			Assert.That(playback.Current.Kind, Is.EqualTo(DialogueStepKind.Wait));
			Assert.That(playback.Current.WaitKind, Is.EqualTo(DialogueWaitKind.Event));

			// 사건이 오면 그 뒤는 다시 건너뛸 수 있다.
			playback.NotifyEvent("문열림");
			playback.Skip();
			Assert.That(playback.ReachedEnd, Is.True);
		}

		[Test]
		public void TimeWaitsAreSkipped()
		{
			// 시간은 연출이고, 건너뛰기는 연출을 접겠다는 뜻이다.
			DialoguePlayback playback = Play(string.Join(NL,
				"## 시작",
				"> wait 3s",
				"> 욘: \"끝.\""));

			playback.Skip();

			Assert.That(playback.ReachedEnd, Is.True);
		}

		[Test]
		public void EffectsAreStillApplied()
		{
			// 「빨리 봤다」가 「덜 받았다」가 되면 안 된다.
			CountingSink sink = new();
			DialoguePlayback playback = Play(string.Join(NL,
				"## 보상",
				"> 욘: \"가져가.\"",
				"> !아이템 1001 3",
				"> 욘: \"잘 써.\""), sink);

			playback.Skip();

			Assert.That(playback.ReachedEnd, Is.True);
			Assert.That(sink.ItemGrants, Is.GreaterThan(0), "건너뛰어도 준 것은 줘야 한다");
		}

		[Test]
		public void SkippingAtAStoppingPoint_ChangesNothing()
		{
			DialoguePlayback playback = Play(string.Join(NL,
				"## 시작",
				"> - 왼쪽 -> 왼",
				"> - 오른쪽 -> 오른",
				"## 왼",
				"> 욘: \"왼.\"",
				"## 오른",
				"> 욘: \"오른.\""));

			Assert.That(playback.Skip(), Is.EqualTo(0), "이미 멈출 자리면 아무 일도 안 한다");
			Assert.That(playback.CurrentChoices, Is.Not.Null);
		}

		[Test]
		public void SkipOnAFinishedPlayback_DoesNothing()
		{
			DialoguePlayback playback = Play(string.Join(NL, "## 시작", "> 욘: \"끝.\""));
			playback.Skip();

			Assert.That(playback.Skip(), Is.EqualTo(0));
		}

		[Test]
		public void ALoopWithNothingToStopAt_FailsFast()
		{
			// 되돌아가는 고리는 허용해 둔 구조다. 멈출 자리가 없으면 **매달리지 말고 터진다** —
			// 프레임이 멎는 것보다 줄 번호가 찍히는 편이 고칠 수 있다.
			DialoguePlayback playback = Play(string.Join(NL,
				"## 시작",
				"> 욘: \"돈다.\"",
				"> -> 시작"));

			Assert.That(() => playback.Skip(), Throws.InvalidOperationException);
		}
	}
}
