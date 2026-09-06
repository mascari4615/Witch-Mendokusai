using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 기지(클리커 층)와 모험(스쿼드 층)이 <b>서로를 부르는가</b> (TASK-WM-406).
	///
	/// ★ 사용자 지적에서 나온 판이다 — 「생산자 클리커 계열 같은데 아직 잘 안 녹아든다」.
	///   원인은 잡기 하나가 자원도 장비도 다 냈다는 것. 그러면 기지가 있을 이유가 없다.
	///   그래서 갈랐다: <b>자원은 기지가, 장비는 모험이</b>.
	///   여기서 지키는 것은 그 <b>갈라짐</b>과 <b>맞물림</b>이다.
	/// </summary>
	public sealed partial class IdleBaseTests
	{
		private const double TOLERANCE = 1e-9d;

		/// <summary>★ 잡기는 자원을 안 낸다 — 안 그러면 기지가 있을 이유가 없다.</summary>
		[Test]
		public void Killing_EarnsGear_NotResource()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			// 처음엔 생산자 하나를 쥐여 주므로, <b>잡기만</b>을 재려면 기지를 비운다.
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Owned[0] = 0L;

			IdleModel.Step(state, tuning, 600d);

			Assert.Greater(state.Kills, 0L, "10분을 돌렸는데 아무것도 안 잡았다");
			Assert.AreEqual(0d, state.Resource, TOLERANCE, "잡기가 자원을 냈다 — 두 층이 도로 합쳐졌다");
			Assert.Greater(state.Bag.Count, 0, "잡았는데 가방에 아무것도 안 들어왔다");
		}

		/// <summary>★ 기지는 잡든 안 잡든 자원을 낸다 — 시간이 내는 것이다.</summary>
		[Test]
		public void Base_EarnsResourceOverTime()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Owned[0] = 10L;

			double expected = 10L * IdleBase.OutputOf(0, tuning) * 60d;
			IdleModel.Step(state, tuning, 60d);

			Assert.AreEqual(expected, state.Resource, 1e-6d, "기지가 시간만큼 안 냈다");
		}

		/// <summary>살수록 값이 오른다 — 생산자 클리커 계열의 1.15배 그대로.</summary>
		[Test]
		public void Producers_GetPricierAsYouBuy()
		{
			IdleTuning tuning = new IdleTuning();

			double first = IdleBase.CostOf(0, 0L, tuning);
			double eleventh = IdleBase.CostOf(0, 10L, tuning);

			Assert.AreEqual(first * System.Math.Pow(1.15d, 10d), eleventh, 1e-6d);
			Assert.Greater(IdleBase.CostOf(1, 0L, tuning), first, "위 번호가 더 싸다");
			Assert.Greater(IdleBase.OutputOf(1, tuning), IdleBase.OutputOf(0, tuning), "위 번호가 덜 낸다");
		}

		/// <summary>자원이 모자라면 안 사진다.</summary>
		[Test]
		public void Buying_NeedsResource()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			long had = state.Owned[0];

			Assert.IsFalse(IdleBase.TryBuy(state, tuning, 0), "빈손인데 사졌다");

			// 값은 <b>이미 가진 수</b>에 따라 오른다 — 첫 하나를 쥐여 줬으므로 그만큼 비싸다.
			state.Resource = IdleBase.CostOf(0, had, tuning);
			Assert.IsTrue(IdleBase.TryBuy(state, tuning, 0));
			Assert.AreEqual(0d, state.Resource, 1e-6d, "값을 안 치렀다");
			Assert.AreEqual(had + 1L, state.Owned[0]);
		}

		/// <summary>
		/// ★ <b>한 층만으로는 못 큰다</b> — 이게 「합쳐졌다」의 실체다.
		///
		/// 기지만 굴린 판과 두 층을 다 굴린 판을 비교한다.
		/// 기지만 굴리면 자원은 쌓여도 <b>장비가 안 모인다</b>(용병이 약해 깊이 못 감).
		/// </summary>
		[Test]
		public void OneLayerAlone_CannotGrow()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState baseOnly = new IdleState();
			IdleState both = new IdleState();

			const double TICK = 10d;
			for (double elapsed = 0d; elapsed < 3d * 3600d; elapsed += TICK)
			{
				IdleModel.Step(baseOnly, tuning, TICK);
				IdlePlay.BuyProducers(baseOnly, tuning);

				IdleModel.Step(both, tuning, TICK);
				IdlePlay.BuyEverything(both, tuning);
			}

			TestContext.WriteLine("[IdleBase] 3시간 — 기지만: " + baseOnly.Stage + "단계 · 가방 " + baseOnly.Bag.Count
				+ "  ||  두 층: " + both.Stage + "단계 · 가방 " + both.Bag.Count);

			// 기지만 굴리면 용병이 기본값 그대로라 얕은 데서 맴돈다 — 장비가 안 모인다.
			Assert.Greater(both.Bag.Count, baseOnly.Bag.Count,
				"용병을 올려도 장비가 더 안 모인다 — 자원이 모험과 안 물린다");
		}

		/// <summary>
		/// ★ <b>감정에 자원이 든다</b> — 이 하나가 두 층을 같은 저울에 올린다.
		/// 공짜면 「올릴까 감정할까」가 결정이 아니다.
		/// </summary>
		[Test]
		public void Appraising_CostsResource()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureTierRoom(4);
			state.DroppedByTier[3] = 5L;

			Assert.IsFalse(IdlePotentials.TryAppraise(state, tuning, 4, out PotentialRoll _),
				"빈손인데 감정이 됐다");

			state.Resource = IdleGear.AppraiseCost(4, tuning);
			Assert.IsTrue(IdlePotentials.TryAppraise(state, tuning, 4, out PotentialRoll roll));
			Assert.AreEqual(0d, state.Resource, 1e-6d, "감정 값을 안 치렀다");
			Assert.Greater(roll.Value, 0d);
		}

		/// <summary>
		/// ★ <b>다음 것은 못 사도 보인다</b> (사용자 지적 2026-08-16).
		///
		/// 전에는 값의 절반을 모아야 다음 줄이 나타났다 — 돈이 모자란 동안 다음 단계가
		/// <b>사라진 것처럼</b> 보였고, 그건 사람 눈에 버그다. 목표가 안 보이면 모을 이유도 없다.
		/// </summary>
		[Test]
		public void NextProducer_StaysVisible_EvenWhenBroke()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Owned[0] = 1L;
			state.Resource = 0d;

			Assert.IsFalse(IdleBase.IsHidden(1, state), "빈손이라고 다음 생산자를 숨겼다");
			Assert.IsTrue(IdleBase.IsHidden(2, state), "그 다음 것까지 펴 놓으면 뭘 할지가 안 보인다");
		}

		/// <summary>산 다음에는 그 다음 것이 열린다 — 한 칸씩 앞이 보인다.</summary>
		[Test]
		public void BuyingReveals_TheOneAfter()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Owned[0] = 1L;
			state.Owned[1] = 1L;

			Assert.IsFalse(IdleBase.IsHidden(2, state));
		}

		/// <summary>기지가 저장을 건넌다 — 안 그러면 껐다 켤 때마다 처음부터 짓는다.</summary>
		[Test]
		public void Base_SurvivesSaveLoad()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Owned[0] = 7L;
			state.Owned[2] = 3L;

			IdleState restored = new IdleState();
			restored.Load(state.Save());

			Assert.AreEqual(7L, restored.Owned[0]);
			Assert.AreEqual(3L, restored.Owned[2]);
		}

		/// <summary>옛 저장에는 기지가 없다 — 빈 기지로 들어온다(터지지 않는다).</summary>
		[Test]
		public void OldSaves_LoadWithEmptyBase()
		{
			IdleState fromOld = new IdleState();
			fromOld.Load(new IdleSaveData { Resource = 5d });

			Assert.IsNotNull(fromOld.Owned);
			Assert.AreEqual(0d, IdleBase.OutputPerSecond(fromOld, new IdleTuning()), TOLERANCE);
		}

		/// <summary>
		/// ★ 「살 게 있다」고 말하면 <b>실제로 하나는 사진다</b> — 버튼이 거짓말하지 않는다.
		///
		/// 화면은 이 답으로 버튼을 켠다. 두 벌로 두면 버튼은 켜져 있고 눌러도
		/// 아무 일이 안 나는 상태가 언젠가 생긴다.
		/// </summary>
		[Test]
		public void SayingYouCanBuy_MeansYouActuallyCan()
		{
			IdleTuning tuning = new IdleTuning();

			// 자원을 조금씩 올려 가며 <말과 실제>가 매번 맞는지 본다.
			double[] purses = { 0d, 1d, 14d, 15d, 16d, 150d, 1e6d };

			foreach (double purse in purses)
			{
				IdleState state = new IdleState();
				state.EnsureProducerRoom(tuning.ProducerCount);
				state.Resource = purse;

				bool said = IdleBase.CheapestAffordable(state, tuning) >= 0;
				int cheapest = IdleBase.CheapestAffordable(state, tuning);
				bool bought = cheapest >= 0 && IdleBase.TryBuy(state, tuning, cheapest);

				Assert.AreEqual(said, bought, "자원 " + purse + ", 말과 실제가 다르다");
			}
		}

		/// <summary>★ 「올릴 게 있다」도 마찬가지 — 말과 실제가 같아야 한다.</summary>
		[Test]
		public void SayingYouCanRaise_MeansYouActuallyCan()
		{
			IdleTuning tuning = new IdleTuning();
			double[] purses = { 0d, 9d, 10d, 24d, 25d, 1e6d };

			foreach (double purse in purses)
			{
				IdleState state = new IdleState();
				IdleHeroes.EnsureStarter(state);
				state.EnsureProducerRoom(tuning.ProducerCount);
				state.Resource = purse;

				bool said = IdleModel.TryGetCost(state, tuning, IdleHeroes.STARTER_ID,
					IdleUpgradeKind.Damage, 1, out double cost) && state.Resource >= cost;
				bool raised = IdleModel.TryRaise(state, tuning, IdleHeroes.STARTER_ID,
					IdleUpgradeKind.Damage, 1);

				Assert.AreEqual(said, raised, "자원 " + purse + ", 말과 실제가 다르다");
			}
		}

		/// <summary>
		/// ★ 「사면 몇 배가 되나」가 <b>맞는 값</b>이다 — 화면의 간판 숫자인데 시험이 없었다.
		/// </summary>
		[Test]
		public void TheIncomeGain_IsTheRealRatio()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Owned[0] = 4L;

			IdleSession session = new IdleSession(tuning, state);
			IdleSnapshot now = session.Capture();

			double before = IdleBase.OutputPerSecond(state, tuning);
			state.Owned[0] += 1L;
			double after = IdleBase.OutputPerSecond(state, tuning);
			state.Owned[0] -= 1L;

			Assert.AreEqual(after / before, now.Producers[0].IncomeGain, 1e-9d,
				"사면 몇 배가 되는지를 틀리게 말한다");
			Assert.Greater(now.Producers[0].IncomeGain, 1d);
		}

		/// <summary>★ 첫 수입일 때는 <b>무한</b> — 0 에서 뭔가로 가는 건 「몇 배」로 못 적는다.</summary>
		[Test]
		public void TheFirstIncome_IsInfinite()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);

			for (int kind = 0; kind < state.Owned.Length; kind++)
			{
				state.Owned[kind] = 0L;
			}

			IdleSnapshot now = new IdleSession(tuning, state).Capture();

			Assert.IsTrue(double.IsInfinity(now.Producers[0].IncomeGain),
				"아무것도 안 벌 때 「몇 배」를 유한한 수로 말한다");
		}
	}
}

