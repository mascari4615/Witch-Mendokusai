using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class DungeonRunModeRulesTests
	{
		private const int SLOT_CAPACITY = 6;
		private const int VISIBLE_COUNT = 3;
		private const int NEW_OFFER_COUNT = 1;

		private static int FirstIndex(int maxExclusive) => 0;

		private static List<RunDeckOffer> Build(DungeonRunMode mode, int[] ownedDecks, int[] newIds, int occupiedSlots, int newOfferCount = NEW_OFFER_COUNT, Func<int, int> nextIndex = null)
			=> DungeonRunModeRules.BuildDeckOffers(mode, ownedDecks, newIds, occupiedSlots, SLOT_CAPACITY, VISIBLE_COUNT, newOfferCount, nextIndex ?? FirstIndex);

		[Test]
		public void 장착_모드는_보유_덱만_전부_낸다()
		{
			List<RunDeckOffer> offers = Build(DungeonRunMode.Equipped, new[] { 0, 1, 2, 3 }, new[] { 100, 101 }, occupiedSlots: 0);

			Assert.AreEqual(4, offers.Count);
			Assert.IsTrue(offers.All(offer => offer.IsNew == false));
			CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, offers.Select(offer => offer.DeckIndex));
		}

		[Test]
		public void 장착_모드_섞기는_기존_순서와_난수_소비가_같다()
		{
			int[] legacy = { 0, 1, 2, 3 };
			Random legacyRandom = new Random(4615);
			for (int i = legacy.Length - 1; i > 0; i--)
			{
				int otherIndex = legacyRandom.Next(0, i + 1);
				(legacy[i], legacy[otherIndex]) = (legacy[otherIndex], legacy[i]);
			}

			Random random = new Random(4615);
			List<RunDeckOffer> offers = Build(DungeonRunMode.Equipped, new[] { 0, 1, 2, 3 }, Array.Empty<int>(), 0, nextIndex: maxExclusive => random.Next(0, maxExclusive));

			CollectionAssert.AreEqual(legacy, offers.Select(offer => offer.DeckIndex));
		}

		[Test]
		public void 랜덤_모드는_보유_덱과_새_장비를_설정값대로_섞는다()
		{
			List<RunDeckOffer> offers = Build(DungeonRunMode.Random, new[] { 0, 1, 2, 3 }, new[] { 100, 101, 102 }, occupiedSlots: 3);

			Assert.AreEqual(VISIBLE_COUNT, offers.Count);
			Assert.AreEqual(NEW_OFFER_COUNT, offers.Count(offer => offer.IsNew));
		}

		[Test]
		public void 칸이_차면_새_장비가_빠진다()
		{
			List<RunDeckOffer> offers = Build(DungeonRunMode.Random, new[] { 0, 1, 2, 3 }, new[] { 100, 101 }, occupiedSlots: SLOT_CAPACITY);

			Assert.AreEqual(VISIBLE_COUNT, offers.Count);
			Assert.IsTrue(offers.All(offer => offer.IsNew == false));
		}

		[Test]
		public void 칸이_찼고_보유_덱이_소진이면_후보가_없다()
		{
			List<RunDeckOffer> offers = Build(DungeonRunMode.Random, Array.Empty<int>(), new[] { 100, 101 }, occupiedSlots: SLOT_CAPACITY);

			Assert.AreEqual(0, offers.Count);
		}

		[Test]
		public void 보유_덱이_모자라면_새_장비로_채운다()
		{
			List<RunDeckOffer> offers = Build(DungeonRunMode.Random, new[] { 0 }, new[] { 100, 101, 102 }, occupiedSlots: 1);

			Assert.AreEqual(VISIBLE_COUNT, offers.Count);
			Assert.AreEqual(2, offers.Count(offer => offer.IsNew));
		}

		[Test]
		public void 새_장비가_모자라면_보유_덱으로_채운다()
		{
			List<RunDeckOffer> offers = Build(DungeonRunMode.Random, new[] { 0, 1, 2, 3 }, Array.Empty<int>(), occupiedSlots: 1);

			Assert.AreEqual(VISIBLE_COUNT, offers.Count);
			Assert.IsTrue(offers.All(offer => offer.IsNew == false));
		}

		[Test]
		public void 후보는_겹치지_않고_보이는_칸_수를_넘지_않는다()
		{
			Random random = new Random(7);
			for (int trial = 0; trial < 200; trial++)
			{
				List<RunDeckOffer> offers = Build(DungeonRunMode.Random, new[] { 0, 1, 2, 3, 4 }, new[] { 100, 101, 102, 103 }, occupiedSlots: 2, newOfferCount: 2, nextIndex: maxExclusive => random.Next(0, maxExclusive));

				Assert.AreEqual(VISIBLE_COUNT, offers.Count);
				Assert.AreEqual(2, offers.Count(offer => offer.IsNew));
				Assert.AreEqual(offers.Count, offers.Distinct().Count());
			}
		}

		[Test]
		public void 후보_목록은_가진_것과_제외_대상을_뺀다()
		{
			List<int> candidates = DungeonRunModeRules.NewEquipmentCandidates(new[] { 100, 101, 101, 102, 103 }, new[] { 101, 103 });

			CollectionAssert.AreEqual(new[] { 100, 102 }, candidates);
		}

		[Test]
		public void 새_장비_제시는_랜덤_모드의_빈_칸에서만()
		{
			Assert.IsFalse(DungeonRunModeRules.CanOfferNewEquipment(DungeonRunMode.Equipped, 0, SLOT_CAPACITY));
			Assert.IsTrue(DungeonRunModeRules.CanOfferNewEquipment(DungeonRunMode.Random, SLOT_CAPACITY - 1, SLOT_CAPACITY));
			Assert.IsFalse(DungeonRunModeRules.CanOfferNewEquipment(DungeonRunMode.Random, SLOT_CAPACITY, SLOT_CAPACITY));
			Assert.IsTrue(DungeonRunModeRules.UsesSavedEquipment(DungeonRunMode.Equipped));
			Assert.IsFalse(DungeonRunModeRules.UsesSavedEquipment(DungeonRunMode.Random));
		}
	}
}
