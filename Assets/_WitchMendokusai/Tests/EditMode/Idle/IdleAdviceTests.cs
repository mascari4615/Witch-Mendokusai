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

		/// <summary>
		/// ★ 「얼마나 기다리나」는 <b>두 축을 다</b> 본다 — 전에는 공격속도를 빼먹었다 (회귀).
		///
		/// 속도가 곧 살 수 있는데도 화면이 훨씬 뒤를 말할 수 있었다.
		/// 기다리라는 말은 「얼마나」가 맞아야 안내가 된다 — 틀린 시각은 침묵보다 나쁘다.
		/// </summary>
		[Test]
		public void HowLongToWait_LooksAtBothAxes()
		{
			IdleState state = Fresh(out IdleTuning tuning);

			// ⚠ 판을 <b>일부러</b> 고른다 (실측 2026-08-17): 첫 판에서는 공격력이 어차피 가장
			//   이르러서(20초 vs 50초) 속도를 빼먹어도 답이 안 바뀌었다 — 처음 쓴 시험이
			//   눈뜬장님이었다. 여기서는 공격력을 12까지 올려 <b>속도가 가장 이른</b> 판을 만든다
			//   (공격력 36초 · 속도 8.3초 · 기지 11.6초).
			state.Damage.Level = 12;
			state.Owned[0] = 6L;

			IdleSnapshot now = Look(state);
			IdleAdviceResult advice = IdleAdvice.NextStep(now);

			Assert.Less(now.AttackSpeed.SecondsToAfford, now.Damage.SecondsToAfford,
				"이 판에서는 속도가 가장 이르지 않다 — 시험이 아무것도 안 본다");

			Assert.AreEqual(IdleStep.Wait, advice.Step, "잴 판이 아니다 — 지금 할 것이 있다");

			double soonest = double.PositiveInfinity;
			soonest = Nearer(soonest, now.Damage.SecondsToAfford);
			soonest = Nearer(soonest, now.AttackSpeed.SecondsToAfford);

			for (int kind = 0; kind < now.Producers.Length; kind++)
			{
				if (now.Producers[kind].Hidden == false)
				{
					soonest = Nearer(soonest, now.Producers[kind].SecondsToAfford);
				}
			}

			Assert.AreEqual(soonest, advice.Amount, 1e-6d,
				"가장 이른 것을 안 짚는다 — 어느 축을 빼먹었다");
		}

		private static double Nearer(double soonest, double seconds)
		{
			if (seconds <= 0d)
			{
				return soonest;
			}

			return seconds < soonest ? seconds : soonest;
		}


		/// <summary>
		/// ★ 여러 번 불러도 <b>같은 답</b>이다 — 씻어 쓰는 판이 지난 셈을 물고 있지 않다.
		///
		/// 매 프레임 예닐곱 번 부르는 자리라 판을 새로 안 만들고 씻어 쓴다(쓰레기 줄이기).
		/// 씻는 걸 빠뜨리면 두 번째 부름부터 <b>수가 불어난다</b> — 그 순간을 잡는 시험이다.
		/// </summary>
		[Test]
		public void CountingTwice_GivesTheSameAnswer()
		{
			IdleState state = Fresh(out IdleTuning tuning);

			for (int one = 0; one < tuning.MergeCount; one++)
			{
				state.Bag.Add(new IdleItem(2, IdleItemSlot.Head));
			}

			IdleSnapshot now = Look(state);

			int first = IdleAdvice.MergeableCount(now);
			Assert.AreEqual(1, first, "합칠 한 벌을 못 셌다 — 시험이 아무것도 안 보고 있다");

			for (int again = 0; again < 5; again++)
			{
				Assert.AreEqual(first, IdleAdvice.MergeableCount(now),
					"부를 때마다 답이 달라진다 — 판을 안 씻고 있다");
			}
		}

		/// <summary>
		/// ★ 다른 판을 물고 오지 않는다 — 앞 판의 가방이 다음 셈에 안 섞인다.
		///
		/// ⚠ 판을 <b>일부러</b> 이렇게 짠다 (실측 2026-08-17): 처음엔 「6개 → 그다음 빈 가방」으로
		///   썼는데, 씻기를 꺼도 <b>둘 다 그대로</b> 통과했다 — 빈 가방은 아무것도 안 더하니까.
		///   「같은 수를 두 번」도 마찬가지로 안 걸린다(같은 양을 더하면 배수를 또 넘는다).
		///   그래서 <b>둘 다 한 벌이 안 되지만 합치면 한 벌이 되는</b> 수(2 + 1)를 쓴다.
		///   씻기를 빠뜨리면 두 번째가 0 이 아니라 1 이 된다.
		/// </summary>
		[Test]
		public void OneBoardDoesNotLeakIntoTheNext()
		{
			IdleTuning tuning = new IdleTuning();
			Assert.AreEqual(3, tuning.MergeCount, "합치는 개수가 3 이 아니면 아래 수를 다시 골라야 한다");

			IdleState few = Fresh(out IdleTuning _);
			few.Bag.Add(new IdleItem(2, IdleItemSlot.Head));
			few.Bag.Add(new IdleItem(2, IdleItemSlot.Head));

			IdleState fewer = Fresh(out IdleTuning _);
			fewer.Bag.Add(new IdleItem(2, IdleItemSlot.Head));

			Assert.AreEqual(0, IdleAdvice.MergeableCount(Look(few)), "둘로는 못 합친다");
			Assert.AreEqual(0, IdleAdvice.MergeableCount(Look(fewer)),
				"앞 판의 둘이 남아서 하나뿐인 판이 합칠 수 있다고 나온다");
		}

		/// <summary>★ 손잡이를 바꾸면 <b>안내도 따라온다</b> — 3 이 코드에 박혀 있으면 안 된다.</summary>
		[Test]
		public void TheAdviceFollowsTheMergeKnob()
		{
			IdleTuning wider = new IdleTuning();
			wider.MergeCount = 4;

			IdleState state = new IdleState();
			state.EnsureProducerRoom(wider.ProducerCount);

			for (int one = 0; one < 3; one++)
			{
				state.Bag.Add(new IdleItem(2, IdleItemSlot.Head));
			}

			Assert.AreEqual(0, IdleAdvice.MergeableCount(new IdleSession(wider, state).Capture()),
				"넷을 모아야 하는 판인데 셋으로 합칠 수 있다고 한다");

			state.Bag.Add(new IdleItem(2, IdleItemSlot.Head));

			Assert.AreEqual(1, IdleAdvice.MergeableCount(new IdleSession(wider, state).Capture()));
		}

		/// <summary>
		/// ★ <b>높은 등급도</b> 합칠 것으로 세어진다 — 후반에 조용히 빠지지 않는다 (회귀).
		///
		/// 세는 판이 64칸으로 못 박혀 있었고 자리는 「등급 x 부위수」로 잡는다.
		/// 등급 천장은 환생할수록 오르므로(기본 6 + 환생마다 2) 다섯 번 환생하면 16 —
		/// 16 x 4 = 64 로 딱 넘어가 <b>맨 위 등급이 안 세어졌다</b>.
		/// 합칠 게 있는데 화면이 아무 말도 안 하는 상태가 후반에 생긴다.
		/// </summary>
		[Test]
		public void HighTiers_AreStillCounted()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);

			// 환생을 다섯 번 한 판 — 등급 천장이 16 이 된다.
			state.Ascensions = 5;
			int ceiling = IdleDrops.CeilingFor(state.Ascensions, tuning);
			Assert.GreaterOrEqual(ceiling, 16, "이 튜닝에서는 천장이 안 올라간다 — 시험을 다시 골라야 한다");

			state.EnsureTierRoom(ceiling);

			for (int one = 0; one < tuning.MergeCount; one++)
			{
				state.Bag.Add(new IdleItem(ceiling, IdleItemSlot.Feet));
			}

			Assert.AreEqual(1, IdleAdvice.MergeableCount(Look(state)),
				"천장 등급 한 벌을 안 셌다 — 후반에 합칠 것이 조용히 사라진다");
		}


		/// <summary>
		/// ★ 서랍 칸 이름은 <b>다섯 개</b>다 — <see cref="IdleTab"/> 과 수가 같아야 한다.
		///
		/// 화면은 이 수만큼 이름표를 들고 있고(점 찍힌 것까지 두 벌), 칸을 그릴 때 그 번호로
		/// 짚는다. 칸이 하나 늘면 화면이 배열 밖을 짚어 터진다 — 늘릴 때 같이 늘리라는 표시다.
		/// </summary>
		[Test]
		public void TheDrawerHasFiveTabs()
		{
			Assert.AreEqual(5, System.Enum.GetValues(typeof(IdleTab)).Length,
				"서랍 칸 수가 바뀌었다 — IdleScreen 의 이름표(TAB_NAMES · TAB_NAMES_DOT)도 같이 고칠 것");

			Assert.AreEqual((int)IdleTab.Prestige, 4, "환생 칸이 마지막이 아니다");
		}
	}
}
