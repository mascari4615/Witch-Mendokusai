using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>인형 레벨 (economy.md 표 3, U4). 골드로 올리고 환생 때 사라진다</summary>
	public sealed class IdleHeroLevelTests
	{
		private static IdleState WithHero(IdleTuning tuning, int id)
		{
			IdleState state = new IdleState();
			IdleHeroes.EnsureStarter(state);

			if (state.IndexOfHero(id) < 0)
			{
				state.Heroes.Add(new IdleHeroOwned(id));
			}

			return state;
		}

		/// <summary>★ 골드를 내고 오른다. 모자라면 아무 일도 안 일어난다</summary>
		[Test]
		public void RaisingALevel_CostsGold()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = WithHero(tuning, 0);
			int at = state.IndexOfHero(0);

			Assert.IsFalse(IdleHeroes.TryRaiseLevel(state, tuning, 0), "골드가 0 인데 올랐다");

			state.Resource = tuning.HeroLevelCostBase;
			Assert.IsTrue(IdleHeroes.TryRaiseLevel(state, tuning, 0));

			Assert.AreEqual(1, state.Heroes[at].Level);
			Assert.AreEqual(0d, state.Resource, 1e-9d, "값을 안 냈다");
		}

		/// <summary>★ 올릴수록 비싸진다. 안 그러면 골드가 많은 순간 레벨이 무한이 된다</summary>
		[Test]
		public void TheCost_ClimbsWithTheLevel()
		{
			IdleTuning tuning = new IdleTuning();
			IdleHeroOwned owned = new IdleHeroOwned(0);

			double first = IdleHeroes.LevelCostOf(owned, tuning);
			owned.Level = 10;
			double later = IdleHeroes.LevelCostOf(owned, tuning);

			Assert.Greater(later, first * 2d, "열 칸을 올렸는데 값이 두 배도 안 됐다");
		}

		/// <summary>★ 레벨이 판을 세게 만든다. 안 그러면 골드를 낼 이유가 없다</summary>
		[Test]
		public void Levels_MakeTheRunStronger()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = WithHero(tuning, 0);
			int at = state.IndexOfHero(0);
			IdleHeroAxis axis = IdleHeroes.KindOf(0).Axis;

			double before = IdleHeroes.AxisMultiplierOf(state, tuning, axis);

			IdleHeroOwned owned = state.Heroes[at];
			owned.Level = 50;
			state.Heroes[at] = owned;

			Assert.Greater(IdleHeroes.AxisMultiplierOf(state, tuning, axis), before,
				"레벨 50 인데 축 배수가 그대로다");
		}

		/// <summary>★ 레벨 열 칸이 ★ 하나쯤. 둘 다 올릴 이유가 남게</summary>
		[Test]
		public void TenLevels_AreAboutOneStar()
		{
			IdleTuning tuning = new IdleTuning();

			IdleHeroOwned leveled = new IdleHeroOwned(0);
			leveled.Level = 10;

			IdleHeroOwned starred = new IdleHeroOwned(0);
			starred.Stars = 1;

			Assert.AreEqual(IdleHeroes.GrowthOf(starred, tuning), IdleHeroes.GrowthOf(leveled, tuning), 1e-9d);
		}

		/// <summary>★ 환생이 레벨을 지운다 (U4). 보유와 ★ 은 남는다</summary>
		[Test]
		public void Prestige_ForgetsLevels_ButKeepsStars()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = WithHero(tuning, 0);
			int at = state.IndexOfHero(0);

			IdleHeroOwned owned = state.Heroes[at];
			owned.Level = 30;
			owned.Stars = 2;
			state.Heroes[at] = owned;

			int heroesBefore = state.Heroes.Count;
			state.Stage = 200;
			state.BestStage = 200;
			IdleModel.TryPrestige(state, tuning, out long _);

			Assert.AreEqual(heroesBefore, state.Heroes.Count, "환생이 인형을 지웠다");
			Assert.AreEqual(0, state.Heroes[at].Level, "환생했는데 레벨이 남았다");
			Assert.AreEqual(2, state.Heroes[at].Stars, "환생이 ★ 을 지웠다");
		}

		/// <summary>★ 저장을 건넌다</summary>
		[Test]
		public void Levels_SurviveTheSave()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = WithHero(tuning, 0);
			int at = state.IndexOfHero(0);

			IdleHeroOwned owned = state.Heroes[at];
			owned.Level = 7;
			state.Heroes[at] = owned;

			IdleState back = new IdleState();
			back.Load(state.Save());

			Assert.AreEqual(7, back.Heroes[back.IndexOfHero(0)].Level);
		}
	}
}
