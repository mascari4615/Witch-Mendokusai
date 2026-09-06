using NUnit.Framework;
using WitchMendokusai.DomainSDK.Discovery;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 방치형 인형 도감이 판정 층 도감 조각을 탐. 가진 인형만 열림, 등록소를 거쳐도 같은 답, 채운 정도는 종류 수
	/// </summary>
	public sealed class IdleHeroDiscoveryTests
	{
		[SetUp]
		public void ClearSources() => DiscoveryUnlocks.Clear();

		[TearDown]
		public void ClearSourcesAfter() => DiscoveryUnlocks.Clear();

		[Test]
		public void OwnedHero_IsUnlocked_OthersAreNot()
		{
			IdleState state = new IdleState();
			state.Heroes.Add(new IdleHeroOwned(2));
			IdleHeroDiscovery source = new IdleHeroDiscovery(state);

			Assert.IsTrue(source.IsUnlocked(IdleHeroDiscovery.EntryIdOf(2)));
			Assert.IsFalse(source.IsUnlocked(IdleHeroDiscovery.EntryIdOf(3)));
			Assert.IsFalse(source.IsUnlocked("not-a-number"));
		}

		[Test]
		public void ThroughTheRegistry_SameAnswer()
		{
			IdleState state = new IdleState();
			state.Heroes.Add(new IdleHeroOwned(1));
			DiscoveryUnlocks.Register(new IdleHeroDiscovery(state));

			Assert.IsTrue(DiscoveryUnlocks.IsUnlocked(IdleHeroDiscovery.CATALOG_ID, IdleHeroDiscovery.EntryIdOf(1)));
			Assert.IsFalse(DiscoveryUnlocks.IsUnlocked(IdleHeroDiscovery.CATALOG_ID, IdleHeroDiscovery.EntryIdOf(0)));
		}

		[Test]
		public void Progress_CountsKinds_NotStars()
		{
			IdleState state = new IdleState();
			state.Heroes.Add(new IdleHeroOwned(0));
			state.Heroes.Add(new IdleHeroOwned(1));

			DiscoveryProgress progress = IdleHeroDiscovery.ProgressOf(state);

			Assert.AreEqual(IdleHeroes.Count, progress.Total);
			Assert.AreEqual(2, progress.Unlocked);
			Assert.IsFalse(progress.IsComplete);
		}

		/// <summary>점수는 종류와 별을 더하고, 배수는 공용 계단 셈과 같아야 함. 갈래가 따로 세던 시절의 값과 동일</summary>
		[Test]
		public void Multiplier_MatchesTheSharedStairs()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			for (int id = 0; id < tuning.DiscoveryStepScore; id++)
			{
				state.Heroes.Add(new IdleHeroOwned(id));
			}

			int score = IdleHeroes.DiscoveryScoreOf(state);
			double expected = DiscoveryTiers.MultiplierOf(score, tuning.DiscoveryStepScore, tuning.DiscoveryStepBonus);

			Assert.AreEqual(expected, IdleHeroes.DiscoveryMultiplierOf(state, tuning), 1e-12d);
			Assert.Greater(expected, 1d);
		}
	}
}
