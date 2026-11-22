using System;
using System.Linq;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class DollMasteryShardScheduleTests
	{
		[Test]
		public void 알리사_값이면_13개씩_번갈아_20초마다()
		{
			DollMasteryShardSchedule schedule = new DollMasteryShardSchedule(new[] { 13, 13 }, 10f, 20f);

			Assert.AreEqual(26, schedule.Count);
			Assert.AreEqual(13, Enumerable.Range(0, schedule.Count).Count(index => schedule.TrackAt(index) == 0));
			for (int i = 1; i < schedule.Count; i++)
				Assert.AreNotEqual(schedule.TrackAt(i - 1), schedule.TrackAt(i), $"{i} 번째가 앞과 같은 막대");
			Assert.AreEqual(10f, schedule.TimeAt(0));
			Assert.AreEqual(510f, schedule.TimeAt(25), "판 10분 안에 다 나옴");
		}

		[Test]
		public void 한쪽이_다_나오면_남은_쪽만()
		{
			DollMasteryShardSchedule schedule = new DollMasteryShardSchedule(new[] { 1, 3 }, 0f, 5f);

			CollectionAssert.AreEqual(new[] { 0, 1, 1, 1 }, Enumerable.Range(0, schedule.Count).Select(schedule.TrackAt).ToArray());
		}

		[Test]
		public void 경과_초까지_놓였어야_할_수()
		{
			DollMasteryShardSchedule schedule = new DollMasteryShardSchedule(new[] { 2, 2 }, 10f, 20f);

			Assert.AreEqual(0, schedule.DueCount(9.9f));
			Assert.AreEqual(1, schedule.DueCount(10f));
			Assert.AreEqual(1, schedule.DueCount(29.9f));
			Assert.AreEqual(2, schedule.DueCount(30f));
			Assert.AreEqual(4, schedule.DueCount(999f), "전체 수를 넘지 않음");
		}

		[Test]
		public void 잘못된_값은_거부한다()
		{
			Assert.Throws<ArgumentException>(() => new DollMasteryShardSchedule(new int[0], 0f, 1f));
			Assert.Throws<ArgumentException>(() => new DollMasteryShardSchedule(new[] { 1 }, 0f, 0f));
			Assert.Throws<ArgumentException>(() => new DollMasteryShardSchedule(new[] { -1 }, 0f, 1f));
		}
	}
}
