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

		/// <summary>
		/// ★ 닫힌 칸의 <b>점</b> — 아무 일도 없으면 어느 칸에도 안 찍힌다.
		///
		/// 점이 늘 켜져 있으면 그건 장식이지 알림이 아니다. 첫 판(자원 0)에서 다섯 칸 모두 꺼져야 한다.
		/// </summary>
		[Test]
		public void OnTheFirstBoard_NoTabHasADot()
		{
			IdleSnapshot now = Look(Fresh(out IdleTuning _));

			for (int tab = 0; tab <= (int)IdleTab.Prestige; tab++)
			{
				Assert.IsFalse(IdleAdvice.HasSomethingToDo(now, (IdleTab)tab),
					((IdleTab)tab) + " 칸에 할 것도 없는데 점이 찍힌다");
			}
		}

		/// <summary>★ 살 수 있게 되면 <b>기지 칸</b>에 점이 켜진다 — 서랍에 가려 안 보이던 것.</summary>
		[Test]
		public void WhenAProducerIsAffordable_TheBaseTabLightsUp()
		{
			IdleState state = Fresh(out IdleTuning tuning);
			state.Resource = IdleBase.CostOf(0, state.Owned[0], tuning);

			Assert.IsTrue(IdleAdvice.HasSomethingToDo(Look(state), IdleTab.Base));
			Assert.IsFalse(IdleAdvice.HasSomethingToDo(Look(state), IdleTab.Prestige),
					"엉뚱한 칸까지 같이 켜진다");
		}

		/// <summary>★ 가방이 차면 <b>장비 칸</b>에 점 — 지금 떨구는 것이 그냥 흘러간다.</summary>
		[Test]
		public void WhenTheBagIsFull_TheGearTabLightsUp()
		{
			IdleState state = Fresh(out IdleTuning tuning);
			IdleSnapshot before = Look(state);

			for (int index = 0; index < before.BagCapacity; index++)
			{
				state.Bag.Add(new IdleItem(1, IdleItemSlot.Head));
			}

			Assert.IsTrue(IdleAdvice.HasSomethingToDo(Look(state), IdleTab.Gear));
		}

		/// <summary>
		/// ★ 영웅이 있는데 <b>자리가 비면</b> 영웅 칸에 점 — 안 세운 영웅의 배수는 그냥 논다.
		///   자리를 채우면 꺼진다(할 일이 없어졌으니).
		/// </summary>
		[Test]
		public void AnEmptyPartySeat_LightsTheHeroTab()
		{
			IdleState state = Fresh(out IdleTuning tuning);
			state.Heroes.Add(new IdleHeroOwned(0));

			Assert.IsTrue(IdleAdvice.HasSomethingToDo(Look(state), IdleTab.Hero),
				"영웅이 있는데 자리가 비었는데도 조용하다");

			state.Party[0] = 0;

			Assert.IsFalse(IdleAdvice.HasSomethingToDo(Look(state), IdleTab.Hero),
				"세울 영웅이 더 없는데도 점이 남는다");
		}

		/// <summary>
		/// ★ <b>확실히</b> 더 나은 것만 점을 켠다 — 빈 자리 · 더 높은 등급 · 같은 등급의 더 높은 잠재.
		///   더 낮은 것을 들고 있다고 점이 켜지면 사람은 곧 점을 안 믿는다.
		/// </summary>
		[Test]
		public void OnlyAClearlyBetterItem_LightsTheGearTab()
		{
			IdleState state = Fresh(out IdleTuning _);

			state.Bag.Add(new IdleItem(1, IdleItemSlot.Head));
			Assert.IsTrue(IdleAdvice.HasBetterUnworn(Look(state)), "빈 자리인데도 조용하다");

			state.Worn[(int)IdleItemSlot.Head] = new IdleItem(3, IdleItemSlot.Head);
			Assert.IsFalse(IdleAdvice.HasBetterUnworn(Look(state)),
				"더 낮은 것을 들고 있는데 점이 켜진다");

			state.Bag.Add(new IdleItem(4, IdleItemSlot.Head));
			Assert.IsTrue(IdleAdvice.HasBetterUnworn(Look(state)), "더 높은 등급인데 조용하다");
		}

		private static IdleState Fresh(out IdleTuning tuning)
		{
			tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			return state;
		}

		private static IdleSnapshot Look(IdleState state)
		{
			return new IdleSession(new IdleTuning(), state).Capture();
		}

		/// <summary>
		/// ★ <b>공짜로 세지는 것</b>을 먼저 말한다 — 가방에 더 좋은 것이 있으면 「차라」.
		///
		/// 차는 데는 아무것도 안 든다. 그걸 두고 「뽑아라 / 사라」라고 말하는 안내는 틀린 안내다.
		/// 전에는 아예 말하지 않아서, 좋은 장비가 가방에서 잠자도 화면이 조용했다.
		/// </summary>
		[Test]
		public void ABetterItemInTheBag_IsToldBeforeSpending()
		{
			IdleState state = Fresh(out IdleTuning tuning);

			// 살 수도 있고 찰 수도 있는 판 — 공짜인 쪽을 먼저 말해야 한다.
			state.Resource = IdleBase.CostOf(0, state.Owned[0], tuning);
			state.Bag.Add(new IdleItem(3, IdleItemSlot.Head));

			Assert.AreEqual(IdleStep.Wear, IdleAdvice.NextStep(Look(state)).Step);
		}

		/// <summary>★ 다 차고 나면 <b>다음 걸음</b>으로 넘어간다 — 같은 말을 영영 반복하지 않는다.</summary>
		[Test]
		public void OnceWorn_TheAdviceMovesOn()
		{
			IdleState state = Fresh(out IdleTuning tuning);
			state.Resource = IdleBase.CostOf(0, state.Owned[0], tuning);
			state.Bag.Add(new IdleItem(3, IdleItemSlot.Head));

			Assert.IsTrue(IdleGear.TryEquip(state, 0));

			Assert.AreEqual(IdleStep.BuyProducer, IdleAdvice.NextStep(Look(state)).Step,
				"찼는데도 계속 차라고 한다");
		}
	}
}
