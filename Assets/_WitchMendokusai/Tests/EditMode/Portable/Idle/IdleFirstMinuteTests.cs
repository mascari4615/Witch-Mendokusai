using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// <b>첫 1분</b> — 이 게임의 승부처 (TASK-WM-406).
	///
	/// ★ 기존 시험은 「30분 동안 몇 개 샀나」를 봤다. 그건 <b>지루하지 않은가</b>이지
	///   <b>처음 켠 사람이 할 일이 있는가</b>가 아니다.
	///   방치형은 첫 화면에서 「이거 뭐 하는 게임이지」로 꺼지면 끝이다.
	///
	/// ★ 여기서 지키는 것 셋:
	///   ① 켜자마자 <b>화면이 할 일을 말해 준다</b> (모른 채 서 있게 두지 않는다)
	///   ② 손으로 두드리면 <b>실제로 앞선다</b> (첫 1분의 주인공은 손이다)
	///   ③ 몇 분 안에 <b>처음 살 것</b>이 생긴다 (기다림이 끝없지 않다)
	/// </summary>
	public sealed class IdleFirstMinuteTests
	{
		/// <summary>★ 켠 순간 화면이 <b>무엇을 하라고</b> 말한다 — 침묵은 「고장」으로 읽힌다.</summary>
		[Test]
		public void AtSecondZero_TheScreenHasSomethingToSay()
		{
			IdleSession session = New(out IdleTuning _);
			IdleAdviceResult advice = IdleAdvice.NextStep(session.Capture());

			// 무엇이 나오든 상관없지만, 「기다려라」면 <b>얼마나</b>인지는 반드시 있어야 한다.
			if (advice.Step == IdleStep.Wait)
			{
				Assert.Greater(advice.Amount, 0d, "기다리라면서 얼마나인지 안 말한다");
				Assert.IsFalse(double.IsInfinity(advice.Amount), "영영 기다리라고 한다");
				return;
			}

			Assert.AreNotEqual(IdleStep.BagFull, advice.Step, "첫 판부터 가방이 찼다고 한다");
		}

		/// <summary>
		/// ★ <b>손이 첫 1분의 주인공</b> — 두드린 판이 가만둔 판보다 확실히 앞선다.
		///
		/// 초당 세 번은 사람이 실제로 하는 속도다. 이만큼 두드려도 차이가 없으면
		/// 「눌러도 그만」이 되고, 그 순간 첫 1분은 구경이 된다.
		/// </summary>
		[Test]
		public void TappingWinsTheFirstMinute()
		{
			IdleSession idle = New(out IdleTuning tuning);
			IdleSession tapping = New(out IdleTuning _);

			const double TICK = 1d / 3d;

			for (int beat = 0; beat < 180; beat++)
			{
				idle.Advance(TICK);

				tapping.Advance(TICK);
				tapping.Send(new IdleTapIntent());
			}

			TestContext.WriteLine("[첫1분] 가만둠 " + idle.State.Kills + "마리 · 두드림 "
				+ tapping.State.Kills + "마리 (초당 3번)");

			Assert.Greater(tapping.State.Kills, idle.State.Kills,
				"1분을 두드렸는데 아무 차이가 없다 — 첫 1분이 구경이 된다");
		}

		/// <summary>
		/// ★ <b>처음 살 것</b>이 몇 분 안에 생긴다 — 첫 구매까지 끝없이 기다리면 사람은 안 돌아온다.
		/// </summary>
		[Test]
		public void SomethingBecomesAffordable_WithinFiveMinutes()
		{
			IdleSession session = New(out IdleTuning tuning);

			for (int second = 0; second < 300; second++)
			{
				session.Advance(1d);

				IdleSnapshot now = session.Capture();
				if (IdleAdvice.CheapestAffordableProducer(now) >= 0
					|| now.Damage.CanAfford
					|| now.AttackSpeed.CanAfford)
				{
					TestContext.WriteLine("[첫1분] " + second + "초에 처음 살 것이 생겼다");
					Assert.Pass();
					return;
				}
			}

			Assert.Fail("5분을 기다려도 살 수 있는 것이 하나도 없다");
		}

		/// <summary>★ 첫 판은 <b>시작 인형 하나</b>뿐 (C10). 그래도 판정 성립. 곱하는 자리가 0 이나 NaN 아님</summary>
		[Test]
		public void WithNoHeroes_TheNumbersStayReal()
		{
			IdleSession session = New(out IdleTuning tuning);
			IdleSnapshot now = session.Capture();

			Assert.AreEqual(1, now.Heroes.Length, "첫 판은 시작 인형 하나여야 한다");
			Assert.Greater(IdleModel.DamageOf(session.State, tuning), 0d, "때리는 힘이 0 이다");
			Assert.Greater(IdleModel.AttackSpeedOf(session.State, tuning), 0d, "때리는 속도가 0 이다");
			Assert.IsFalse(double.IsNaN(now.IncomePerSecond), "수입이 NaN 이다");
		}

		private static IdleSession New(out IdleTuning tuning)
		{
			tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			return new IdleSession(tuning, state);
		}
	}
}
