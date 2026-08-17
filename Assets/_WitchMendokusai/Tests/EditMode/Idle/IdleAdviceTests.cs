using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 「다음 한 걸음」 (TASK-WM-406).
	///
	/// ★ 이 판단이 화면 안에 있을 때는 시험할 수 없었다 — 그래서 코어로 내렸다.
	///   여기서 지키는 것은 <b>순서</b>다: 사라지는 것 → 손해 보는 것 → 판을 가르는 것
	///   → 모은 것을 쓰는 것 → 사는 것 → 기다림.
	///   순서가 틀리면 안내가 사람을 <b>손해 보는 쪽</b>으로 민다.
	/// </summary>
	public sealed class IdleAdviceTests
	{
		/// <summary>★ 아무것도 없는 첫 판 — 손으로 때리라고 한다.</summary>
		[Test]
		public void FirstMinute_SaysTap()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Owned[0] = 0L; // 기지를 비운다 — 아직 아무것도 안 돈다

			Assert.AreEqual(IdleStep.Tap, Advise(state, tuning).Step);
		}

		/// <summary>★ 살 수 있으면 사라고 한다 — 그리고 <b>어느 것</b>인지 짚는다.</summary>
		[Test]
		public void WhenAffordable_PointsAtTheCheapestProducer()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Resource = IdleBase.CostOf(0, state.Owned[0], tuning);

			IdleAdviceResult advice = Advise(state, tuning);

			Assert.AreEqual(IdleStep.BuyProducer, advice.Step);
			Assert.AreEqual(0, advice.Subject, "가장 싼 것을 안 짚었다");
			Assert.Greater(advice.Amount, 1d, "사도 수입이 안 는다고 말한다");
		}

		/// <summary>
		/// ★ <b>사라지는 것이 가장 먼저다</b> — 살 것이 있어도 그건 기다려 주지만 이건 안 기다린다.
		/// </summary>
		[Test]
		public void VisitorBeatsEverything()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Resource = 1e12d;          // 살 것 천지
			state.VisitorSecondsLeft = 5d;   // 그런데 지나가는 것이 떠 있다

			Assert.AreEqual(IdleStep.CatchVisitor, Advise(state, tuning).Step);
		}

		/// <summary>
		/// ★ 가방이 차면 <b>버는 것보다 잃는 것을 먼저</b> 막는다.
		///
		/// 찬 동안에는 떨구는 게 통째로 버려지므로, 사라고 말하는 건 손해를 늘리라는 말이다.
		/// </summary>
		[Test]
		public void FullBagBeatsBuying()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Resource = 1e12d;

			for (int one = 0; one < tuning.BagCapacity; one++)
			{
				state.Bag.Add(new IdleItem(1, IdleItemSlot.Head));
			}

			Assert.AreEqual(IdleStep.BagFull, Advise(state, tuning).Step);
		}

		/// <summary>
		/// ★ 천장에 닿았으면 환생하라고 한다 — 더 내려가도 등급이 안 열리는 자리다.
		/// </summary>
		[Test]
		public void AtTheCeiling_SaysPrestige()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);

			// 천장까지 내려간 판을 만든다.
			state.BestStage = 400;
			IdleModel.TryGoToStage(state, 400);

			IdleAdviceResult advice = Advise(state, tuning);

			if (IdleModel.PrestigeAwardFor(state, tuning) > 0L)
			{
				Assert.AreEqual(IdleStep.Prestige, advice.Step,
					"천장에 닿았는데 환생하라고 안 한다");
				Assert.Greater(advice.Amount, 0d, "얼마를 받는지 안 알려준다");
			}
			else
			{
				Assert.Pass("이 손잡이에서는 아직 환생할 수 없다 — 순서 시험은 다른 판이 본다");
			}
		}

		/// <summary>★ 뽑을 수 있으면 뽑으라고 한다 — 사는 것보다 앞이다.</summary>
		[Test]
		public void PullingBeatsBuying()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Resource = 1e12d;
			state.Stones = 10L;

			Assert.AreEqual(IdleStep.Pull, Advise(state, tuning).Step);
		}

		/// <summary>★ 할 게 없으면 <b>언제쯤</b> 생기는지 말한다 — 「기다려라」만으로는 안내가 아니다.</summary>
		[Test]
		public void WhenNothingToDo_SaysHowLong()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Owned[0] = 1L;
			state.Resource = 0d;

			IdleAdviceResult advice = Advise(state, tuning);

			Assert.AreEqual(IdleStep.Wait, advice.Step);
			Assert.Greater(advice.Amount, 0d, "얼마나 기다려야 하는지 안 알려준다");
			Assert.IsFalse(double.IsInfinity(advice.Amount), "영영 못 산다고 말한다");
		}

		private static IdleAdviceResult Advise(IdleState state, IdleTuning tuning)
		{
			IdleSession session = new IdleSession(tuning, state);
			return IdleAdvice.NextStep(session.Capture());
		}
	}
}
