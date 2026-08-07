using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — **전부 조건부인 선택지 묶음**을 잡는 검사의 회귀 잠금.
	///
	/// ★ 왜 이 흠이 고약한가: 재현이 안 된다. 조건이 하나라도 참인 저장에서는 멀쩡히 돌아가고,
	///   전부 거짓인 저장에서만 「말하다 말고 대화가 툭 끊긴다」로 보인다.
	///   재생 쪽 처리(고를 게 없으면 끝낸다)는 옳다 — 고칠 곳은 원고고, 알려야 할 자리는 쓰는 자리다.
	///
	/// 재생 쪽 실제 동작도 같이 붙들어 둔다. 검사만 있고 동작이 바뀌면 경고가 거짓말이 된다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueScriptChoiceWayOutTest
	{
		private const string NL = "\n";

		private static ParsedDialogueScript Parse(params string[] lines)
		{
			return DialogueScriptParser.Parse(string.Join(NL, lines));
		}

		private static bool HasWayOutIssue(ParsedDialogueScript parsed)
		{
			for (int i = 0; i < parsed.Issues.Count; i++)
			{
				if (parsed.Issues[i].Message.Contains("전부 조건부"))
				{
					return true;
				}
			}
			return false;
		}

		[Test]
		public void AllChoicesConditional_IsReported()
		{
			ParsedDialogueScript parsed = Parse(
				"## 문앞",
				"> - 열쇠를 쓴다 [아이템 1001] -> 열림",
				"> - 부순다 [아이템 1002] -> 열림",
				"## 열림",
				"> 욘: \"열렸다.\"");

			Assert.That(HasWayOutIssue(parsed), Is.True,
				"열쇠도 망치도 없는 저장에서는 아무 칸도 안 뜨고 대화가 끊긴다");
		}

		[Test]
		public void OneUnconditionalChoice_IsEnough()
		{
			// 퇴로가 하나 있으면 된다 — 조건이 다 거짓이어도 「그냥 간다」가 남는다.
			ParsedDialogueScript parsed = Parse(
				"## 문앞",
				"> - 열쇠를 쓴다 [아이템 1001] -> 열림",
				"> - 그냥 간다 -> 문앞",
				"## 열림",
				"> 욘: \"열렸다.\"");

			Assert.That(parsed.HasIssues, Is.False, "멀쩡한 원고를 잡으면 그 검사는 곧 무시당한다");
		}

		[Test]
		public void PlainChoices_AreNeverReported()
		{
			ParsedDialogueScript parsed = Parse(
				"## 갈림",
				"> - 왼쪽 -> 왼",
				"> - 오른쪽 -> 오른",
				"## 왼",
				"> 욘: \"왼.\"",
				"## 오른",
				"> 욘: \"오른.\"");

			Assert.That(HasWayOutIssue(parsed), Is.False);
		}

		[Test]
		public void WhenNothingIsAvailable_ThePlaybackReallyEnds()
		{
			// 경고가 말하는 그 일이 실제로 일어나는지 — 동작이 바뀌면 경고 문구가 거짓말이 된다.
			DialogueItemBridge.Clear(DialogueItemBridge.Current);

			ParsedDialogueScript parsed = Parse(
				"## 문앞",
				"> - 열쇠를 쓴다 [아이템 1001] -> 열림",
				"## 열림",
				"> 욘: \"열렸다.\"");

			DialoguePlayback playback = new(DialogueScriptGraphBuilder.Build(parsed));
			playback.Begin();

			Assert.That(playback.ReachedEnd, Is.True,
				"고를 게 없는 화면에 플레이어를 붙잡아 두는 것보다 끝내는 쪽이 낫다 — 대신 원고에 경고를 남긴다");
		}
	}
}
