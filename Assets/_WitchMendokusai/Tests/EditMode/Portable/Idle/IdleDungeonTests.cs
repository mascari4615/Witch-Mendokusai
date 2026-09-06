using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>던전 입장권 (economy.md 4). 하루 몇 번 이라는 울타리가 진짜 도는가</summary>
	public sealed class IdleDungeonTests
	{
		private const long DAY = 86400L;

		/// <summary>기준으로 삼을 시각. 경계(offset)를 막 지난 자리</summary>
		private static long JustAfterBoundary(IdleTuning tuning)
		{
			return 100L * DAY + tuning.DayResetOffsetSeconds + 60L;
		}

		/// <summary>★ 첫 판도 채운다. 안 그러면 시작하자마자 하루를 기다려야 한다</summary>
		[Test]
		public void AFreshGame_StartsWithTickets()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			IdleDungeons.Refill(state, tuning, JustAfterBoundary(tuning));

			Assert.AreEqual(tuning.TicketsPerDay, IdleDungeons.TicketsOf(state, IdleDungeonKind.Gold));
			Assert.AreEqual(tuning.TicketsPerDay, IdleDungeons.TicketsOf(state, IdleDungeonKind.Skill));
		}

		/// <summary>★ 쓰면 준다. 다 쓰면 못 들어간다</summary>
		[Test]
		public void SpendingATicket_TakesOne_AndStopsAtZero()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			IdleDungeons.Refill(state, tuning, JustAfterBoundary(tuning));

			for (long spent = 0; spent < tuning.TicketsPerDay; spent++)
			{
				Assert.IsTrue(IdleDungeons.TrySpend(state, IdleDungeonKind.Boss), "남았는데 못 들어갔다");
			}

			Assert.AreEqual(0L, IdleDungeons.TicketsOf(state, IdleDungeonKind.Boss));
			Assert.IsFalse(IdleDungeons.TrySpend(state, IdleDungeonKind.Boss), "없는데 들어갔다");
			Assert.AreEqual(tuning.TicketsPerDay, IdleDungeons.TicketsOf(state, IdleDungeonKind.Gear),
				"한 던전을 썼는데 다른 던전 것이 줄었다");
		}

		/// <summary>★ 같은 날 안에서는 안 찬다. 안 그러면 껐다 켜기가 곧 무한 입장</summary>
		[Test]
		public void WithinTheSameDay_NothingRefills()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			long start = JustAfterBoundary(tuning);

			IdleDungeons.Refill(state, tuning, start);
			Assert.IsTrue(IdleDungeons.TrySpend(state, IdleDungeonKind.Gold));

			IdleDungeons.Refill(state, tuning, start + 23L * 3600L);

			Assert.AreEqual(tuning.TicketsPerDay - 1L, IdleDungeons.TicketsOf(state, IdleDungeonKind.Gold),
				"같은 날인데 입장권이 다시 찼다");
		}

		/// <summary>★ 날이 바뀌면 상한까지. 며칠을 비워도 상한이 끝이라 몰아 쓰기가 안 된다</summary>
		[Test]
		public void ANewDay_RefillsToTheCap_NoMatterHowLongAway()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			long start = JustAfterBoundary(tuning);

			IdleDungeons.Refill(state, tuning, start);
			IdleDungeons.TrySpend(state, IdleDungeonKind.Gold);
			IdleDungeons.TrySpend(state, IdleDungeonKind.Gold);

			IdleDungeons.Refill(state, tuning, start + 30L * DAY);

			Assert.AreEqual(tuning.TicketsPerDay, IdleDungeons.TicketsOf(state, IdleDungeonKind.Gold),
				"서른 날을 비웠는데 상한을 안 채웠거나 넘겼다");
		}

		/// <summary>★ 경계는 offset 자리에 있다. 자정이 아니다</summary>
		[Test]
		public void TheBoundary_SitsAtTheOffset_NotMidnight()
		{
			IdleTuning tuning = new IdleTuning();
			long boundary = 100L * DAY + tuning.DayResetOffsetSeconds;

			Assert.AreEqual(IdleDungeons.DayIndexOf(boundary - 1L, tuning.DayResetOffsetSeconds) + 1L,
				IdleDungeons.DayIndexOf(boundary, tuning.DayResetOffsetSeconds),
				"경계 앞뒤가 같은 날로 잡혔다");

			Assert.AreEqual(IdleDungeons.DayIndexOf(boundary, tuning.DayResetOffsetSeconds),
				IdleDungeons.DayIndexOf(boundary + DAY - 1L, tuning.DayResetOffsetSeconds),
				"경계 뒤 하루가 같은 날이 아니다");
		}

		/// <summary>★ 다음 채워질 때까지가 화면에 실린다. 음수나 하루 넘김이 나오면 안 된다</summary>
		[Test]
		public void TheCountdown_StaysWithinADay()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			long start = JustAfterBoundary(tuning);
			IdleDungeons.Refill(state, tuning, start);

			double left = IdleDungeons.SecondsUntilRefill(state, tuning, start);

			Assert.Greater(left, 0d);
			Assert.LessOrEqual(left, DAY);
			Assert.AreEqual(DAY - 60L, left, 1e-9d, "경계를 60초 지났으면 남은 것도 그만큼 짧아야 한다");
		}

		/// <summary>
		/// ★ 사진의 카운트다운이 <b>실시각</b>을 따라감.
		///
		/// 전에는 저장 직전에만 찍히는 <c>LastSeenUnixSeconds</c> 로 재서 늘 0 시간
		/// (실측 2026-09-01)
		/// </summary>
		[Test]
		public void TheSnapshotCountdown_FollowsTheRealClock()
		{
			IdleTuning tuning = new IdleTuning();
			IdleSession session = new IdleSession(tuning);
			long start = JustAfterBoundary(tuning);
			session.CatchUp(start);

			double first = session.Capture().TicketRefillSeconds;
			Assert.Greater(first, 0d, "카운트다운이 0 으로 떴다");

			for (int beat = 0; beat < 600; beat++)
			{
				session.AdvanceLive(0.1d);
			}

			double later = session.Capture().TicketRefillSeconds;

			Assert.AreEqual(first - 60d, later, 1e-6d, "60초를 흘렸는데 카운트다운이 그만큼 안 줄었다");
		}

		/// <summary>
		/// ★ <b>배속이 날을 앞당기지 않음</b>. 안 그러면 3배속으로 켜 두는 것이 입장권 3배
		/// </summary>
		[Test]
		public void Speed_DoesNotBringTheNextDayCloser()
		{
			IdleTuning tuning = new IdleTuning();
			long start = JustAfterBoundary(tuning);

			IdleSession slow = new IdleSession(tuning);
			IdleSession fast = new IdleSession(tuning);
			slow.CatchUp(start);
			fast.CatchUp(start);
			fast.CycleSpeed();

			for (int beat = 0; beat < 600; beat++)
			{
				slow.AdvanceLive(0.1d);
				fast.AdvanceLive(0.1d);
			}

			Assert.AreEqual(slow.Capture().TicketRefillSeconds, fast.Capture().TicketRefillSeconds, 1e-6d,
				"배속 판의 다음 날이 더 가까워졌다");
		}

		/// <summary>★ 저장을 건넌다. 안 그러면 껐다 켜서 다시 채우는 길이 생긴다</summary>
		[Test]
		public void Tickets_SurviveTheSave()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			IdleDungeons.Refill(state, tuning, JustAfterBoundary(tuning));
			IdleDungeons.TrySpend(state, IdleDungeonKind.Skill);

			IdleState back = new IdleState();
			back.Load(state.Save());

			Assert.AreEqual(tuning.TicketsPerDay - 1L, IdleDungeons.TicketsOf(back, IdleDungeonKind.Skill));
			Assert.AreEqual(state.TicketDay, back.TicketDay, "채운 날이 저장을 못 건넜다");
		}

		/// <summary>★ 옛 저장에는 칸이 없다. 그래도 안 터지고 다음 경계에 채워진다</summary>
		[Test]
		public void AnOldSave_WithoutTickets_StillWorks()
		{
			IdleTuning tuning = new IdleTuning();
			IdleSaveData old = new IdleState().Save();
			old.Tickets = null;
			old.TicketDay = 0L;

			IdleState state = new IdleState();
			state.Load(old);

			Assert.AreEqual(IdleDungeons.COUNT, state.Tickets.Length);

			IdleDungeons.Refill(state, tuning, JustAfterBoundary(tuning));

			Assert.AreEqual(tuning.TicketsPerDay, IdleDungeons.TicketsOf(state, IdleDungeonKind.Gold));
		}
	}
}
