using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai.Tests
{
	/// <summary>몬스터 기록 (2026-11-25). 종별로 세기, 단계와 새 기록 표시, 쓰러짐만 있어도 만난 것, 시간대 합치기</summary>
	public sealed class MonsterLogBookTest
	{
		private static readonly int[] TARGETS = { 1, 3, 5, 7, 9 };
		private const string TODAY = "2026-11-25";
		private const int SLIME = 2;
		private const int FOREST = 0;

		private static MonsterLogBook NewBook() => new(new Dictionary<int, MonsterLogRecord>());

		[Test]
		public void Kills_are_counted_per_species_and_the_first_one_dates_the_record()
		{
			MonsterLogBook book = NewBook();

			book.AddKill(SLIME, false, FOREST, TODAY, TARGETS);
			book.AddKill(SLIME, true, 7, "2026-12-01", TARGETS);
			book.AddKill(99, false, FOREST, TODAY, TARGETS);

			MonsterLogRecord record = book.Find(SLIME);
			Assert.That(record.Kills, Is.EqualTo(2));
			Assert.That(record.EliteKills, Is.EqualTo(1));
			Assert.That(record.FirstKilledOn, Is.EqualTo(TODAY), "처음 잡은 날은 안 바뀜");
			Assert.That(record.FirstDungeonId, Is.EqualTo(FOREST));
			Assert.That(book.KillsOf(99), Is.EqualTo(1));
			Assert.That(book.KillsOf(12345), Is.EqualTo(0));
		}

		[Test]
		public void A_kill_reports_the_stage_before_and_after()
		{
			MonsterLogBook book = NewBook();

			(int before, int after) first = book.AddKill(SLIME, false, FOREST, TODAY, TARGETS);
			(int before, int after) second = book.AddKill(SLIME, false, FOREST, null, TARGETS);
			(int before, int after) third = book.AddKill(SLIME, false, FOREST, null, TARGETS);

			Assert.That(first, Is.EqualTo((0, 1)));
			Assert.That(second, Is.EqualTo((1, 1)));
			Assert.That(third, Is.EqualTo((1, 2)));
		}

		[Test]
		public void News_goes_away_when_seen_and_comes_back_on_the_next_stage()
		{
			MonsterLogBook book = NewBook();
			book.AddKill(SLIME, false, FOREST, TODAY, TARGETS);
			Assert.That(book.HasNews(SLIME, TARGETS), Is.True);

			book.MarkSeen(SLIME, TARGETS);
			Assert.That(book.HasNews(SLIME, TARGETS), Is.False);

			book.AddKill(SLIME, false, FOREST, null, TARGETS);
			Assert.That(book.HasNews(SLIME, TARGETS), Is.False, "단계가 안 올랐으면 그대로");
			book.AddKill(SLIME, false, FOREST, null, TARGETS);
			Assert.That(book.HasNews(SLIME, TARGETS), Is.True);
		}

		[Test]
		public void Being_defeated_counts_as_meeting_without_opening_a_slot()
		{
			MonsterLogBook book = NewBook();
			Assert.That(book.IsMet(SLIME), Is.False);

			book.AddDefeat(SLIME);

			Assert.That(book.IsMet(SLIME), Is.True);
			Assert.That(book.StageOf(SLIME, TARGETS), Is.EqualTo(0));
			Assert.That(book.HasNews(SLIME, TARGETS), Is.False);
		}

		[Test]
		public void Touching_time_spans_are_merged_and_gaps_are_kept()
		{
			List<(float Start, float End)> merged = DiscoverySpans.Merge(new List<(float Start, float End)>
			{
				(120f, 180f),
				(0f, 60f),
				(60f, 120f),
				(300f, 360f),
				(310f, 320f),
			});

			Assert.That(merged, Is.EqualTo(new List<(float Start, float End)> { (0f, 180f), (300f, 360f) }));
		}
	}
}
