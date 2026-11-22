using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class DeckOfferWeightsTests
	{
		private const int TRIALS = 20000;
		private const int SEED = 4615;

		private static readonly float[] DEFAULT_TABLE = { 1f, 4f };

		[Test]
		public void 표는_받은_장수로_찾고_넘치면_마지막_값()
		{
			Assert.AreEqual(1f, DeckOfferWeights.Weight(DEFAULT_TABLE, 0));
			Assert.AreEqual(4f, DeckOfferWeights.Weight(DEFAULT_TABLE, 1));
			Assert.AreEqual(4f, DeckOfferWeights.Weight(DEFAULT_TABLE, 9));
			Assert.AreEqual(1f, DeckOfferWeights.Weight(DEFAULT_TABLE, -1));
		}

		[Test]
		public void 표가_비면_모두_1()
		{
			Assert.AreEqual(1f, DeckOfferWeights.Weight(Array.Empty<float>(), 3));
			Assert.AreEqual(1f, DeckOfferWeights.Weight(null, 3));
		}

		[Test]
		public void 굴림이_가중치_구간대로_고른다()
		{
			// 덱 0 은 가중치 1 (굴림 0~0.2), 덱 1 은 4 (0.2~1)
			Func<int, float> weightOf = deck => deck == 0 ? 1f : 4f;

			List<int> lowRoll = DeckOfferWeights.WeightedOrder(new[] { 0, 1 }, weightOf, maxExclusive => (int)(maxExclusive * 0.19d));
			List<int> highRoll = DeckOfferWeights.WeightedOrder(new[] { 0, 1 }, weightOf, maxExclusive => (int)(maxExclusive * 0.21d));

			Assert.AreEqual(0, lowRoll[0]);
			Assert.AreEqual(1, highRoll[0]);
		}

		[Test]
		public void 가장_큰_굴림은_마지막_덱()
		{
			List<int> ordered = DeckOfferWeights.WeightedOrder(new[] { 0, 1, 2 }, deck => 1f, maxExclusive => maxExclusive - 1);

			Assert.AreEqual(2, ordered[0]);
		}

		[Test]
		public void 덱이_하나면_그대로()
		{
			List<int> ordered = DeckOfferWeights.WeightedOrder(new[] { 5 }, deck => 4f, maxExclusive => maxExclusive - 1);

			CollectionAssert.AreEqual(new[] { 5 }, ordered);
		}

		[Test]
		public void 순서는_모든_덱을_한_번씩_담는다()
		{
			Random random = new Random(SEED);
			List<int> ordered = DeckOfferWeights.WeightedOrder(new[] { 0, 1, 2, 3, 4 }, deck => deck + 1f, maxExclusive => random.Next(0, maxExclusive));

			CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4 }, ordered);
		}

		[Test]
		public void 가중치_0_이하는_빠지지_않고_맨_뒤()
		{
			Random random = new Random(SEED);
			for (int trial = 0; trial < 200; trial++)
			{
				List<int> ordered = DeckOfferWeights.WeightedOrder(new[] { 0, 1, 2, 3 }, deck => deck == 0 ? 0f : deck == 1 ? -3f : 1f, maxExclusive => random.Next(0, maxExclusive));

				CollectionAssert.AreEquivalent(new[] { 2, 3 }, ordered.Take(2));
				CollectionAssert.AreEquivalent(new[] { 0, 1 }, ordered.Skip(2));
			}
		}

		[Test]
		public void 받은_덱은_네_배_자주_첫_칸에_온다()
		{
			// 덱 2 만 한 장 받음: 첫 칸 확률 4 / (1 + 1 + 4 + 1) = 0.571
			int[] picks = { 0, 0, 1, 0 };
			double ratio = FirstSlotRatio(deck => DeckOfferWeights.Weight(DEFAULT_TABLE, picks[deck]), targetDeck: 2);

			Assert.AreEqual(4d / 7d, ratio, 0.015);
		}

		[Test]
		public void 같은_가중치면_균등하다()
		{
			int[] picks = { 0, 3, 1, 0 };
			double ratio = FirstSlotRatio(deck => DeckOfferWeights.Weight(new[] { 2f, 2f }, picks[deck]), targetDeck: 2);

			Assert.AreEqual(0.25d, ratio, 0.015);
		}

		[Test]
		public void 랜덤_모드에서_받은_덱이_세_칸에_더_자주_든다()
		{
			// 보유 5, 새 장비 없음, 보이는 칸 3. 균등이면 0.6, 덱 4 가중치 4 면 그보다 높음
			Random random = new Random(SEED);
			int included = 0;
			for (int trial = 0; trial < TRIALS; trial++)
			{
				List<RunDeckOffer> offers = DungeonRunModeRules.BuildDeckOffers(
					DungeonRunMode.Random, new[] { 0, 1, 2, 3, 4 }, Array.Empty<int>(), 6, 6, 3, 1,
					maxExclusive => random.Next(0, maxExclusive),
					deck => deck == 4 ? 4f : 1f);
				if (offers.Any(offer => offer.DeckIndex == 4))
					included++;
			}

			Assert.Greater(included / (double)TRIALS, 0.85d);
		}

		private static double FirstSlotRatio(Func<int, float> weightOf, int targetDeck)
		{
			Random random = new Random(SEED);
			int first = 0;
			for (int trial = 0; trial < TRIALS; trial++)
			{
				List<int> ordered = DeckOfferWeights.WeightedOrder(new[] { 0, 1, 2, 3 }, weightOf, maxExclusive => random.Next(0, maxExclusive));
				if (ordered[0] == targetDeck)
					first++;
			}
			return first / (double)TRIALS;
		}
	}
}
