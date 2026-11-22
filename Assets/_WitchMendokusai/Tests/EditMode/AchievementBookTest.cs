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
		public void Legacy_tier_must_be_inside_the_achievement_tiers()
		{
			Assert.Throws<ArgumentException>(() => AchievementTiers.ValidateLegacyTier("zero", 0, 3));
			Assert.Throws<ArgumentException>(() => AchievementTiers.ValidateLegacyTier("over", 4, 3));
			Assert.DoesNotThrow(() => AchievementTiers.ValidateLegacyTier("boss", 3, 3));
		}

		[Test]
		public void Book_tier_only_goes_up_and_dates_every_new_tier()
		{
			AchievementBook book = new(new Dictionary<int, AchievementRecord>());

			int beforeFirst = book.Advance(101, 2, TODAY);
			int beforeLower = book.Advance(101, 1, "2026-12-01");
			int beforeThird = book.Advance(101, 3, "2026-12-05");

			Assert.That(beforeFirst, Is.EqualTo(0));
			Assert.That(beforeLower, Is.EqualTo(2), "낮은 단계로 내려가지 않음");
			Assert.That(beforeThird, Is.EqualTo(2));
			Assert.That(book.TierOf(101), Is.EqualTo(3));
			Assert.That(book.DateOf(101, 1), Is.EqualTo(TODAY), "한 번에 건너뛴 단계도 그날 날짜");
			Assert.That(book.DateOf(101, 2), Is.EqualTo(TODAY));
			Assert.That(book.DateOf(101, 3), Is.EqualTo("2026-12-05"));
			Assert.That(book.LatestDateOf(101), Is.EqualTo("2026-12-05"));
			Assert.That(book.TierOf(999), Is.EqualTo(0));
			Assert.That(book.DateOf(999, 1), Is.Null);
			Assert.That(book.DateOf(101, 0), Is.Null);
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
			Assert.That(book.CountAtLeast(3), Is.EqualTo(1));
			Assert.That(book.CountAtLeast(2), Is.EqualTo(1));
			Assert.That(book.CountAtLeast(1), Is.EqualTo(2), "단계별 메달 수는 그 단계 이상");
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
			Assert.That(loaded.Records[101].TierDates, Is.EqualTo(new[] { TODAY, TODAY }));
			Assert.That(loaded.LegacyCompletedQuestIds, Is.Empty);
		}

		[Test]
		public void Save_from_before_tier_dates_keeps_its_one_date_on_the_current_tier()
		{
			const string OLD_SAVE = @"{""Records"":{""102"":{""Tier"":3,""UnlockedOn"":""2026-11-20""}}}";
			AchievementSaveSlice slice = new();
			slice.Reset();

			slice.Restore(OLD_SAVE);
			AchievementBook book = new(slice.Records);

			Assert.That(book.TierOf(102), Is.EqualTo(3));
			Assert.That(book.DateOf(102, 3), Is.EqualTo("2026-11-20"));
			Assert.That(book.DateOf(102, 1), Is.Null, "아래 단계 날짜는 모름");
			Assert.That(slice.Records[102].UnlockedOn, Is.Null, "옮긴 뒤 옛 칸은 비움");
			Assert.That(slice.Capture(), Does.Not.Contain("UnlockedOn"));
			Assert.That(slice.WallPins, Is.Empty);
			Assert.That(slice.WallEdited, Is.False);
		}

		[Test]
		public void Untouched_wall_fills_with_highest_tiers_in_screen_order()
		{
			AchievementSaveSlice slice = new();
			slice.Reset();
			AchievementBook book = new(slice.Records);
			book.Advance(1, 1, TODAY);
			book.Advance(2, 3, TODAY);
			book.Advance(101, 2, TODAY);
			book.Advance(102, 3, TODAY);
			AchievementWall wall = new(slice);

			wall.Refresh(new[] { 1, 2, 101, 102, 201 }, book, 3);

			Assert.That(wall.Pins, Is.EqualTo(new[] { 2, 102, 101 }), "금 둘 (화면 순서), 그다음 은. 못 얻은 201 은 없음, 칸 수까지만");
		}

		[Test]
		public void Edited_wall_keeps_the_players_choice_and_stops_at_full()
		{
			AchievementSaveSlice slice = new();
			slice.Reset();
			AchievementBook book = new(slice.Records);
			book.Advance(1, 1, TODAY);
			book.Advance(2, 3, TODAY);
			book.Advance(101, 2, TODAY);
			AchievementWall wall = new(slice);
			int[] ordered = { 1, 2, 101 };
			wall.Refresh(ordered, book, 2);

			Assert.That(wall.Toggle(2, 2), Is.EqualTo(AchievementWallChange.Unpinned));
			wall.Refresh(ordered, book, 2);
			Assert.That(wall.Pins, Is.EqualTo(new[] { 101 }), "내린 메달을 자동으로 다시 걸지 않음");

			Assert.That(wall.Toggle(1, 2), Is.EqualTo(AchievementWallChange.Pinned));
			Assert.That(wall.Toggle(2, 2), Is.EqualTo(AchievementWallChange.Full));
			Assert.That(wall.Pins, Is.EqualTo(new[] { 101, 1 }));
			Assert.That(slice.WallEdited, Is.True);
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
