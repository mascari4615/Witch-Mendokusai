using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — **아무도 안 부르는 장면**을 잡는 검사의 회귀 잠금.
	///
	/// ★ 왜 잠그나: 이 흠은 증상이 없다. 원고도 그래프도 게임도 멀쩡한데 그 장면만 조용히 빠진다.
	///   그래서 검사가 조용히 죽어도 아무도 모른다 — 시험이 유일한 파수꾼이다.
	///
	/// 반대쪽도 같이 잠근다: **멀쩡한 원고를 잡으면 안 된다.** 정상을 잡는 검사는 곧 무시당하고,
	/// 무시당하는 검사는 없는 것보다 나쁘다(오탐이 진짜를 덮는다).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueScriptReachabilityTest
	{
		private const string NL = "\n";

		private static ParsedDialogueScript Parse(params string[] lines)
		{
			return DialogueScriptParser.Parse(string.Join(NL, lines));
		}

		private static bool HasUnreachableIssue(ParsedDialogueScript parsed, string sectionName)
		{
			for (int i = 0; i < parsed.Issues.Count; i++)
			{
				if (parsed.Issues[i].Message.Contains(sectionName) && parsed.Issues[i].Message.Contains("가는 길이 없다"))
				{
					return true;
				}
			}
			return false;
		}

		[Test]
		public void SectionNobodyPointsAt_IsReported()
		{
			// 「재회」로 가려다 「재회2」라 쓴 흔한 사고 — 보내는 쪽은 멀쩡해 보이고, 받는 쪽만 굶는다.
			ParsedDialogueScript parsed = Parse(
				"## 시작",
				"> 욘: \"가자.\"",
				"> -> 재회2",
				"## 재회",
				"> 욘: \"오랜만이야.\"",
				"## 재회2",
				"> 욘: \"또 왔네.\"");

			Assert.That(HasUnreachableIssue(parsed, "재회"), Is.True,
				"아무도 안 가리키고 흘러 들어오지도 않는 장면 — 써 두었지만 절대 안 나온다");
			Assert.That(HasUnreachableIssue(parsed, "재회2"), Is.False, "가리켜진 쪽은 멀쩡하다");
		}

		[Test]
		public void FallingThroughFromThePreviousSection_CountsAsReachable()
		{
			// 종이에 쓴 순서대로 읽히는 게 기본이다 — 안 가리켜도 앞 장면에서 그냥 흘러 들어온다.
			ParsedDialogueScript parsed = Parse(
				"## 시작",
				"> 욘: \"음.\"",
				"## 다음",
				"> 욘: \"그래서?\"");

			Assert.That(parsed.HasIssues, Is.False, "멀쩡한 원고를 잡으면 그 검사는 곧 무시당한다");
		}

		[Test]
		public void UnconditionalGoto_StopsTheFallThrough()
		{
			// 조건 없는 갈 곳에서 흐름이 샌다 — 그 뒤 장면은 흘러 들어올 길이 없다.
			ParsedDialogueScript parsed = Parse(
				"## 시작",
				"> -> 끝",
				"## 사이",
				"> 욘: \"이건 안 나온다.\"",
				"## 끝",
				"> 욘: \"끝.\"");

			Assert.That(HasUnreachableIssue(parsed, "사이"), Is.True);
		}

		[Test]
		public void ConditionalGoto_DoesNotStopTheFallThrough()
		{
			// 조건이 거짓이면 그대로 흘러간다 — 조건부 갈 곳을 「샌다」로 세면 멀쩡한 원고를 잡는다.
			ParsedDialogueScript parsed = Parse(
				"## 시작",
				"> ?봤음 5200 -> 끝",
				"## 사이",
				"> 욘: \"이건 나온다.\"",
				"## 끝",
				"> 욘: \"끝.\"");

			Assert.That(parsed.HasIssues, Is.False);
		}

		[Test]
		public void ChoiceLeaksTheFlow_SoTheNextSectionNeedsAPointer()
		{
			// 선택지에서도 샌다 — 고르면 그리로 가고, 고를 게 없으면 대화가 끝난다.
			ParsedDialogueScript parsed = Parse(
				"## 시작",
				"> - 간다 -> 끝",
				"## 사이",
				"> 욘: \"이건 안 나온다.\"",
				"## 끝",
				"> 욘: \"끝.\"");

			Assert.That(HasUnreachableIssue(parsed, "사이"), Is.True);
			Assert.That(HasUnreachableIssue(parsed, "끝"), Is.False);
		}

		[Test]
		public void FirstSectionIsAlwaysReachable()
		{
			// 대화가 시작되는 자리다. 아무도 안 가리킨다고 「안 나온다」고 하면 모든 원고가 걸린다.
			ParsedDialogueScript parsed = Parse(
				"## 시작",
				"> 욘: \"하나뿐인 장면.\"");

			Assert.That(parsed.HasIssues, Is.False);
		}

		[Test]
		public void ALoopBackIsStillReachable()
		{
			// 되돌아가는 고리는 정상이다(허용해 둔 구조다) — 도달 검사가 고리에서 멈추면 안 된다.
			ParsedDialogueScript parsed = Parse(
				"## 시작",
				"> - 다시 묻는다 -> 되묻기",
				"## 되묻기",
				"> 욘: \"뭘?\"",
				"> -> 시작");

			Assert.That(parsed.HasIssues, Is.False);
		}
	}
}
