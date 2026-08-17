using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 영웅 가챠 · 보유 효과 · 파티 (TASK-WM-406).
	///
	/// ★ 사용자 결정 2026-08-17 — 가챠 수집형 / 관대한 인심 / 파티 3자리 / 중복은 ★ 승급 /
	///   보유 효과 두 겹(개별 보유 + 도감).
	///
	/// 여기서 지키는 것은 <b>그 결정들이 실제로 판을 움직이나</b>이다.
	/// 「뽑았다」가 화면 글자만 바꾸면 그건 도감 놀이지 성장이 아니다.
	/// </summary>
	public sealed class IdleHeroTests
	{
		/// <summary>★ 환생석이 없으면 못 뽑는다 — 뽑기가 공짜면 환생할 이유가 사라진다.</summary>
		[Test]
		public void Pulling_CostsRebirthStones()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			Assert.IsFalse(IdleGacha.TryPull(state, tuning, out IdleHeroPull _), "빈손인데 뽑혔다");

			// 자원만 있어도 안 되고, 환생석만 있어도 안 된다 — 둘 다 낸다.
			state.Resource = IdleGacha.CostOf(state, tuning);
			Assert.IsFalse(IdleGacha.TryPull(state, tuning, out IdleHeroPull _), "환생석 없이 뽑혔다");

			state.Stones = IdleGacha.StoneCostOf(tuning);
			Assert.IsTrue(IdleGacha.TryPull(state, tuning, out IdleHeroPull got));
			Assert.AreEqual(0d, state.Resource, 1e-6d, "자원 값을 안 치렀다");
			Assert.AreEqual(0L, state.Stones, "환생석 값을 안 치렀다");
			Assert.AreEqual(1, state.Heroes.Count, "뽑았는데 도감에 안 들어왔다");
			Assert.IsTrue(got.IsNew);
		}

		/// <summary>
		/// ★ <b>중복이 꽝이 아니다</b> — 쌓여서 ★ 이 오른다.
		///
		/// 수집형이 죽는 자리가 정확히 여기다: 네 번째부터 나오는 얼굴이 전부 꽝이면
		/// 뽑기의 두근거림이 사라진다.
		/// </summary>
		[Test]
		public void Duplicates_BecomeStars()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Heroes.Add(new IdleHeroOwned(0));

			int before = state.Heroes[0].Stars;

			// 같은 얼굴을 필요한 만큼 먹인다.
			for (int one = 0; one < IdleGacha.CopiesForNextStar(before, tuning); one++)
			{
				GiveSpecific(state, tuning, 0);
			}

			Assert.Greater(state.Heroes[0].Stars, before, "중복을 먹였는데 ★ 이 그대로다");
		}

		/// <summary>★ 첫 영웅은 <b>빈 자리에 스스로</b> 앉는다 — 뽑았는데 아무 일도 안 나면 안 된다.</summary>
		[Test]
		public void FirstHeroes_TakeEmptyPartySlots()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			Afford(state, tuning, 3);

			for (int one = 0; one < 3; one++)
			{
				IdleGacha.TryPull(state, tuning, out IdleHeroPull _);
			}

			int seated = 0;
			for (int slot = 0; slot < state.Party.Length; slot++)
			{
				if (state.Party[slot] >= 0)
				{
					seated++;
				}
			}

			Assert.Greater(seated, 0, "뽑은 영웅이 아무 자리에도 안 앉았다");
		}

		/// <summary>
		/// ★ <b>보유만 해도</b> 판이 세진다 — 파티에 못 든 얼굴에게 존재 이유를 준다.
		/// </summary>
		[Test]
		public void OwningAlone_MakesYouStronger()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState empty = new IdleState();
			IdleState owning = new IdleState();

			// 파티에는 안 앉힌다 — 순수하게 <b>보유</b> 몫만 잰다.
			owning.Heroes.Add(new IdleHeroOwned(0));

			Assert.Greater(IdleModel.DamageOf(owning, tuning), IdleModel.DamageOf(empty, tuning),
				"들고 있어도 아무 몫이 없다 — 그러면 중복도 새 얼굴도 의미가 없다");
		}

		/// <summary>★ <b>내보내면 더</b> 세진다 — 안 그러면 「누구를 내보낼까」가 결정이 아니다.</summary>
		[Test]
		public void SendingOut_BeatsJustOwning()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState owning = new IdleState();
			owning.Heroes.Add(new IdleHeroOwned(0));

			IdleState fighting = new IdleState();
			fighting.Heroes.Add(new IdleHeroOwned(0));
			fighting.Party[0] = 0;

			Assert.Greater(IdleModel.DamageOf(fighting, tuning), IdleModel.DamageOf(owning, tuning),
				"내보내나 안 내보내나 같다 — 파티 자리가 장식이 된다");
		}

		/// <summary>
		/// ★ 축이 갈린다 — 공격 영웅은 공격만, 기지 영웅은 기지만 민다.
		///
		/// 이게 무너지면 「무엇을 뽑았나」가 아무 뜻이 없어지고 그냥 숫자 하나가 된다.
		/// </summary>
		[Test]
		public void EachHero_PushesItsOwnAxis()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState plain = new IdleState();
			plain.EnsureProducerRoom(tuning.ProducerCount);

			IdleState baseHero = new IdleState();
			baseHero.EnsureProducerRoom(tuning.ProducerCount);
			baseHero.Heroes.Add(new IdleHeroOwned(1)); // 네모 = 기지 축

			Assert.Greater(IdleBase.OutputPerSecond(baseHero, tuning), IdleBase.OutputPerSecond(plain, tuning),
				"기지 영웅인데 기지가 그대로다");
			Assert.AreEqual(IdleModel.AttackSpeedOf(plain, tuning), IdleModel.AttackSpeedOf(baseHero, tuning), 1e-9d,
				"기지 영웅이 공격속도까지 올렸다 — 축이 안 갈렸다");
		}

		/// <summary>
		/// ★ 같은 갈래는 <b>더하고</b>, 도감은 <b>곱한다</b> — 한 갈래에 몰아줄수록 수확이 체감한다.
		/// </summary>
		[Test]
		public void SameAxisAdds_CodexMultiplies()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			state.Heroes.Add(new IdleHeroOwned(0));
			double one = IdleHeroes.OwnedMultiplierOf(state, tuning, IdleHeroAxis.Damage);

			state.Heroes.Add(new IdleHeroOwned(4)); // 쐐기 = 같은 공격 축, 레어
			double two = IdleHeroes.OwnedMultiplierOf(state, tuning, IdleHeroAxis.Damage);

			// 합이므로 <b>배수의 초과분</b>이 정확히 더해진다.
			double firstShare = IdleHeroes.OwnedShareOf(state.Heroes[0], tuning);
			double secondShare = IdleHeroes.OwnedShareOf(state.Heroes[1], tuning);

			Assert.AreEqual(1d + firstShare, one, 1e-9d);
			Assert.AreEqual(1d + firstShare + secondShare, two, 1e-9d, "같은 갈래가 곱해졌다 — 폭주한다");
		}

		/// <summary>★ 도감은 <b>문턱마다 계단</b>으로 오른다 — 매끈하면 채운 순간이 안 느껴진다.</summary>
		[Test]
		public void Codex_RisesInSteps()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			double before = IdleHeroes.CodexMultiplierOf(state, tuning);

			for (int id = 0; id < tuning.CodexStepScore; id++)
			{
				state.Heroes.Add(new IdleHeroOwned(id));
			}

			Assert.Greater(IdleHeroes.CodexMultiplierOf(state, tuning), before, "문턱을 넘었는데 안 올랐다");
		}

		/// <summary>
		/// ★ <b>천장</b>이 약속을 지킨다 — 불운이 길어도 반드시 최고 등급이 온다.
		/// </summary>
		[Test]
		public void Pity_EventuallyGivesLegend()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			Afford(state, tuning, tuning.PityPulls + 1);

			bool sawLegend = false;

			for (int one = 0; one <= tuning.PityPulls; one++)
			{
				if (IdleGacha.TryPull(state, tuning, out IdleHeroPull got) == false)
				{
					break;
				}

				if (got.Grade == IdleHeroGrade.Legend)
				{
					sawLegend = true;
					break;
				}
			}

			Assert.IsTrue(sawLegend, "천장까지 뽑았는데 최고 등급이 한 번도 안 나왔다");
		}

		/// <summary>★ 같은 얼굴이 두 자리를 먹지 않는다 — 그러면 셋을 고르는 뜻이 사라진다.</summary>
		[Test]
		public void OneFace_CannotTakeTwoSlots()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Heroes.Add(new IdleHeroOwned(0));
			state.Heroes.Add(new IdleHeroOwned(1));

			IdleSession session = new IdleSession(tuning, state);
			session.Send(new IdleSetPartyIntent(0, 0));
			session.Send(new IdleSetPartyIntent(1, 1));
			session.Send(new IdleSetPartyIntent(1, 0));

			Assert.AreNotEqual(state.Party[0], state.Party[1], "같은 영웅이 두 자리에 앉았다");
		}

		/// <summary>도감과 파티가 저장을 건넌다 — 안 그러면 껐다 켤 때마다 처음부터 모은다.</summary>
		[Test]
		public void HeroesSurviveSaveLoad()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			Afford(state, tuning, 5);

			for (int one = 0; one < 5; one++)
			{
				IdleGacha.TryPull(state, tuning, out IdleHeroPull _);
			}

			IdleState restored = new IdleState();
			restored.Load(state.Save());

			Assert.AreEqual(state.Heroes.Count, restored.Heroes.Count);
			Assert.AreEqual(state.Party[0], restored.Party[0]);
			Assert.AreEqual(state.PullsSincePity, restored.PullsSincePity);
		}

		/// <summary>옛 저장에는 영웅이 없다 — 빈 도감으로 들어온다(터지지 않는다).</summary>
		[Test]
		public void OldSaves_LoadWithNoHeroes()
		{
			IdleState fromOld = new IdleState();
			fromOld.Load(new IdleSaveData { Resource = 5d });

			Assert.IsNotNull(fromOld.Heroes);
			Assert.AreEqual(0, fromOld.Heroes.Count);
			Assert.AreEqual(3, fromOld.Party.Length);
		}

		/// <summary>시험용 — 이만큼 뽑을 수 있게 재화를 얹는다(값이 뽑을수록 오르므로 넉넉히).</summary>
		private static void Afford(IdleState state, IdleTuning tuning, int pulls)
		{
			state.Stones += pulls;
			state.Resource += tuning.PullCostBase
				* System.Math.Pow(tuning.PullCostRatio, state.PullsDone + pulls) * pulls;
		}

		/// <summary>시험용 — 원하는 영웅이 나올 때까지 뽑는다(주사위를 건드리지 않는다).</summary>
		private static void GiveSpecific(IdleState state, IdleTuning tuning, int id)
		{
			int at = state.IndexOfHero(id);
			IdleHeroOwned owned = state.Heroes[at];
			owned.Copies += 1;

			int needed = IdleGacha.CopiesForNextStar(owned.Stars, tuning);
			if (owned.Stars < tuning.MaxStars && owned.Copies >= needed)
			{
				owned.Copies -= needed;
				owned.Stars += 1;
			}

			state.Heroes[at] = owned;
		}

		/// <summary>
		/// ★ 화면에 적는 확률이 <b>실제 굴림과 같아야</b> 한다 (TASK-WM-406).
		///
		/// 적어만 두고 다르면 그건 침묵보다 나쁘다 — 거짓말이 된다.
		/// 사진(IdleSnapshot)이 내주는 값이 손잡이 그대로인지 못 박고,
		/// 많이 굴려서 실제 비율이 그 언저리인지도 본다.
		/// </summary>
		[Test]
		public void PublishedOdds_MatchTheRealRoll()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			IdleSession session = new IdleSession(tuning, state);

			IdleSnapshot shown = session.Capture();
			Assert.AreEqual(tuning.LegendChance, shown.LegendChance, 1e-9d, "적는 값과 손잡이가 다르다");
			Assert.AreEqual(tuning.EpicChance, shown.EpicChance, 1e-9d);
			Assert.AreEqual(tuning.RareChance, shown.RareChance, 1e-9d);

			// 천장을 아주 멀리 밀어 두고 굴려야 <순수한 확률>이 보인다.
			tuning.PityPulls = 100000;

			int legend = 0;
			int rolls = 40000;

			for (int one = 0; one < rolls; one++)
			{
				state.Resource = 1e30d;
				state.Stones = 1L;
				state.PullsDone = 0L;

				if (IdleGacha.TryPull(state, tuning, out IdleHeroPull got) && got.Grade == IdleHeroGrade.Legend)
				{
					legend++;
				}
			}

			double seen = (double)legend / rolls;
			TestContext.WriteLine("[확률] 레전드 적은 값 " + tuning.LegendChance.ToString("P2")
				+ " · 4만번 굴린 실제 " + seen.ToString("P2"));

			// 4만 판이면 2% 의 표준오차는 0.07%p — 네 배(0.28%p) 안이면 같은 확률로 본다.
			// (4천 판으로는 오차가 0.22%p 라 <어긋남>과 <운>을 못 가른다. 그래서 늘렸다.)
			Assert.Less(System.Math.Abs(seen - tuning.LegendChance), 0.0028d,
				"적은 확률과 실제 굴림이 어긋난다");
		}
	}
}
