using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 상점 뽑기 넷 (사용자 2026-09-05). 묶음 뽑기, 묶음 보장, 픽업, 무료 상자
	///
	/// ★ 지키는 것: 묶음은 1회의 묶음 수 배 (할인 없음), 묶음 안에 보장 등급 하나,
	///   픽업은 같은 등급 안에서 더 잘 나오고 주기마다 바뀜, 무료 상자는 하루 한 번
	/// </summary>
	public sealed class IdleGachaShopTests
	{
		private const long SECONDS_PER_DAY = 86400L;

		/// <summary>그 날 번호의 경계에서 몇 초 지난 시각</summary>
		private static long At(IdleTuning tuning, long day, long into)
		{
			return day * SECONDS_PER_DAY + tuning.DayResetOffsetSeconds + into;
		}

		[Test]
		public void Batch_CostsBatchTimesOnePull_NoDiscount()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			int count = tuning.PullBatchCount;
			double one = IdleGacha.CostOf(state, tuning);

			state.Stones = IdleGacha.StoneCostOf(tuning) * count;
			state.Resource = one * count - 1d;
			Assert.IsFalse(IdleGacha.CanPullBatch(state, tuning), "골드가 1 모자란데 묶음이 됐다");

			state.Resource = one * count;
			List<IdleHeroPull> pulls = new List<IdleHeroPull>();
			Assert.IsTrue(IdleGacha.TryPullBatch(state, tuning, -1, pulls));

			Assert.AreEqual(count, pulls.Count);
			Assert.AreEqual(0d, state.Resource, 1e-6d, "값이 1회의 묶음 수 배가 아니다");
			Assert.AreEqual(0L, state.Stones, "환생석이 1회의 묶음 수 배가 아니다");
			Assert.AreEqual((long)count, state.PullsDone, "뽑은 횟수가 묶음 수만큼 안 올랐다");
		}

		/// <summary>★ 전부 일반만 나오는 판에서도 묶음 안에 보장 등급 하나. 자리는 마지막</summary>
		[Test]
		public void Batch_GuaranteesFloorGrade_OnTheLastSlot()
		{
			IdleTuning tuning = new IdleTuning();
			tuning.LegendChance = 0d;
			tuning.EpicChance = 0d;
			tuning.RareChance = 0d;
			tuning.PityPulls = 100000;
			tuning.PullBatchFloorGrade = (int)IdleHeroGrade.Epic;
			IdleState state = new IdleState();
			state.Stones = IdleGacha.BatchStoneCostOf(tuning);
			state.Resource = IdleGacha.BatchCostOf(state, tuning);

			List<IdleHeroPull> pulls = new List<IdleHeroPull>();
			Assert.IsTrue(IdleGacha.TryPullBatch(state, tuning, -1, pulls));

			int atFloor = 0;
			for (int index = 0; index < pulls.Count; index++)
			{
				atFloor += pulls[index].Grade >= IdleHeroGrade.Epic ? 1 : 0;
			}

			Assert.AreEqual(1, atFloor, "보장이 하나가 아니다");
			Assert.AreEqual(IdleHeroGrade.Epic, pulls[pulls.Count - 1].Grade, "보장이 마지막 자리가 아니다");
		}

		/// <summary>★ 하루 한 번. 같은 날 다시 못 열고, 날이 바뀌면 다시</summary>
		[Test]
		public void FreeBox_OpensOncePerDay()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			long now = At(tuning, 5L, 3600L);

			Assert.IsTrue(IdleFreeBox.IsReady(state, tuning, now));
			Assert.IsTrue(IdleFreeBox.TryOpen(state, tuning, now, out long stones));
			Assert.AreEqual(tuning.FreeBoxStones, stones);
			Assert.AreEqual(tuning.FreeBoxStones, state.Stones);

			Assert.IsFalse(IdleFreeBox.IsReady(state, tuning, now + 60L));
			Assert.IsFalse(IdleFreeBox.TryOpen(state, tuning, now + 60L, out long _), "같은 날 두 번 열렸다");
			Assert.AreEqual(SECONDS_PER_DAY - 3600L, IdleFreeBox.SecondsLeft(state, tuning, now), 1e-9d);

			Assert.IsTrue(IdleFreeBox.IsReady(state, tuning, now + SECONDS_PER_DAY));
			Assert.AreEqual(0d, IdleFreeBox.SecondsLeft(state, tuning, now + SECONDS_PER_DAY), 1e-9d);
		}

		[Test]
		public void FreeBoxDay_SurvivesTheSave()
		{
			IdleState state = new IdleState();
			state.FreeBoxDay = 77L;

			IdleState restored = new IdleState();
			restored.Load(state.Save());

			Assert.AreEqual(77L, restored.FreeBoxDay);
		}

		/// <summary>★ 픽업은 주기마다 바뀌고, 같은 주기 안에서는 그대로. 최고 등급에서만</summary>
		[Test]
		public void Pickup_RotatesEveryPeriod()
		{
			IdleTuning tuning = new IdleTuning();
			long days = tuning.PickupDays;

			int first = IdleGacha.PickupHeroOf(tuning, At(tuning, 0L, 10L));
			int stillFirst = IdleGacha.PickupHeroOf(tuning, At(tuning, days - 1L, 10L));
			int second = IdleGacha.PickupHeroOf(tuning, At(tuning, days, 10L));

			Assert.GreaterOrEqual(first, 0, "픽업이 없다");
			Assert.AreEqual(first, stillFirst, "같은 주기인데 픽업이 바뀌었다");
			Assert.AreNotEqual(first, second, "주기가 바뀌었는데 픽업이 그대로다");
			Assert.AreEqual(IdleHeroGrade.Legend, IdleHeroes.KindOf(first).Grade);
			Assert.AreEqual(days * SECONDS_PER_DAY - 10L, IdleGacha.PickupSecondsLeft(tuning, At(tuning, 0L, 10L)), 1e-9d);
		}

		/// <summary>★ 픽업 무게 2 면 같은 등급 넷 중 2/5 가 픽업. 균등이면 1/4</summary>
		[Test]
		public void Pickup_ComesOutMoreOften()
		{
			IdleTuning tuning = new IdleTuning();
			tuning.LegendChance = 1d;
			tuning.EpicChance = 0d;
			tuning.RareChance = 0d;
			tuning.PityPulls = 100000;
			tuning.PullCostRatio = 1d;
			IdleState state = new IdleState();

			List<int> legends = new List<int>();
			IdleHeroes.IdsOfGrade(IdleHeroGrade.Legend, legends);
			Assert.AreEqual(4, legends.Count, "이 시험은 최고 등급 넷을 전제한다");
			int pickup = legends[0];

			const int PULLS = 4000;
			state.Stones = PULLS;
			state.Resource = tuning.PullCostBase * PULLS;

			int hits = 0;
			for (int one = 0; one < PULLS; one++)
			{
				Assert.IsTrue(IdleGacha.TryPull(state, tuning, pickup, out IdleHeroPull got));
				hits += got.Id == pickup ? 1 : 0;
			}

			double share = (double)hits / PULLS;
			double expected = tuning.PickupWeight / (legends.Count - 1 + tuning.PickupWeight);
			TestContext.WriteLine("[픽업] " + hits + "/" + PULLS + " = " + share.ToString("P1") + " (기대 " + expected.ToString("P1") + ")");
			Assert.Greater(share, expected - 0.05d, "픽업이 더 잘 나오지 않는다");
			Assert.Less(share, expected + 0.05d, "픽업이 무게보다 더 나온다");
		}

		/// <summary>★ 픽업이 없는 판 (-1) 은 옛 뽑기와 같은 주사위 소비. 같은 씨앗이면 같은 얼굴</summary>
		[Test]
		public void NoPickup_IsTheOldRoll()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState one = new IdleState();
			IdleState other = new IdleState();
			one.Stones = 5L;
			other.Stones = 5L;
			one.Resource = 100000d;
			other.Resource = 100000d;

			for (int pull = 0; pull < 5; pull++)
			{
				Assert.IsTrue(IdleGacha.TryPull(one, tuning, out IdleHeroPull a));
				Assert.IsTrue(IdleGacha.TryPull(other, tuning, -1, out IdleHeroPull b));
				Assert.AreEqual(a.Id, b.Id);
			}
		}
	}
}
