using System;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-164 Phase 1 step2 — <see cref="SimulationTickScheduler"/> fixed-accumulator
	/// 회귀 잠금. RoadGraph 와 형제 — Phase 2 (에이전트·전기 전파) 가 결정적 tick 토대 위에 올라야 한다.
	///
	/// 순수 POCO — Time.timeScale ⊥ + render 프레임율 ⊥ 검증 (테스트가 dt 직접 주입).
	/// RoadGraphTest 패턴 답습 (new() 직접 + Assert.That).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class SimulationTickSchedulerTest
	{
		private const float TICK = 0.05f;
		private const float EPS = 1e-5f;

		[Test]
		public void Constructor_RejectsNonPositiveSecondsPerTick()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationTickScheduler(0f));
			Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationTickScheduler(-1f));
		}

		[Test]
		public void Constructor_RejectsZeroMaxSteps()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationTickScheduler(TICK, maxStepsPerAdvance: 0));
		}

		[Test]
		public void Advance_SubTickDt_AccumulatesNoFire()
		{
			SimulationTickScheduler scheduler = new(TICK);

			int ticks = scheduler.Advance(TICK * 0.4f);

			Assert.That(ticks, Is.Zero, "sub-tick 단발 fire X");
			Assert.That(scheduler.TickCount, Is.Zero);
			Assert.That(scheduler.Accumulator, Is.EqualTo(TICK * 0.4f).Within(EPS), "잔여분 누적");
		}

		[Test]
		public void Advance_SubTickDeltasSumToTick_FiresOnce()
		{
			SimulationTickScheduler scheduler = new(TICK);

			Assert.That(scheduler.Advance(TICK * 0.4f), Is.Zero);
			Assert.That(scheduler.Advance(TICK * 0.7f), Is.EqualTo(1), "0.4 + 0.7 = 1.1 tick → 1 fire");

			Assert.That(scheduler.TickCount, Is.EqualTo(1L));
			Assert.That(scheduler.Accumulator, Is.EqualTo(TICK * 0.1f).Within(EPS), "잔여 0.1 tick 누적");
		}

		[Test]
		public void Advance_ExactlyOneTick_FiresOnce_AccumulatorZero()
		{
			SimulationTickScheduler scheduler = new(TICK);

			int ticks = scheduler.Advance(TICK);

			Assert.That(ticks, Is.EqualTo(1));
			Assert.That(scheduler.Accumulator, Is.EqualTo(0f).Within(EPS));
		}

		[Test]
		public void Advance_LongDt_FiresMultipleTicks_RemainderAccumulates()
		{
			SimulationTickScheduler scheduler = new(TICK);

			int ticks = scheduler.Advance(TICK * 3.5f);

			Assert.That(ticks, Is.EqualTo(3));
			Assert.That(scheduler.Accumulator, Is.EqualTo(TICK * 0.5f).Within(EPS));
		}

		[Test]
		public void Advance_ExcessDt_ClampedByMaxStepsPerAdvance_AccumulatorReset()
		{
			SimulationTickScheduler scheduler = new(TICK, maxStepsPerAdvance: 2);

			// 5 tick 분량 입력 but cap=2 → spiral-of-death 차단, 잔여 drop.
			int ticks = scheduler.Advance(TICK * 5f);

			Assert.That(ticks, Is.EqualTo(2));
			Assert.That(scheduler.TickCount, Is.EqualTo(2L));
			Assert.That(scheduler.Accumulator, Is.EqualTo(0f).Within(EPS),
				"cap 초과분은 drop — 다음 Advance 가 catch-up 가속 안 함");
		}

		[Test]
		public void Advance_SpeedMultiplier2x_DoublesEffectiveDt()
		{
			SimulationTickScheduler scheduler = new(TICK);
			scheduler.SpeedMultiplier = 2f;

			int ticks = scheduler.Advance(TICK);

			Assert.That(ticks, Is.EqualTo(2));
			Assert.That(scheduler.TickCount, Is.EqualTo(2L));
		}

		[Test]
		public void Advance_SpeedMultiplierZero_PausesNoFire_AccumulatorUntouched()
		{
			SimulationTickScheduler scheduler = new(TICK);
			scheduler.Advance(TICK * 0.5f);
			float accBefore = scheduler.Accumulator;

			scheduler.SpeedMultiplier = 0f;
			int ticks = scheduler.Advance(TICK * 10f);

			Assert.That(ticks, Is.Zero);
			Assert.That(scheduler.Accumulator, Is.EqualTo(accBefore).Within(EPS),
				"pause 중 accumulator 동결 (이전 잔여 보존)");
		}

		[Test]
		public void Advance_SpeedMultiplierNegative_TreatedAsPause()
		{
			SimulationTickScheduler scheduler = new(TICK);
			scheduler.SpeedMultiplier = -1f;

			int ticks = scheduler.Advance(TICK * 10f);

			Assert.That(ticks, Is.Zero, "음수 배속 = rewind X, pause 동등");
			Assert.That(scheduler.Accumulator, Is.EqualTo(0f).Within(EPS));
		}

		[Test]
		public void Advance_NegativeOrZeroDt_NoEffect()
		{
			SimulationTickScheduler scheduler = new(TICK);

			Assert.That(scheduler.Advance(0f), Is.Zero);
			Assert.That(scheduler.Advance(-1f), Is.Zero);
			Assert.That(scheduler.Accumulator, Is.EqualTo(0f).Within(EPS));
			Assert.That(scheduler.TickCount, Is.Zero);
		}

		[Test]
		public void Reset_RestoresSnapshotState_PreservesConfig()
		{
			SimulationTickScheduler scheduler = new(TICK, maxStepsPerAdvance: 4);
			scheduler.Advance(TICK * 7f);

			scheduler.Reset(tickCount: 100L, accumulator: TICK * 0.3f);

			Assert.That(scheduler.TickCount, Is.EqualTo(100L));
			Assert.That(scheduler.Accumulator, Is.EqualTo(TICK * 0.3f).Within(EPS));
			Assert.That(scheduler.SecondsPerTick, Is.EqualTo(TICK).Within(EPS), "config 보존");
			Assert.That(scheduler.MaxStepsPerAdvance, Is.EqualTo(4), "config 보존");
		}

		[Test]
		public void Reset_NegativeArgs_Throw()
		{
			SimulationTickScheduler scheduler = new(TICK);

			Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Reset(tickCount: -1L));
			Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Reset(accumulator: -0.1f));
		}

		// ★ 결정성 핵심: 같은 누적 dt → 같은 총 tick 수 + 같은 accumulator 종착, 프레임 슬라이싱과 무관.
		[Test]
		public void Advance_DeterministicAcrossFrameSlicing_SameTotal()
		{
			SimulationTickScheduler one = new(TICK);
			int oneTicks = one.Advance(TICK * 3.7f);

			SimulationTickScheduler many = new(TICK);
			int manyTicks = 0;
			for (int i = 0; i < 10; i++)
			{
				manyTicks += many.Advance(TICK * 0.37f);
			}

			Assert.That(oneTicks, Is.EqualTo(manyTicks), "프레임 슬라이싱과 무관하게 동일 tick 수");
			Assert.That(many.Accumulator, Is.EqualTo(one.Accumulator).Within(EPS * 10f),
				"잔여 누적 동일 (float 오차 허용)");
		}
	}
}
