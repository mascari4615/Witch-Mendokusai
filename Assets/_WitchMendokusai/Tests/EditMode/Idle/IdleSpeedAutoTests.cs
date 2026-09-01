using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>배속과 자동 시전 (gap-2026-08-23 P1-6)</summary>
	public sealed class IdleSpeedAutoTests
	{
		/// <summary>★ 배속이 돌아가며 바뀌고 저장을 건넌다</summary>
		[Test]
		public void Speed_CyclesAndSticks()
		{
			IdleTuning tuning = new IdleTuning();
			IdleSession session = new IdleSession(tuning);

			Assert.AreEqual(1d, session.SpeedNow, 1e-9d, "처음이 1배가 아니다");

			session.CycleSpeed();
			double second = session.SpeedNow;
			Assert.Greater(second, 1d, "한 번 돌렸는데 안 빨라졌다");

			for (int turn = 0; turn < tuning.SpeedSteps.Length - 1; turn++)
			{
				session.CycleSpeed();
			}

			Assert.AreEqual(1d, session.SpeedNow, 1e-9d, "끝에서 처음으로 안 돌아왔다");
		}

		/// <summary>★ 배속이 걸리면 같은 시간에 더 나아간다</summary>
		[Test]
		public void Speed_MakesTheRunGoFurther()
		{
			IdleTuning tuning = new IdleTuning();

			IdleSession slow = new IdleSession(tuning);
			IdleSession fast = new IdleSession(tuning);
			fast.CycleSpeed();

			for (int beat = 0; beat < 200; beat++)
			{
				slow.AdvanceLive(0.1d);
				fast.AdvanceLive(0.1d);
			}

			Assert.Greater(fast.Capture().Kills, slow.Capture().Kills, "배속인데 진행이 같다");
		}

		/// <summary>
		/// ★ <b>배속이 오프라인 보상을 안 부풀림</b>. 실측은 초당 값이라 그래야 맞음
		///
		/// 이게 깨지면 배속을 켜 두고 나가는 것이 늘 정답이 되어 배속이 방치의 배수
		/// </summary>
		[Test]
		public void Speed_DoesNotInflateTheOfflineRate()
		{
			IdleTuning tuning = new IdleTuning();

			IdleSession slow = new IdleSession(tuning);
			IdleSession fast = new IdleSession(tuning);
			fast.CycleSpeed();

			// 구역 고정. 구역이 바뀌면 측정이 처음부터 다시 세므로 60초를 못 채움
			slow.State.HoldingStage = true;
			fast.State.HoldingStage = true;

			// 같은 <시뮬 시간>을 준다. 배속 판은 실제로 덜 앉아 있는 셈
			for (int beat = 0; beat < 1200; beat++)
			{
				slow.AdvanceLive(0.1d);
			}

			for (int beat = 0; beat < 1200 / 2; beat++)
			{
				fast.AdvanceLive(0.1d);
			}

			double slowRate = slow.State.MeasuredKillsPerSecond;
			double fastRate = fast.State.MeasuredKillsPerSecond;

			Assert.Greater(slowRate, 0d, "실측이 아예 안 잡혔다");
			Assert.AreEqual(slowRate, fastRate, slowRate * 0.35d,
				"같은 시뮬 시간을 돌았는데 초당 처치가 크게 갈렸다");
		}

		/// <summary>★ 자동은 꺼져 있으면 아무 일도 안 한다</summary>
		[Test]
		public void Auto_DoesNothingWhileOff()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Cost = tuning.CostMax;

			Assert.IsFalse(IdleCards.AutoCastOne(state, tuning, out IdleCardResult _));
			Assert.AreEqual(tuning.CostMax, state.Cost, 1e-9d, "꺼져 있는데 코스트를 썼다");
		}

		/// <summary>★ 켜면 한 번에 <b>한 장만</b>. 다 쏟으면 손패가 장식이 된다</summary>
		[Test]
		public void Auto_CastsOneCardAtATime()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			IdleCards.EnsureDeck(state);
			state.AutoCast = true;
			state.Cost = tuning.CostMax;

			IdleCardKind wasFirst = IdleCards.HandAt(state, 0);
			double before = state.Cost;

			Assert.IsTrue(IdleCards.AutoCastOne(state, tuning, out IdleCardResult result));
			Assert.AreEqual(wasFirst, result.Kind, "손패 앞에서부터 안 골랐다");
			Assert.AreEqual(before - IdleCards.CostOf(wasFirst, tuning), state.Cost, 1e-9d,
				"한 장 값보다 많이 썼다");
		}

		/// <summary>★ 켜고 끄기가 저장을 건넌다</summary>
		[Test]
		public void TheToggles_SurviveTheSave()
		{
			IdleTuning tuning = new IdleTuning();
			IdleSession session = new IdleSession(tuning);
			session.CycleSpeed();
			session.ToggleAutoCast();

			IdleState back = new IdleState();
			back.Load(session.State.Save());

			Assert.AreEqual(session.State.SpeedStep, back.SpeedStep);
			Assert.IsTrue(back.AutoCast);
		}
	}
}
