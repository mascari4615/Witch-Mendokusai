using NUnit.Framework;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 세계의 시간이 흐르는 규칙 (TASK-WM-217) — 서버가 굴릴 수 있게 엔진 밖으로 내린 그것.
	/// </summary>
	public sealed class WorldCalendarTests
	{
		private static WorldCalendar Fresh() => new WorldCalendar(24, 28, 4, 6, 0);

		[Test]
		public void 분이_쌓이면_시가_넘어간다()
		{
			WorldCalendar calendar = Fresh();

			calendar.AdvanceMinutes(70f);

			Assert.That(calendar.Hour, Is.EqualTo(7));
			Assert.That(calendar.Minute, Is.EqualTo(10));
		}

		[Test]
		public void 소수점은_버리지_않고_모인다()
		{
			WorldCalendar calendar = Fresh();

			// 0.5분씩 스무 번 = 10분. 버리면 영영 0분이다(20Hz 서버가 정확히 이 모양이다).
			for (int i = 0; i < 20; i++)
				calendar.AdvanceMinutes(0.5f);

			Assert.That(calendar.Minute, Is.EqualTo(10));
		}

		[Test]
		public void 자정을_넘으면_알려준다()
		{
			WorldCalendar calendar = Fresh();

			Assert.That(calendar.AdvanceMinutes(60f), Is.False);
			Assert.That(calendar.AdvanceMinutes(18f * 60f), Is.True);
			Assert.That(calendar.Day, Is.EqualTo(2));
			Assert.That(calendar.Hour, Is.EqualTo(1));
		}

		[Test]
		public void 계절과_해가_넘어간다()
		{
			WorldCalendar calendar = new WorldCalendar(24, 28, 4, 0, 0);

			// 4계절 * 28일 = 112일 = 한 해
			calendar.AdvanceMinutes(112f * 24f * 60f);

			Assert.That(calendar.Year, Is.EqualTo(2));
			Assert.That(calendar.Season, Is.EqualTo(0));
			Assert.That(calendar.Day, Is.EqualTo(1));
		}

		[Test]
		public void 망가진_시각은_접어_넣는다()
		{
			WorldCalendar calendar = Fresh();

			calendar.Set(year: 0, season: 9, day: 999, hour: 99, minute: -5);

			Assert.That(calendar.Year, Is.EqualTo(1));
			Assert.That(calendar.Season, Is.EqualTo(1));
			Assert.That(calendar.Hour, Is.EqualTo(3));
			Assert.That(calendar.Minute, Is.EqualTo(55));
			Assert.That(calendar.Day, Is.InRange(1, 28));
		}

		[Test]
		public void 세계를_껐다_켜도_시각이_이어진다()
		{
			WorldSim world = new WorldSim();
			world.AdvanceMinutes(3f * 24f * 60f + 90f);

			WorldSaveData saved = world.Save();
			WorldSim reborn = new WorldSim();
			reborn.Load(saved);

			Assert.That(reborn.Calendar.Day, Is.EqualTo(world.Calendar.Day));
			Assert.That(reborn.Calendar.Hour, Is.EqualTo(world.Calendar.Hour));
			Assert.That(reborn.Calendar.Minute, Is.EqualTo(world.Calendar.Minute));
		}

		[Test]
		public void 아무도_없어도_시간은_흐른다()
		{
			WorldSim world = new WorldSim();

			int before = world.Calendar.TotalDays();
			world.AdvanceMinutes(24f * 60f);

			// 접속자 0명 — MMO 는 「내가 없어도 밤이 온다」가 핵심이다.
			Assert.That(world.Snapshot().Length, Is.EqualTo(0));
			Assert.That(world.Calendar.TotalDays(), Is.EqualTo(before + 1));
		}

		[Test]
		public void 시각을_그_값으로_세운다()
		{
			// 세계가 여럿이면 저마다 흘리는 게 아니라 <b>같은 셈</b>으로 맞춰야 한다 (TASK-WM-266).
			WorldCalendar sky = Fresh();
			int began = sky.TotalMinutes();

			Assert.IsFalse(sky.SetTotalMinutes(began + 30), "하루가 안 넘었으면 false");
			Assert.AreEqual(began + 30, sky.TotalMinutes());
		}

		[Test]
		public void 세운_시각이_하루를_넘으면_알려_준다()
		{
			WorldCalendar sky = Fresh();
			Assert.IsTrue(sky.SetTotalMinutes(sky.TotalMinutes() + (24 * 60)),
				"하루가 바뀌는 순간에 걸리는 일들이 있다");
		}

		[Test]
		public void 시각은_거꾸로_안_간다()
		{
			// 벽시계가 잠깐 뒤로 가도(시간 맞춤·서머타임) 세계의 어제로 돌아가면 안 된다.
			WorldCalendar sky = Fresh();
			sky.SetTotalMinutes(sky.TotalMinutes() + 100);
			int now = sky.TotalMinutes();

			Assert.IsFalse(sky.SetTotalMinutes(now - 50));
			Assert.AreEqual(now, sky.TotalMinutes());
		}

		[Test]
		public void 아주_멀리_한_번에_가도_자릿수가_맞는다()
		{
			// 며칠 꺼져 있다 켜지는 일은 흔하다 — 그때 한 번에 건너뛴다.
			WorldCalendar sky = Fresh();
			sky.SetTotalMinutes(sky.TotalMinutes() + (28 * 4 * 24 * 60) + 90);

			Assert.AreEqual(2, sky.Year, "한 해가 지났어야 한다");
			Assert.AreEqual(7, sky.Hour, "6시에서 90분 뒤 = 7시 30분");
			Assert.AreEqual(30, sky.Minute);
		}
	}
}
