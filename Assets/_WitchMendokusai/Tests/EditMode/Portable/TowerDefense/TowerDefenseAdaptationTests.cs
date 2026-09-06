using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 적응 규칙 회귀 — 「한 번 찾은 정답」이 영원히 통하지 않게 하되, *봉인은 절대 아니게*.
	/// 이 균형이 깨지면 적응은 재미가 아니라 벌칙이 된다. TASK-WM-194.
	/// </summary>
	public class TowerDefenseAdaptationTests
	{
		[Test]
		public void 아무것도_안_썼으면_저항_없음()
		{
			TowerDefenseAdaptationState state = TowerDefenseAdaptation.From(0, 0, 0, 1f);

			Assert.IsFalse(state.HasAny);
		}

		[Test]
		public void 골고루_쓰면_저항이_안_붙는다()
		{
			// 규칙의 전부 = 「한 수단에만 기대지 마라」. 골고루면 대가가 없어야 한다.
			TowerDefenseAdaptationState state = TowerDefenseAdaptation.From(30, 30, 30, 1f);

			Assert.IsFalse(state.HasAny, "균등하게 썼는데 저항이 붙으면 오래 한 것 자체가 벌칙이 된다.");
		}

		[Test]
		public void 한_수단에만_기대면_그것에만_저항이_붙는다()
		{
			TowerDefenseAdaptationState state = TowerDefenseAdaptation.From(100, 5, 5, 1f);

			Assert.Greater(state.SlowResist, 0f);
			Assert.AreEqual(0f, state.SplashResist);
			Assert.AreEqual(0f, state.PierceResist);
		}

		[Test]
		public void 저항은_절대_절반을_넘지_않는다()
		{
			// 1 에 닿으면 그 전략은 못 쓰는 것이 되고, 그건 적응이 아니라 봉인이다.
			TowerDefenseAdaptationState state = TowerDefenseAdaptation.From(100000, 0, 0, 99f);

			Assert.LessOrEqual(state.SlowResist, TowerDefenseAdaptation.MAX_RESIST);
			Assert.Greater(state.SlowResist, 0f);
		}

		[Test]
		public void 민감도0이면_적응이_꺼진다()
		{
			TowerDefenseAdaptationState state = TowerDefenseAdaptation.From(100, 0, 0, 0f);

			Assert.IsFalse(state.HasAny);
		}

		[Test]
		public void 무엇에_익숙해졌는지_말해준다()
		{
			Assert.AreEqual("둔화에 익숙함", TowerDefenseAdaptation.Describe(TowerDefenseAdaptation.From(100, 5, 5, 1f)));
			Assert.AreEqual("광역에 익숙함", TowerDefenseAdaptation.Describe(TowerDefenseAdaptation.From(5, 100, 5, 1f)));
			Assert.AreEqual("관통에 익숙함", TowerDefenseAdaptation.Describe(TowerDefenseAdaptation.From(5, 5, 100, 1f)));
			Assert.AreEqual(string.Empty, TowerDefenseAdaptation.Describe(TowerDefenseAdaptation.From(10, 10, 10, 1f)));
		}
	}
}
