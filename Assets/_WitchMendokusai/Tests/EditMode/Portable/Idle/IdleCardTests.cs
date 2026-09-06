using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 카드·코스트 — V2 개입층 (concept-v2, 사용자 방향 2026-08-23).
	///
	/// ★ 여기서 지키는 것 넷:
	///   ① 코스트는 시간이 채우고 <b>스텝을 쪼개도 같게</b> 찬다 (오프라인 정산의 전제)
	///   ② 카드는 코스트가 모자라면 <b>아무 일도</b> 안 일어난다
	///   ③ 보급 배수는 스텝 <b>중간에 끝나도</b> 쪼개 밟은 것과 결과가 같다 (경계 분할)
	///   ④ 감정 카드는 자원을 안 쓰되 <b>개수는 쓴다</b> (깊이의 뜻을 지키는 선)
	/// </summary>
	public sealed class IdleCardTests
	{
		/// <summary>★ 코스트 채움의 스텝 불변 — 60초 한 번 == 0.1초 600번.</summary>
		[Test]
		public void CostFills_TheSame_HoweverTheStepIsSplit()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState once = new IdleState();
			IdleModel.Step(once, tuning, 60d);

			IdleState split = new IdleState();
			for (int beat = 0; beat < 600; beat++)
			{
				IdleModel.Step(split, tuning, 0.1d);
			}

			Assert.AreEqual(once.Cost, split.Cost, 1e-9d,
				"쪼개 밟았더니 코스트가 다르게 찼다 — 오프라인 정산이 이 위에 선다");
		}

		/// <summary>★ 상한에서 멎는다 — 자리를 아무리 비워도 게이지는 가득까지다.</summary>
		[Test]
		public void Cost_StopsAtTheCap()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			IdleModel.Step(state, tuning, 8d * 3600d);

			Assert.AreEqual(tuning.CostMax, state.Cost, 1e-9d, "코스트가 상한을 넘었다");
		}

		/// <summary>★ 모자라면 아무 일도 없다 — 거절이 코어 한 벌이어야 버튼이 거짓말을 못 한다.</summary>
		[Test]
		public void Casting_WithoutCost_DoesNothing()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Cost = tuning.VolleyCost - 0.5d;

			long killsBefore = state.Kills;
			bool cast = IdleCards.TryCast(state, tuning, IdleCardKind.Volley, out IdleCardResult _);

			Assert.IsFalse(cast, "코스트가 모자란데 카드가 나갔다");
			Assert.AreEqual(tuning.VolleyCost - 0.5d, state.Cost, 1e-12d, "거절됐는데 코스트가 줄었다");
			Assert.AreEqual(killsBefore, state.Kills, "거절됐는데 판이 움직였다");
		}

		/// <summary>★ 일제 사격 — 코스트를 치르고 실제로 몰아친다 (안 치면 장식이다).</summary>
		[Test]
		public void Volley_SpendsCost_AndStrikes()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState idle = new IdleState();
			IdleState volley = new IdleState();
			volley.Cost = tuning.VolleyCost;

			bool cast = IdleCards.TryCast(volley, tuning, IdleCardKind.Volley, out IdleCardResult _);

			Assert.IsTrue(cast, "코스트가 찼는데 카드가 안 나갔다");
			Assert.AreEqual(0d, volley.Cost, 1e-12d, "코스트를 안 치렀다");
			Assert.Greater(volley.Kills, idle.Kills, "일제 사격이 아무것도 안 잡았다");
		}

		/// <summary>
		/// ★ 보급 — 걸려 있는 동안 수입이 배수만큼 는다.
		/// </summary>
		[Test]
		public void Supply_MultipliesIncome_WhileItLasts()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState plain = new IdleState();
			IdleState boosted = new IdleState();
			boosted.Cost = tuning.SupplyCost;

			Assert.IsTrue(IdleCards.TryCast(boosted, tuning, IdleCardKind.Supply, out IdleCardResult result));
			Assert.AreEqual(tuning.SupplySeconds, result.EffectSeconds, 1e-12d);
			Assert.AreEqual(tuning.SupplyMultiplier, result.EffectMultiplier, 1e-12d);

			// 보급 시간 안쪽만 밟는다 — 이 구간에서는 정확히 배수여야 한다.
			double seconds = tuning.SupplySeconds * 0.5d;
			IdleModel.Step(plain, tuning, seconds);
			IdleModel.Step(boosted, tuning, seconds);

			Assert.AreEqual(plain.Resource * tuning.SupplyMultiplier, boosted.Resource, plain.Resource * 1e-9d,
				"보급이 걸렸는데 수입이 배수만큼 안 늘었다");
		}

		/// <summary>
		/// ★ 보급이 스텝 <b>중간에</b> 끝나도 결과가 같다 — 경계 분할의 증명.
		///
		/// 이게 깨지면 「자리 비운 8시간을 한 번에 밟는다」가 보급과 함께 틀린 답을 낸다.
		/// </summary>
		[Test]
		public void SupplyExpiry_MidStep_IsStepInvariant()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState once = new IdleState();
			IdleState split = new IdleState();

			once.Cost = tuning.SupplyCost;
			split.Cost = tuning.SupplyCost;
			Assert.IsTrue(IdleCards.TryCast(once, tuning, IdleCardKind.Supply, out IdleCardResult _));
			Assert.IsTrue(IdleCards.TryCast(split, tuning, IdleCardKind.Supply, out IdleCardResult _));

			// 보급(30초)이 중간에 끝나는 120초 — 한 번에 vs 0.1초씩 1200번.
			IdleModel.Step(once, tuning, 120d);
			for (int beat = 0; beat < 1200; beat++)
			{
				IdleModel.Step(split, tuning, 0.1d);
			}

			Assert.AreEqual(once.Resource, split.Resource, once.Resource * 1e-9d,
				"보급이 스텝 중간에 끝나자 쪼개 밟은 판과 갈렸다 — 경계 분할이 안 됐다");
			Assert.AreEqual(0d, once.SupplySecondsLeft, 1e-9d, "보급이 안 끝났다");
		}

		/// <summary>★ 감정 카드 — 자원 0 이어도 굴리고, 개수는 하나 쓴다.</summary>
		[Test]
		public void AppraiseCard_RollsWithoutResource_ButSpendsTheCount()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureTierRoom(3);
			state.DroppedByTier[2] = 1L; // 3등급 하나 — 레어 잠재가 붙는 등급.
			state.Cost = tuning.AppraiseCardCost;
			state.Resource = 0d;

			bool cast = IdleCards.TryCast(state, tuning, IdleCardKind.Appraise, out IdleCardResult result);

			Assert.IsTrue(cast, "굴릴 것이 있는데 감정 카드가 거절됐다");
			Assert.IsTrue(result.HasRoll, "감정 카드가 굴림 결과를 안 돌려줬다");
			Assert.AreEqual(0L, state.DroppedByTier[2], "개수를 안 썼다 — 무한 굴림이 된다");
			Assert.AreEqual(0d, state.Resource, 1e-12d, "자원이 움직였다 — 카드가 면제하는 것은 자원이다");
			Assert.Greater(state.BestPotentialValue, 0d, "굴렸는데 잠재가 안 붙었다");
		}

		/// <summary>★ 굴릴 것이 없으면 감정 카드는 거절 — 코스트도 안 쓴다.</summary>
		[Test]
		public void AppraiseCard_RefusedWhenNothingToRoll()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Cost = tuning.AppraiseCardCost;

			Assert.IsFalse(IdleCards.TryCast(state, tuning, IdleCardKind.Appraise, out IdleCardResult _),
				"굴릴 것이 없는데 감정 카드가 나갔다");
			Assert.AreEqual(tuning.AppraiseCardCost, state.Cost, 1e-12d, "거절됐는데 코스트가 줄었다");
		}

		/// <summary>★ 저장을 건너 살아남는다 — 코스트·보급 남은 시간.</summary>
		[Test]
		public void CostAndSupply_SurviveTheSave()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Cost = 4.5d;
			state.SupplySecondsLeft = 12d;

			IdleState back = new IdleState();
			back.Load(state.Save());

			Assert.AreEqual(4.5d, back.Cost, 1e-12d, "코스트가 저장을 못 건넜다");
			Assert.AreEqual(12d, back.SupplySecondsLeft, 1e-12d, "보급 남은 시간이 저장을 못 건넜다");
		}

		/// <summary>★ 사진에 실린다 — 화면이 손패를 자기 눈으로 세지 않게.</summary>
		[Test]
		public void CastingFromTheHand_MovesOnlyTheUsedCardToTheBack()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			IdleCards.EnsureDeck(state);
			state.Cost = tuning.VolleyCost;

			Assert.IsTrue(IdleCards.TryCastHand(state, tuning, 0, out IdleCardResult result));
			Assert.AreEqual(IdleCardKind.Volley, result.Kind);
			Assert.AreEqual(IdleCardKind.Supply, IdleCards.HandAt(state, 0));
			Assert.AreEqual(IdleCardKind.Appraise, IdleCards.HandAt(state, 1));
			Assert.AreEqual(IdleCardKind.Volley, (IdleCardKind)state.CardDeck[IdleCards.DECK_SIZE - 1]);
		}

		/// <summary>★ 예고가 순환을 따라온다 - 안 그러면 화면이 거짓말을 한다 (gap-2026-08-23 P1)</summary>
		[Test]
		public void TheQueue_ShowsWhatComesNext()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			IdleCards.EnsureDeck(state);

			IdleCardKind wasFirstInLine = IdleCards.QueuedAt(state, 0);
			Assert.AreEqual((IdleCardKind)state.CardDeck[IdleCards.HAND_SIZE], wasFirstInLine);

			state.Cost = tuning.VolleyCost;
			Assert.IsTrue(IdleCards.TryCastHand(state, tuning, 0, out IdleCardResult _));

			Assert.AreEqual(wasFirstInLine, IdleCards.HandAt(state, IdleCards.HAND_SIZE - 1),
				"줄 서 있던 첫 카드가 손패 맨 뒤로 안 올라왔다");
			Assert.AreEqual(IdleCardKind.Volley, IdleCards.QueuedAt(state, IdleCards.QUEUE_SIZE - 1),
				"낸 카드가 줄 맨 뒤에 안 붙었다");
		}

		/// <summary>★ 사진이 줄을 싣는다 - 화면이 덱을 직접 뒤지지 않게</summary>
		[Test]
		public void TheSnapshot_CarriesTheQueue()
		{
			IdleSession session = new IdleSession(new IdleTuning());
			IdleSnapshot snapshot = session.Capture();

			Assert.AreEqual(IdleCards.QUEUE_SIZE, snapshot.Queued.Length, "줄이 사진에 안 실렸다");
		}

		[Test]
		public void DeckOrder_SurvivesTheSave()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Cost = tuning.VolleyCost;
			Assert.IsTrue(IdleCards.TryCastHand(state, tuning, 0, out IdleCardResult _));

			IdleState back = new IdleState();
			back.Load(state.Save());

			Assert.AreEqual(IdleCards.DECK_SIZE, back.CardDeck.Length);
			for (int index = 0; index < IdleCards.DECK_SIZE; index++)
			{
				Assert.AreEqual(state.CardDeck[index], back.CardDeck[index]);
			}
		}

		[Test]
		public void TheSnapshot_CarriesTheHand()
		{
			IdleSession session = new IdleSession(new IdleTuning());
			IdleSnapshot snapshot = session.Capture();

			Assert.AreEqual(IdleCards.CARD_COUNT, snapshot.Cards.Length, "손패가 사진에 안 실렸다");
			Assert.AreEqual(session.Tuning.CostMax, snapshot.CostMax, 1e-12d, "코스트 상한이 사진에 안 실렸다");
		}
	}
}
