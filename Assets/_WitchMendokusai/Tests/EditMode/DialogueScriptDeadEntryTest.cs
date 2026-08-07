using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — **이미 다른 데로 보낸 뒤에 더 쓴 줄**을 잡는 검사의 회귀 잠금.
	///
	/// ★ 왜 도달 검사로는 못 잡나: 그쪽은 **장면 단위**다. 장면 자체는 멀쩡히 도달하고,
	///   그 안의 뒷줄만 굶는다. 화면엔 흔적이 없고 「분명 썼는데 안 나온다」로만 보인다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueScriptDeadEntryTest
	{
		private const string NL = "\n";

		private static ParsedDialogueScript Parse(params string[] lines)
		{
			return DialogueScriptParser.Parse(string.Join(NL, lines));
		}

		private static bool HasDeadEntryIssue(ParsedDialogueScript parsed)
		{
			for (int i = 0; i < parsed.Issues.Count; i++)
			{
				if (parsed.Issues[i].Message.Contains("절대 안 나온다"))
				{
					return true;
				}
			}
			return false;
		}

		[Test]
		public void LinesAfterAGoto_AreReported()
		{
			ParsedDialogueScript parsed = Parse(
				"## 시작",
				"> 욘: \"간다.\"",
				"> -> 끝",
				"> 욘: \"이 줄은 안 나온다.\"",
				"## 끝",
				"> 욘: \"끝.\"");

			Assert.That(HasDeadEntryIssue(parsed), Is.True);
		}

		[Test]
		public void LinesAfterAChoice_AreReported()
		{
			// 선택지도 그 자리에서 흐름을 가로챈다 — 고르면 그리로 가고, 아래로는 안 내려온다.
			ParsedDialogueScript parsed = Parse(
				"## 갈림",
				"> - 간다 -> 끝",
				"> 욘: \"이 줄은 안 나온다.\"",
				"## 끝",
				"> 욘: \"끝.\"");

			Assert.That(HasDeadEntryIssue(parsed), Is.True);
		}

		[Test]
		public void AGotoAtTheEnd_IsFine()
		{
			// 제일 흔한 정상 모양이다. 이걸 잡으면 거의 모든 원고가 걸린다.
			ParsedDialogueScript parsed = Parse(
				"## 시작",
				"> 욘: \"간다.\"",
				"> -> 끝",
				"## 끝",
				"> 욘: \"끝.\"");

			Assert.That(parsed.HasIssues, Is.False);
		}

		[Test]
		public void ConditionalGoto_DoesNotKillTheLinesBelow()
		{
			// 조건이 거짓이면 그대로 아래로 읽어 내려간다 — 세면 멀쩡한 원고를 잡는다.
			ParsedDialogueScript parsed = Parse(
				"## 시작",
				"> ?봤음 5200 -> 끝",
				"> 욘: \"처음 보는 얼굴이네.\"",
				"## 끝",
				"> 욘: \"또 왔구나.\"");

			Assert.That(parsed.HasIssues, Is.False);
		}

		[Test]
		public void OnlyTheFirstDeadLineIsReported()
		{
			// 원인은 하나(위에서 보냈다)다. 굶은 줄마다 한 번씩 떠들면 진짜 원인이 파묻힌다.
			ParsedDialogueScript parsed = Parse(
				"## 시작",
				"> -> 끝",
				"> 욘: \"하나.\"",
				"> 욘: \"둘.\"",
				"> 욘: \"셋.\"",
				"## 끝",
				"> 욘: \"끝.\"");

			int deadEntryIssues = 0;
			for (int i = 0; i < parsed.Issues.Count; i++)
			{
				if (parsed.Issues[i].Message.Contains("절대 안 나온다"))
				{
					deadEntryIssues++;
				}
			}

			Assert.That(deadEntryIssues, Is.EqualTo(1));
		}
	}
}
