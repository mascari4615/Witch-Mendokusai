using NUnit.Framework;
using WitchMendokusai;
using WitchMendokusai.DomainSDK.Workshop;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-170 — 「몇 시가 낮이고 몇 시가 밤인가」 규칙만 따로 지킨다.
	/// 씬도 시계도 없이 판정되도록 순수 정적으로 뽑아 뒀기 때문에 여기서 볼 수 있다.
	///
	/// ★ 이 시험이 있는 진짜 이유: 이 층(공방)은 어제까지 <b>게임 어디서도 안 불리는 코드</b>였고,
	///   그래서 통째로 지워졌는데도 컴파일이 안 깨져 아무도 몰랐다. 배선과 시험을 같이 박아 둔다.
	/// </summary>
	public class WorkshopDirectorPhaseTests
	{
		private const int DAY_START = 6;
		private const int NIGHT_START = 18;

		[Test]
		public void 낮_구간은_낮으로_판정된다()
		{
			Assert.AreEqual(DayNightPhase.Day, WorkshopDirector.PhaseAtHour(DAY_START, DAY_START, NIGHT_START));
			Assert.AreEqual(DayNightPhase.Day, WorkshopDirector.PhaseAtHour(12, DAY_START, NIGHT_START));
			Assert.AreEqual(DayNightPhase.Day, WorkshopDirector.PhaseAtHour(NIGHT_START - 1, DAY_START, NIGHT_START));
		}

		[Test]
		public void 밤_구간은_밤으로_판정된다()
		{
			Assert.AreEqual(DayNightPhase.Night, WorkshopDirector.PhaseAtHour(NIGHT_START, DAY_START, NIGHT_START));
			Assert.AreEqual(DayNightPhase.Night, WorkshopDirector.PhaseAtHour(23, DAY_START, NIGHT_START));
			Assert.AreEqual(DayNightPhase.Night, WorkshopDirector.PhaseAtHour(0, DAY_START, NIGHT_START));
			Assert.AreEqual(DayNightPhase.Night, WorkshopDirector.PhaseAtHour(DAY_START - 1, DAY_START, NIGHT_START));
		}

		[Test]
		public void 낮이_자정을_넘는_설정도_안_깨진다()
		{
			// 20시부터 낮, 4시부터 밤 — 낮 구간이 자정을 가로지른다.
			Assert.AreEqual(DayNightPhase.Day, WorkshopDirector.PhaseAtHour(22, 20, 4));
			Assert.AreEqual(DayNightPhase.Day, WorkshopDirector.PhaseAtHour(0, 20, 4));
			Assert.AreEqual(DayNightPhase.Day, WorkshopDirector.PhaseAtHour(3, 20, 4));
			Assert.AreEqual(DayNightPhase.Night, WorkshopDirector.PhaseAtHour(4, 20, 4));
			Assert.AreEqual(DayNightPhase.Night, WorkshopDirector.PhaseAtHour(19, 20, 4));
		}

		[Test]
		public void 두_시각이_같으면_낮으로_고정된다()
		{
			// 교대 시각을 같게 두면 「밤이 0시간」이다 — 나눗셈 없는 자리라 예외가 아니라 낮 고정으로 둔다.
			Assert.AreEqual(DayNightPhase.Day, WorkshopDirector.PhaseAtHour(0, 8, 8));
			Assert.AreEqual(DayNightPhase.Day, WorkshopDirector.PhaseAtHour(20, 8, 8));
		}

		[Test]
		public void 한_바퀴_돌면_하루가_하나_는다()
		{
			DayNightCycle cycle = new DayNightCycle(DayNightPhase.Day, 0);

			cycle.Advance(); // 낮 → 밤
			Assert.AreEqual(DayNightPhase.Night, cycle.Phase);
			Assert.AreEqual(0, cycle.DayIndex);

			cycle.Advance(); // 밤 → 낮 = 한 바퀴 닫힘
			Assert.AreEqual(DayNightPhase.Day, cycle.Phase);
			Assert.AreEqual(1, cycle.DayIndex);
		}
	}
}
