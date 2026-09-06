using NUnit.Framework;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-168 INC-5c — <see cref="TimeOfDayMap"/> 시각→시간대 구간 회귀 잠금. 순수 — PlayMode 무관.
	/// </summary>
	public sealed class TimeOfDayMapTest
	{
		[Test]
		public void FromHour_MapsEachWindow()
		{
			Assert.That(TimeOfDayMap.FromHour(7), Is.EqualTo(TimeOfDay.Morning), "07시 = 아침");
			Assert.That(TimeOfDayMap.FromHour(14), Is.EqualTo(TimeOfDay.Afternoon), "14시 = 낮");
			Assert.That(TimeOfDayMap.FromHour(19), Is.EqualTo(TimeOfDay.Evening), "19시 = 저녁");
			Assert.That(TimeOfDayMap.FromHour(23), Is.EqualTo(TimeOfDay.Night), "23시 = 밤");
			Assert.That(TimeOfDayMap.FromHour(3), Is.EqualTo(TimeOfDay.Night), "03시(새벽) = 밤");
		}

		[Test]
		public void FromHour_BoundariesAreInclusiveLowerExclusiveUpper()
		{
			Assert.That(TimeOfDayMap.FromHour(5), Is.EqualTo(TimeOfDay.Morning), "05시 = 아침 시작");
			Assert.That(TimeOfDayMap.FromHour(11), Is.EqualTo(TimeOfDay.Afternoon), "11시 = 낮 시작");
			Assert.That(TimeOfDayMap.FromHour(17), Is.EqualTo(TimeOfDay.Evening), "17시 = 저녁 시작");
			Assert.That(TimeOfDayMap.FromHour(22), Is.EqualTo(TimeOfDay.Night), "22시 = 밤 시작");
			Assert.That(TimeOfDayMap.FromHour(4), Is.EqualTo(TimeOfDay.Night), "04시 = 아직 밤");
		}

		[Test]
		public void FromHour_NormalizesOutOfRange()
		{
			Assert.That(TimeOfDayMap.FromHour(24), Is.EqualTo(TimeOfDayMap.FromHour(0)), "24시 = 0시");
			Assert.That(TimeOfDayMap.FromHour(31), Is.EqualTo(TimeOfDay.Morning), "31시 = 07시 = 아침");
			Assert.That(TimeOfDayMap.FromHour(-1), Is.EqualTo(TimeOfDay.Night), "-1시 = 23시 = 밤");
		}
	}
}
