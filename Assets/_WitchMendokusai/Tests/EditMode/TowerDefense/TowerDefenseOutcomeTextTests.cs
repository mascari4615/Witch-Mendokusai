using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 결말 배너가 *무슨 일이 일어났는지* 말하나 (TASK-WM-194).
	///
	/// ★ 라이브에서 잡힌 사고를 못 박는다: 목숨 0 으로 진 판인데 화면엔 「최고 기록 — 48초 버팀」만
	///   떴다. 이긴 판은 「개척 성공」이라 또렷이 말하면서 진 판은 축하 문구처럼 보이는 줄 하나로
	///   끝났다 — 화면만 보면 이겼는지 졌는지 알 수 없었다.
	/// </summary>
	public class TowerDefenseOutcomeTextTests
	{
		[Test]
		public void 진_판은_졌다고_말한다()
		{
			string text = TowerDefenseOutcomeText.Build(TowerDefenseOutcome.Defeat, "48초", 0, 48, 40, false);

			StringAssert.Contains("실패", text, "졌는데 졌다는 말이 없다.");
			StringAssert.Contains("48초", text);
		}

		[Test]
		public void 기록을_깨도_진_것은_진_것이다()
		{
			// ★ 사고의 핵심 — 져도 기록은 깨질 수 있고, 그때 기록이 결과를 가렸다.
			string text = TowerDefenseOutcomeText.Build(TowerDefenseOutcome.Defeat, "48초", 0, 48, 40, true);

			StringAssert.Contains("실패", text, "기록 경신이 「졌다」를 밀어냈다.");
			StringAssert.Contains("최고 기록", text, "기록을 깼는데 그 말이 없다.");
			Assert.IsFalse(text.StartsWith("최고 기록"), "첫 마디가 축하면 진 판이 이긴 판처럼 보인다.");
		}

		[Test]
		public void 이긴_판은_이겼다고_말한다()
		{
			string text = TowerDefenseOutcomeText.Build(TowerDefenseOutcome.Victory, "3분 20초", 8, 200, 120, true);

			StringAssert.Contains("성공", text);
			StringAssert.Contains("둥지 8곳", text, "이긴 방식(둥지를 다 부숨)이 안 보인다.");
		}

		[Test]
		public void 성적과_이전_기록을_항상_같이_보여준다()
		{
			// 비교할 수 없는 기록은 기록이 아니다 — 「다시 도전」이 이유를 가지려면 둘이 같이 보여야 한다.
			string beaten = TowerDefenseOutcomeText.Build(TowerDefenseOutcome.Defeat, "60초", 0, 60, 40, true);
			string kept = TowerDefenseOutcomeText.Build(TowerDefenseOutcome.Defeat, "30초", 0, 30, 40, false);

			StringAssert.Contains("점수 60", beaten);
			StringAssert.Contains("이전 최고 40", beaten);
			StringAssert.Contains("점수 30", kept);
			StringAssert.Contains("최고 40", kept);
		}

		[Test]
		public void 둥지를_안_부쉈으면_그_말을_안_한다()
		{
			// 0 곳을 「0곳 부숨」이라 적으면 아무 일도 없던 것을 성과처럼 말하는 셈이다.
			string text = TowerDefenseOutcomeText.Build(TowerDefenseOutcome.Defeat, "10초", 0, 10, 10, false);

			StringAssert.DoesNotContain("둥지", text);
		}
	}
}
