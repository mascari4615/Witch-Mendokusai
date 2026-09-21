using NUnit.Framework;
using WitchMendokusai.DomainSDK.Discovery;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 방치형 인형 도감이 판정 층 도감 조각을 탐. 가진 인형만 열림, 등록소를 거쳐도 같은 답, 채운 정도는 종류 수
	/// </summary>
	public sealed class IdleDollDiscoveryTests
	{
		[SetUp]
		public void ClearSources() => DiscoveryUnlocks.Clear();

		[TearDown]
		public void ClearSourcesAfter() => DiscoveryUnlocks.Clear();

		[Test]
		public void OwnedDoll_IsUnlocked_OthersAreNot()
		{
			IdleState state = new IdleState();
			state.Dolls.Add(new IdleDollOwned(2));
			IdleDollDiscovery source = new IdleDollDiscovery(state);

			Assert.IsTrue(source.IsUnlocked(IdleDollDiscovery.EntryIdOf(2)));
			Assert.IsFalse(source.IsUnlocked(IdleDollDiscovery.EntryIdOf(3)));
			Assert.IsFalse(source.IsUnlocked("not-a-number"));
		}

		[Test]
		public void ThroughTheRegistry_SameAnswer()
		{
			IdleState state = new IdleState();
			state.Dolls.Add(new IdleDollOwned(1));
			DiscoveryUnlocks.Register(new IdleDollDiscovery(state));

			Assert.IsTrue(DiscoveryUnlocks.IsUnlocked(IdleDollDiscovery.CATALOG_ID, IdleDollDiscovery.EntryIdOf(1)));
			Assert.IsFalse(DiscoveryUnlocks.IsUnlocked(IdleDollDiscovery.CATALOG_ID, IdleDollDiscovery.EntryIdOf(0)));
		}

		[Test]
		public void Progress_CountsKinds_NotStars()
		{
			IdleState state = new IdleState();
			state.Dolls.Add(new IdleDollOwned(0));
			state.Dolls.Add(new IdleDollOwned(1));

			DiscoveryProgress progress = IdleDollDiscovery.ProgressOf(state);

			Assert.AreEqual(IdleDolls.Count, progress.Total);
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
				state.Dolls.Add(new IdleDollOwned(id));
			}

			int score = IdleDolls.DiscoveryScoreOf(state);
			double expected = DiscoveryTiers.MultiplierOf(score, tuning.DiscoveryStepScore, tuning.DiscoveryStepBonus);

			Assert.AreEqual(expected, IdleDolls.DiscoveryMultiplierOf(state, tuning), 1e-12d);
			Assert.Greater(expected, 1d);
		}
	}
}
