using NUnit.Framework;
using WitchMendokusai;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 하늘의 정본은 <b>벽시계</b>다 — 저장된 달력이 앞서 있어도 맞춰진다 (TASK-WM-315).
	///
	/// ★ prod 실측 2026-08-13: 세계 둘을 나란히 띄웠더니 east 125일 · west 91일이었다.
	///   앞으로만 가는 셈(<see cref="WorldCalendar.SetTotalMinutes"/>) 때문에, 옛 저장값이 앞선 세계는
	///   벽시계를 영영 못 따라잡는다 — <b>34일 어긋난 두 하늘</b>. 국경을 넘으면 밤이 낮이 된다.
	/// </summary>
	public sealed class SkyAgreesTests
	{
		[Test]
		public void 앞선_하늘은_되감아서라도_맞춘다()
		{
			WorldCalendar ahead = new WorldCalendar(24, 28, 4);
			ahead.SetTotalMinutes(125 * 24 * 60);

			WorldCalendar wall = new WorldCalendar(24, 28, 4);
			wall.SetTotalMinutes(91 * 24 * 60);

			// 앞으로만 가는 셈으로는 못 맞춘다 — 이것이 prod 에서 굳어 버린 자리다.
			Assert.That(ahead.SetTotalMinutes(wall.TotalMinutes()), Is.False);
			Assert.That(ahead.TotalDays(), Is.EqualTo(125));

			ahead.SetTotalMinutesHard(wall.TotalMinutes());
			Assert.That(ahead.TotalDays(), Is.EqualTo(wall.TotalDays()));
			Assert.That(ahead.TotalMinutes(), Is.EqualTo(wall.TotalMinutes()));
		}

		[Test]
		public void 맞춘_하늘은_시각까지_같다()
		{
			WorldCalendar one = new WorldCalendar(24, 28, 4);
			WorldCalendar other = new WorldCalendar(24, 28, 4);

			one.SetTotalMinutesHard(91 * 24 * 60 + 17 * 60 + 22);
			other.SetTotalMinutesHard(91 * 24 * 60 + 17 * 60 + 22);

			Assert.That(one.Day, Is.EqualTo(other.Day));
			Assert.That(one.Hour, Is.EqualTo(other.Hour));
			Assert.That(one.Minute, Is.EqualTo(other.Minute));
			Assert.That(one.Season, Is.EqualTo(other.Season));
			Assert.That(one.Year, Is.EqualTo(other.Year));
		}

		[Test]
		public void 뒤처진_하늘은_그냥_앞으로_간다()
		{
			// 되감기는 <b>앞섰을 때만</b>이다 — 평소에는 예전 그대로 앞으로만 흐른다.
			WorldCalendar behind = new WorldCalendar(24, 28, 4);
			behind.SetTotalMinutes(50 * 24 * 60);

			Assert.That(behind.SetTotalMinutes(91 * 24 * 60), Is.True);
			Assert.That(behind.TotalDays(), Is.EqualTo(91));
		}

		[Test]
		public void 음수는_0_으로_접는다()
		{
			WorldCalendar one = new WorldCalendar(24, 28, 4);
			one.SetTotalMinutesHard(-5);

			Assert.That(one.TotalMinutes(), Is.EqualTo(0));
		}
	}
}
