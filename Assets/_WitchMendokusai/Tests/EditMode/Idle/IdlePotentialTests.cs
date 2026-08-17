using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 도박이 도박이면서 <b>깊이를 못 건너뛰는가</b> (TASK-WM-406).
	///
	/// ★ 이 게임의 몸통은 연쇄다: 깊이 → 장비 등급 → 잠재 등급 → 값.
	///   근거는 울티마 스쿼드의 실제 표다(2~3등급 레어 · 4~5 에픽 · 6 유니크 · 7~8 레전드리).
	///   여기서 지키는 것은 <b>운으로 그 연쇄를 못 건너뛴다</b>이다 —
	///   얕은 데서 아무리 굴려도 깊은 데의 최저값을 못 이겨야 「내려갈 이유」가 산다.
	/// </summary>
	public sealed class IdlePotentialTests
	{
		private const double TOLERANCE = 1e-12d;

		/// <summary>
		/// 그 등급짜리를 <paramref name="count"/> 개 가진 판.
		/// ★ 자원도 넉넉히 쥐여 준다 — 이제 <b>감정에 자원이 든다</b>(기지와 모험을 같은 저울에 올린 자리).
		///   여기서 재려는 것은 「굴리면 어떻게 되나」지 「자원이 있나」가 아니다.
		/// </summary>
		private static IdleState WithDrops(int tier, long count)
		{
			IdleState state = new IdleState();
			state.EnsureTierRoom(tier);
			state.DroppedByTier[tier - 1] = count;
			state.Resource = IdleGear.AppraiseCost(tier, new IdleTuning()) * (count + 1L);
			return state;
		}

		/// <summary>울티마 스쿼드 표 그대로.</summary>
		[Test]
		public void GradeTable_MatchesTheReference()
		{
			Assert.AreEqual(PotentialGrade.None, IdlePotentials.GradeFor(1));
			Assert.AreEqual(PotentialGrade.Rare, IdlePotentials.GradeFor(2));
			Assert.AreEqual(PotentialGrade.Rare, IdlePotentials.GradeFor(3));
			Assert.AreEqual(PotentialGrade.Epic, IdlePotentials.GradeFor(4));
			Assert.AreEqual(PotentialGrade.Epic, IdlePotentials.GradeFor(5));
			Assert.AreEqual(PotentialGrade.Unique, IdlePotentials.GradeFor(6));
			Assert.AreEqual(PotentialGrade.Legendary, IdlePotentials.GradeFor(7));
			Assert.AreEqual(PotentialGrade.Legendary, IdlePotentials.GradeFor(8));
		}

		/// <summary>
		/// ★ 핵심 — <b>운으로 등급을 못 건너뛴다.</b>
		/// 아래 등급의 <b>최고값</b>이 위 등급의 <b>최저값</b>보다 낮아야 등급이 뜻을 갖는다.
		/// </summary>
		[Test]
		public void LuckNeverBeatsDepth_GradesDoNotOverlap()
		{
			IdleTuning tuning = new IdleTuning();

			PotentialGrade[] ladder =
			{
				PotentialGrade.Rare, PotentialGrade.Epic, PotentialGrade.Unique, PotentialGrade.Legendary,
			};

			for (int i = 1; i < ladder.Length; i++)
			{
				double bestOfLower = IdlePotentials.CeilingOf(ladder[i - 1], tuning);
				double worstOfHigher = IdlePotentials.FloorOf(ladder[i], tuning);

				Assert.Less(bestOfLower, worstOfHigher,
					ladder[i - 1] + " 최고값이 " + ladder[i] + " 최저값을 넘는다 — 얕은 데서 운으로 깊이를 이긴다");
			}
		}

		/// <summary>감정하면 개수를 하나 쓴다 — 안 쓰면 무한히 굴려 깊이가 뜻을 잃는다.</summary>
		[Test]
		public void Appraising_SpendsOneDrop()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = WithDrops(4, 3L);

			Assert.IsTrue(IdlePotentials.TryAppraise(state, tuning, 4, out PotentialRoll roll));

			Assert.AreEqual(2L, state.DroppedByTier[3], "개수가 안 줄었다");
			Assert.AreEqual(PotentialGrade.Epic, roll.Grade);
		}

		/// <summary>없는 것은 못 감정한다.</summary>
		[Test]
		public void CannotAppraise_WhatYouDoNotHave()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState empty = new IdleState();

			Assert.IsFalse(IdlePotentials.TryAppraise(empty, tuning, 4, out PotentialRoll _));

			// 1등급은 잠재가 안 붙는다 — 가지고 있어도 못 감정한다.
			IdleState lowly = WithDrops(1, 10L);
			Assert.IsFalse(IdlePotentials.TryAppraise(lowly, tuning, 1, out PotentialRoll _));
			Assert.AreEqual(10L, lowly.DroppedByTier[0], "못 감정했는데 개수가 줄었다");
		}

		/// <summary>나온 값이 그 등급의 범위 안이다 — 여러 번 굴려 확인한다.</summary>
		[Test]
		public void RolledValue_StaysInsideItsGrade()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = WithDrops(7, 500L);

			double floor = IdlePotentials.FloorOf(PotentialGrade.Legendary, tuning);
			double ceiling = IdlePotentials.CeilingOf(PotentialGrade.Legendary, tuning);

			for (int i = 0; i < 500; i++)
			{
				Assert.IsTrue(IdlePotentials.TryAppraise(state, tuning, 7, out PotentialRoll roll));
				Assert.GreaterOrEqual(roll.Value, floor);
				Assert.Less(roll.Value, ceiling);
			}
		}

		/// <summary>더 좋을 때만 갈아 끼운다 — 나빠지면 사람이 안 누른다.</summary>
		[Test]
		public void OnlyBetterRolls_Replace()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = WithDrops(7, 200L);

			double best = 0d;
			for (int i = 0; i < 200; i++)
			{
				IdlePotentials.TryAppraise(state, tuning, 7, out PotentialRoll roll);

				if (roll.Value > best)
				{
					best = roll.Value;
					Assert.IsTrue(roll.Replaced, "더 좋은데 안 갈아 끼웠다");
				}
				else
				{
					Assert.IsFalse(roll.Replaced, "더 나쁜데 갈아 끼웠다");
				}

				Assert.AreEqual(best, state.BestPotentialValue, TOLERANCE);
			}

			TestContext.WriteLine("[IdlePotential] 레전드리 200번 굴려 최고 " + (best * 100d).ToString("N2") + "%"
				+ " (범위 " + (IdlePotentials.FloorOf(PotentialGrade.Legendary, tuning) * 100d).ToString("N1")
				+ "~" + (IdlePotentials.CeilingOf(PotentialGrade.Legendary, tuning) * 100d).ToString("N1") + "%)");
		}

		/// <summary>같은 씨앗이면 같은 판 — 시험이 통계가 아니게 하는 근거.</summary>
		[Test]
		public void SameSeed_SameRolls()
		{
			IdleRandom one = new IdleRandom(12345L);
			IdleRandom two = new IdleRandom(12345L);

			for (int i = 0; i < 50; i++)
			{
				Assert.AreEqual(one.NextDouble(), two.NextDouble(), TOLERANCE);
			}

			IdleRandom other = new IdleRandom(999L);
			Assert.AreNotEqual(new IdleRandom(12345L).NextDouble(), other.NextDouble());
		}

		/// <summary>주사위가 0 이상 1 미만에 머문다.</summary>
		[Test]
		public void Dice_StaysInRange()
		{
			IdleRandom dice = new IdleRandom(7L);

			for (int i = 0; i < 10_000; i++)
			{
				double value = dice.NextDouble();
				Assert.GreaterOrEqual(value, 0d);
				Assert.Less(value, 1d);
			}
		}

		/// <summary>
		/// ★ <b>껐다 켜서 다시 굴리기가 공짜가 아니다</b> — 주사위 상태가 저장에 실린다.
		/// 안 실으면 나쁜 값이 나올 때마다 껐다 켜면 되고, 그러면 도박이 도박이 아니다.
		/// </summary>
		[Test]
		public void ReloadingDoesNotRerollTheDice()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState straight = WithDrops(6, 5L);
			IdlePotentials.TryAppraise(straight, tuning, 6, out PotentialRoll first);
			IdlePotentials.TryAppraise(straight, tuning, 6, out PotentialRoll second);

			// 같은 자리에서 저장하고 되살린 뒤 두 번째를 굴린다 — 같은 값이 나와야 한다.
			IdleState saved = WithDrops(6, 5L);
			IdlePotentials.TryAppraise(saved, tuning, 6, out PotentialRoll firstAgain);

			IdleState reloaded = new IdleState();
			reloaded.Load(saved.Save());
			IdlePotentials.TryAppraise(reloaded, tuning, 6, out PotentialRoll secondAfterReload);

			Assert.AreEqual(first.Value, firstAgain.Value, TOLERANCE);
			Assert.AreEqual(second.Value, secondAfterReload.Value, TOLERANCE,
				"껐다 켜니 다른 값이 나왔다 — 다시 굴리기가 공짜다");
		}

		/// <summary>잠재는 저장과 리셋을 건넌다 — 장비가 판을 건너 남는 것과 같은 이치.</summary>
		[Test]
		public void Potential_SurvivesSaveAndPrestige()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = WithDrops(7, 20L);
			state.Stage = 30;

			for (int i = 0; i < 20; i++)
			{
				IdlePotentials.TryAppraise(state, tuning, 7, out PotentialRoll _);
			}

			double best = state.BestPotentialValue;
			Assert.Greater(best, 0d);

			IdleState restored = new IdleState();
			restored.Load(state.Save());
			Assert.AreEqual(best, restored.BestPotentialValue, TOLERANCE);
			Assert.AreEqual((int)PotentialGrade.Legendary, restored.BestPotentialGrade);

			Assert.IsTrue(IdleModel.TryPrestige(restored, tuning, out long _));
			Assert.AreEqual(best, restored.BestPotentialValue, TOLERANCE, "리셋에 잠재가 지워졌다");
		}

		/// <summary>잠재가 공격력에 실제로 실린다 — 안 실리면 굴릴 이유가 없다.</summary>
		[Test]
		public void Potential_MultipliesDamage()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState plain = new IdleState();
			IdleState blessed = new IdleState { BestPotentialValue = 0.25d };

			Assert.AreEqual(1d, IdleModel.PotentialMultiplier(plain), TOLERANCE);
			Assert.AreEqual(1.25d, IdleModel.PotentialMultiplier(blessed), TOLERANCE);
			Assert.AreEqual(IdleModel.DamageOf(plain, tuning) * 1.25d, IdleModel.DamageOf(blessed, tuning), 1e-9d);
		}

		/// <summary>의도로도 굴러간다 — 표현이 쓰는 길이 진짜 도는지.</summary>
		[Test]
		public void Intent_Appraises()
		{
			IdleTuning tuning = new IdleTuning();
			IdleSession session = new IdleSession(tuning, WithDrops(5, 1L));

			Assert.IsTrue(session.Send(new IdleAppraiseIntent(5)));
			Assert.IsFalse(session.Send(new IdleAppraiseIntent(5)), "다 썼는데 또 굴러갔다");

			Assert.AreEqual((int)PotentialGrade.Epic, session.State.BestPotentialGrade);
			Assert.Greater(session.Capture().BestPotentialValue, 0d);
		}

		/// <summary>
		/// ⚠ <b>지금 감정은 아이템에 안 붙는다</b> — 판 전체 값 하나만 올린다 (성질 고정, U7).
		///
		/// 이 시험은 「이게 옳다」가 아니라 <b>「지금 이렇다」</b>를 못 박는 것이다.
		/// 코드에 두 체계가 겹쳐 있다(2026-08-17 발견):
		///   · 감정은 등급별 <b>개수</b> 하나를 쓰고 <see cref="IdleState.BestPotentialValue"/> 만 올린다
		///   · 그런데 아이템에는 <c>PotentialValue</c> 칸이 있고 착용 배수·합치기·「차라」 안내가 그걸 본다
		///   · <b>그 칸에 값을 쓰는 코드가 어디에도 없다</b> = 모든 장비가 영원히 미감정
		/// 그래서 「좋은 잠재를 지킬까, 등급을 올릴까」는 글에만 있고 판에는 없다.
		/// 어느 쪽으로 갈지는 사용자 결정(decision-sheet U7) — 그때 이 시험이 <b>먼저 빨개진다</b>.
		/// </summary>
		[Test]
		public void Appraising_MovesTheBoardValue_NotTheItem()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureTierRoom(6);
			state.Resource = 1e12d;
			state.DroppedByTier[2] = 5L;

			state.Bag.Add(new IdleItem(3, IdleItemSlot.Head));

			Assert.IsTrue(IdlePotentials.TryAppraise(state, tuning, 3, out PotentialRoll roll));
			Assert.Greater(roll.Value, 0d, "감정했는데 아무 값도 안 나왔다");
			Assert.Greater(state.BestPotentialValue, 0d, "판 전체 값이 안 올랐다");

			Assert.IsTrue(state.Bag[0].IsRaw,
				"가방의 것이 감정됐다 — U7 이 ⓐ 로 정해졌다면 이 시험을 고쳐라(반대면 회귀다)");
		}

		/// <summary>
		/// ★ 「왜 감정을 못 하나」는 <b>한 자리에서만</b> 판정한다 — 화면이 베끼면 거짓말이 된다.
		///
		/// 전에는 화면이 세 조건(1등급 · 안 떨어짐 · 자원 부족)을 자기가 베껴 봤다.
		/// 코어가 규칙을 바꾸면 버튼은 켜져 있는데 눌러도 아무 일이 안 나는 상태가 된다.
		/// </summary>
		[Test]
		public void TheReasonYouCantAppraise_ComesFromOnePlace()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureTierRoom(6);

			Assert.AreEqual(AppraiseBlock.TierTooLow, IdlePotentials.WhyNot(state, tuning, 1),
				"1등급엔 잠재가 안 붙는데 다른 이유를 댄다");

			Assert.AreEqual(AppraiseBlock.NothingDropped, IdlePotentials.WhyNot(state, tuning, 3),
				"아직 안 떨어졌는데 다른 이유를 댄다");

			state.DroppedByTier[2] = 2L;
			Assert.AreEqual(AppraiseBlock.TooPoor, IdlePotentials.WhyNot(state, tuning, 3),
				"자원이 0 인데 할 수 있다고 한다");

			state.Resource = 1e12d;
			Assert.AreEqual(AppraiseBlock.None, IdlePotentials.WhyNot(state, tuning, 3));
		}

		/// <summary>★ <b>막힌 이유가 있으면</b> 감정도 안 된다 — 둘이 같은 답을 쓴다.</summary>
		[Test]
		public void WhenBlocked_AppraisingDoesNothing()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureTierRoom(6);
			state.DroppedByTier[2] = 1L;
			state.Resource = 0d;

			Assert.AreNotEqual(AppraiseBlock.None, IdlePotentials.WhyNot(state, tuning, 3));
			Assert.IsFalse(IdlePotentials.TryAppraise(state, tuning, 3, out PotentialRoll _),
				"막혔다고 해 놓고 감정은 된다 — 둘이 다른 규칙을 쓰고 있다");
			Assert.AreEqual(1L, state.DroppedByTier[2], "안 됐는데 개수가 줄었다");
		}
	}
}
