using NUnit.Framework;
using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai.Tests
{
	/// <summary>몬스터 기록의 세이브 조각 왕복 (2026-11-25). 장부 규칙은 Portable/MonsterLogBookTest</summary>
	public sealed class MonsterLogTest
	{
		private static readonly int[] TARGETS = { 1, 3, 5, 7, 9 };
		private const string TODAY = "2026-11-25";
		private const int SLIME = 2;
		private const int FOREST = 0;

		[Test]
		public void Save_slice_round_trips_and_resets()
		{
			MonsterLogSaveSlice saved = new();
			MonsterLogBook book = new(saved.Records);
			book.AddKill(SLIME, true, FOREST, TODAY, TARGETS);
			book.AddDefeat(SLIME);
			book.MarkSeen(SLIME, TARGETS);

			MonsterLogSaveSlice loaded = new();
			loaded.Restore(saved.Capture());

			MonsterLogRecord record = loaded.Records[SLIME];
			Assert.That(record.Kills, Is.EqualTo(1));
			Assert.That(record.EliteKills, Is.EqualTo(1));
			Assert.That(record.Defeats, Is.EqualTo(1));
			Assert.That(record.FirstKilledOn, Is.EqualTo(TODAY));
			Assert.That(record.FirstDungeonId, Is.EqualTo(FOREST));
			Assert.That(record.SeenStage, Is.EqualTo(1));

			loaded.Reset();
			Assert.That(loaded.Records, Is.Empty);
		}
	}
}
