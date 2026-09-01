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
	public sealed class IdleBaseTests
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

		/// <summary>
		/// ★ 화면이 <b>때리는 장단</b>을 코어에서 받는다 — 지어내면 올려도 빨라진 게 안 보인다.
		/// </summary>
		[Test]
		public void Snapshot_CarriesAttackSpeed()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			IdleSession session = new IdleSession(tuning, state);

			double before = session.Capture().AttacksPerSecond;
			Assert.Greater(before, 0d, "안 때리는 걸로 보인다");

			state.Resource = 1e12d;
			session.Send(new IdleRaiseUpgradeIntent(IdleHeroes.STARTER_ID, IdleUpgradeKind.AttackSpeed, 1));

			Assert.Greater(session.Capture().AttacksPerSecond, before, "속도를 올렸는데 장단이 그대로다");
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
		/// ★ 사진 찍기가 <b>쓰레기를 안 만든다</b> — 방치형은 밤새 켜 두는 게 기본값이다.
		///
		/// 실측(2026-08-17): 고치기 전엔 <b>한 번에 2472 바이트</b>였다(가방 40칸·영웅 16).
		/// 60프레임 x 8시간이면 <b>4 GB</b>어치다. 지금은 판을 돌려 써서 0 이다.
		///
		/// ⚠ 그 대가로 <b>사진은 다음 사진을 찍을 때까지만 살아 있다</b>. 들고 있다가 나중에
		///   보면 그때는 다른 판이다 — 들고 있어야 하면 복사해서 들어라.
		///   그 성질을 아래 시험이 같이 못 박는다.
		/// </summary>
		[Test]
		public void TakingThePicture_MakesNoGarbage()
		{
			IdleSession session = Loaded(out IdleTuning _);

			session.Capture();

			long before = System.GC.GetAllocatedBytesForCurrentThread();

			for (int again = 0; again < 100; again++)
			{
				session.Capture();
			}

			long each = (System.GC.GetAllocatedBytesForCurrentThread() - before) / 100L;

			TestContext.WriteLine("[할당] 사진 한 번 = " + each + " 바이트");

			Assert.LessOrEqual(each, 64L,
				"사진 한 번에 " + each + " 바이트를 만든다 — 밤새 켜 두면 그게 그대로 쌓인다");
		}

		/// <summary>
		/// ★ 그 대신 <b>사진은 다음 사진까지만</b> 유효하다 — 성질을 못 박아 둔다.
		///
		/// 이걸 안 적어 두면 다음 사람이 사진을 들고 있다가 <b>조용히 다른 판</b>을 보게 된다.
		/// </summary>
		[Test]
		public void AnOldPicture_ShowsTheNewBoard()
		{
			IdleSession session = Loaded(out IdleTuning tuning);

			IdleSnapshot old = session.Capture();
			int wasBag = old.Bag.Length;

			session.State.Bag[0] = new IdleItem(9, IdleItemSlot.Feet);
			session.Capture();

			Assert.AreEqual(wasBag, old.Bag.Length, "길이는 그대로여야 한다(판을 돌려 쓴다)");
			Assert.AreEqual(9, old.Bag[0].Tier,
				"들고 있던 사진이 옛 판을 보여준다 — 판을 돌려 쓰는 성질이 사라졌다면 이 시험을 지워라");
		}

		/// <summary>가방·영웅이 들어찬 판 — 사진이 제일 커지는 자리.</summary>
		private static IdleSession Loaded(out IdleTuning tuning)
		{
			tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.EnsureTierRoom(12);

			for (int one = 0; one < tuning.BagCapacity; one++)
			{
				state.Bag.Add(new IdleItem(3, IdleItemSlot.Head));
			}

			for (int id = 0; id < 16; id++)
			{
				state.Heroes.Add(new IdleHeroOwned(id));
			}

			return new IdleSession(tuning, state);
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

		/// <summary>
		/// ★ <b>사진을 찍는다고 판이 바뀌면 안 된다</b> — 조회가 판을 건드리던 자리가 있었다 (회귀).
		///
		/// 「사면 몇 배」를 재려고 생산자를 하나 얹었다 되돌렸다. 그 사이에 무슨 일이 나면
		/// 공짜 생산자가 남는다. 지금은 안 건드린다 — 그걸 못 박는다.
		/// </summary>
		[Test]
		public void TakingThePicture_DoesNotTouchTheBoard()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Owned[0] = 3L;
			state.Owned[1] = 1L;
			state.Resource = 500d;

			state.Stage = 6;
			state.BestStage = 25;
			state.Heroes.Add(new IdleHeroOwned(1));
			state.Bag.Add(new IdleItem(2, IdleItemSlot.Feet));

			// 세션 생성은 사진이 아님 (시작 인형 착석). 사진은 Capture
			IdleSession session = new IdleSession(tuning, state);
			IdleSaveData before = state.Save();
			session.Capture();
			IdleSaveData after = state.Save();

			// ⚠ 네 칸만 보면 <b>반만 보는 감시</b>다 — 사진이 어느 칸을 건드려도 잡히게 전부 본다.
			//   (오늘 이 자리에서만 둘을 잡았다: 「사면 몇 배」와 「어디서 파는 게 빠른가」.
			//    둘 다 판을 잠깐 바꿔 놓고 되돌리는 방식이었다.)
			System.Reflection.FieldInfo[] fields = typeof(IdleSaveData)
				.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

			Assert.Greater(fields.Length, 10, "저장 꼴이 비었다 — 시험이 아무것도 안 보고 있다");

			foreach (System.Reflection.FieldInfo field in fields)
			{
				object one = field.GetValue(before);
				object other = field.GetValue(after);

				if (one is System.Array first)
				{
					System.Array second = (System.Array)other;
					Assert.AreEqual(first.Length, second.Length, field.Name + " 의 길이가 달라졌다");

					for (int at = 0; at < first.Length; at++)
					{
						Assert.AreEqual(first.GetValue(at), second.GetValue(at),
							"사진을 찍었더니 " + field.Name + " 의 " + at + "번째가 달라졌다");
					}

					continue;
				}

				Assert.AreEqual(one, other, "사진을 찍었더니 " + field.Name + " 가 달라졌다");
			}
		}

		/// <summary>
		/// ★ <b>버튼이 코어에 닿는 길</b>이 맞는지 — 의도 하나하나.
		///
		/// ⚠ 시험은 여태 코어 함수를 <b>직접</b> 불렀다(TryBuy·TryMerge…). 그래서 화면이 보내는
		///   <b>의도</b>가 엉뚱한 함수에 닿거나 값을 흘려도 <b>전부 초록</b>이었다.
		///   오늘 파티 자리 복제 버그가 정확히 그 자리에서 나왔다(Send(IdleSetPartyIntent)).
		///   여기서는 <b>보낸 결과</b>만 본다 — 판이 실제로 그렇게 됐나.
		/// </summary>
		[Test]
		public void BuyingProducer_ThroughTheIntent_Works()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Resource = 1e6d;

			IdleSession session = new IdleSession(tuning, state);
			long before = state.Owned[1];

			Assert.IsTrue(session.Send(new IdleBuyProducerIntent(1)), "샀다는 답이 안 온다");
			Assert.AreEqual(before + 1L, state.Owned[1], "<1번>을 샀는데 다른 게 늘었다");
			Assert.Less(state.Resource, 1e6d, "자원을 안 썼다");
		}

		/// <summary>★ 물러나기 의도가 <b>그 단계로</b> 옮긴다.</summary>
		[Test]
		public void GoingToStage_ThroughTheIntent_Works()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Stage = 20;
			state.BestStage = 30;

			IdleSession session = new IdleSession(tuning, state);

			Assert.IsTrue(session.Send(new IdleGoToStageIntent(7)));
			Assert.AreEqual(7, state.Stage, "엉뚱한 자리로 갔다");

			Assert.IsFalse(session.Send(new IdleGoToStageIntent(999)), "가 본 적 없는 데로 보낸다");
			Assert.AreEqual(7, state.Stage);
		}

		/// <summary>★ 차기 의도가 <b>그 자리에</b> 채운다 — 가방에서 빠지고 부위에 들어간다.</summary>
		[Test]
		public void EquippingThroughTheIntent_Works()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Bag.Add(new IdleItem(5, IdleItemSlot.Feet));

			IdleSession session = new IdleSession(tuning, state);

			Assert.IsTrue(session.Send(new IdleEquipIntent(IdleHeroes.STARTER_ID, 0)));
			Assert.AreEqual(0, state.Bag.Count, "가방에서 안 빠졌다");
			Assert.AreEqual(5, state.Worn[(int)IdleItemSlot.Feet].Tier, "엉뚱한 부위에 찼다");
		}

		/// <summary>★ 합치기 의도가 <b>그 등급·그 부위</b>를 합친다.</summary>
		[Test]
		public void MergingThroughTheIntent_Works()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Resource = 1e9d;

			for (int one = 0; one < tuning.MergeCount; one++)
			{
				state.Bag.Add(new IdleItem(2, IdleItemSlot.Head));
			}

			IdleSession session = new IdleSession(tuning, state);

			Assert.IsTrue(session.Send(new IdleMergeIntent(2, IdleItemSlot.Head)));
			Assert.AreEqual(1, state.Bag.Count, "재료가 안 없어졌다");
			Assert.AreEqual(3, state.Bag[0].Tier, "한 단계 위로 안 갔다");
			// 결과 부위는 굴림 (2026-08-31). 재료 부위와 무관
			Assert.GreaterOrEqual((int)state.Bag[0].Slot, 0);
			Assert.Less((int)state.Bag[0].Slot, IdleGear.SLOT_COUNT);
		}

		/// <summary>★ 뽑기 의도가 <b>실제로 뽑는다</b> — 값도 치른다.</summary>
		[Test]
		public void PullingThroughTheIntent_Works()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Resource = 1e9d;
			state.Stones = 5L;

			IdleSession session = new IdleSession(tuning, state);

			Assert.IsTrue(session.Send(new IdlePullHeroIntent()), "뽑았다는 답이 안 온다");

			// 시작 인형 하나 있음. 뽑은 것이 새 얼굴이면 둘, 시작 인형과 겹치면 하나 + 중복 1
			int faces = 0;
			for (int index = 0; index < state.Heroes.Count; index++)
			{
				faces += 1 + state.Heroes[index].Copies;
			}

			Assert.AreEqual(2, faces, "뽑았는데 아무도 안 왔다 (시작 인형 1 + 뽑은 것 1)");
			Assert.AreEqual(4L, state.Stones, "환생석을 안 썼다");
			Assert.Less(state.Resource, 1e9d, "자원을 안 썼다");
		}
	}
}
