using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 업적 별도 시스템 (2026-11-22). 값에서 단계 고르기, 장부는 오르기만, 장부가 플랫폼에 내는 단계 목록, 옛 세이브 옮기기
	/// </summary>
	public sealed class AchievementBookTest
	{
		private static readonly int[] HUNTER_TARGETS = { 100, 500, 2000 };
		private const string TODAY = "2026-11-22";

		[TestCase(0, 0)]
		[TestCase(99, 0)]
		[TestCase(100, 1)]
		[TestCase(499, 1)]
		[TestCase(500, 2)]
		[TestCase(1999, 2)]
		[TestCase(2000, 3)]
		[TestCase(999999, 3)]
		public void Tier_is_the_highest_target_reached(int value, int expectedTier)
		{
			Assert.That(AchievementTiers.Resolve(value, HUNTER_TARGETS), Is.EqualTo(expectedTier));
		}

		[Test]
		public void Bad_target_tables_are_rejected()
		{
			Assert.Throws<ArgumentException>(() => AchievementTiers.Validate("empty", new int[0]));
			Assert.Throws<ArgumentException>(() => AchievementTiers.Validate("four", new[] { 1, 2, 3, 4 }));
			Assert.Throws<ArgumentException>(() => AchievementTiers.Validate("zero", new[] { 0, 10 }));
			Assert.Throws<ArgumentException>(() => AchievementTiers.Validate("descending", new[] { 10, 5 }));
			Assert.DoesNotThrow(() => AchievementTiers.Validate("ok", HUNTER_TARGETS));
		}

		[Test]
		public void Book_tier_only_goes_up_and_keeps_the_first_date()
		{
			AchievementBook book = new(new Dictionary<int, AchievementRecord>());

			int beforeFirst = book.Advance(101, 2, TODAY);
			int beforeLower = book.Advance(101, 1, "2026-12-01");

			Assert.That(beforeFirst, Is.EqualTo(0));
			Assert.That(beforeLower, Is.EqualTo(2), "낮은 단계로 내려가지 않음");
			Assert.That(book.TierOf(101), Is.EqualTo(2));
			Assert.That(book.UnlockedOnOf(101), Is.EqualTo(TODAY));
			Assert.That(book.TierOf(999), Is.EqualTo(0));
			Assert.That(book.UnlockedOnOf(999), Is.Null);
		}

		[Test]
		public void Ledger_lists_every_reached_tier_so_platform_links_on_lower_tiers_still_fire()
		{
			AchievementBook book = new(new Dictionary<int, AchievementRecord>());
			book.Advance(102, 3, TODAY);
			book.Advance(201, 1, TODAY);

			Assert.That(book.UnlockedSteps, Is.EquivalentTo(new[]
			{
				new AchievementStep(102, 1),
				new AchievementStep(102, 2),
				new AchievementStep(102, 3),
				new AchievementStep(201, 1),
			}));
			Assert.That(book.UnlockedCount, Is.EqualTo(2));
			Assert.That(book.CountAtTier(3), Is.EqualTo(1));
			Assert.That(book.CountAtTier(1), Is.EqualTo(1));
		}

		[Test]
		public void Save_slice_round_trips_records()
		{
			AchievementSaveSlice slice = new();
			slice.Reset();
			new AchievementBook(slice.Records).Advance(101, 2, TODAY);

			AchievementSaveSlice loaded = new();
			loaded.Reset();
			loaded.Restore(slice.Capture());

			Assert.That(loaded.Records[101].Tier, Is.EqualTo(2));
			Assert.That(loaded.Records[101].UnlockedOn, Is.EqualTo(TODAY));
			Assert.That(loaded.LegacyCompletedQuestIds, Is.Empty);
		}

		[Test]
		public void Legacy_save_keeps_only_completed_quests_for_migration()
		{
			GameData legacy = new()
			{
				questStates = new Dictionary<int, int>
				{
					{ 500, (int)QuestState.Completed },
					{ 501, (int)QuestState.Unlocked },
					{ 1001, (int)QuestState.Completed },
				},
			};
			AchievementSaveSlice slice = new();
			slice.Reset();

			slice.RestoreLegacy(legacy);

			Assert.That(slice.Records, Is.Empty);
			Assert.That(slice.LegacyCompletedQuestIds, Is.EquivalentTo(new[] { 500, 1001 }));
		}
	}
}
