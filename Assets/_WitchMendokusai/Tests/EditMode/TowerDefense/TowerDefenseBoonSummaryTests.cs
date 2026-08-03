using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 「내가 뭘 골랐더라」를 화면이 말하는가 (TASK-WM-194).
	/// 예전엔 열넷 중 셋만 말해서, 나머지를 고르면 화면이 침묵했다 — 골라도 아무 일 없어 보였다.
	/// </summary>
	public class TowerDefenseBoonSummaryTests
	{
		private static TowerDefenseBoon Boon(TowerDefenseBoonKind kind, float magnitude)
		{
			return new TowerDefenseBoon(kind, magnitude, "이름", "설명");
		}

		[Test]
		public void 안_골랐으면_아무_말도_안_한다()
		{
			Assert.IsEmpty(new TowerDefenseBoonState().Describe());
		}

		[Test]
		public void 즉시효과만_골라도_장수는_말한다()
		{
			// 쌓이는 수치가 없는 카드다 — 그래도 화면이 빈칸이면 「골랐는데 아무 일도 없다」가 된다.
			TowerDefenseBoonState state = new();
			state.Take(Boon(TowerDefenseBoonKind.Windfall, 70f));

			StringAssert.Contains("1장", state.Describe());
		}

		[Test]
		public void 쌓이는_것은_전부_말한다()
		{
			TowerDefenseBoonState state = new();
			state.Take(Boon(TowerDefenseBoonKind.Vision, 0.2f));
			state.Take(Boon(TowerDefenseBoonKind.HarvestYield, 0.3f));
			state.Take(Boon(TowerDefenseBoonKind.EnemySlow, 0.1f));

			string text = state.Describe();

			StringAssert.Contains("3장", text);
			StringAssert.Contains("시야 +20%", text);
			StringAssert.Contains("채집 +30%", text);
			StringAssert.Contains("마수둔화 +10%", text);
		}

		[Test]
		public void 새_판이면_요약도_비워진다()
		{
			TowerDefenseBoonState state = new();
			state.Take(Boon(TowerDefenseBoonKind.Firepower, 0.5f));

			state.Reset();

			Assert.IsEmpty(state.Describe());
		}
	}
}
